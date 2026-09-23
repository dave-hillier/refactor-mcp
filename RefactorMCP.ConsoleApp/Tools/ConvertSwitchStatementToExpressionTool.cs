using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Threading;

[McpServerToolType]
public static class ConvertSwitchStatementToExpressionTool
{
    [McpServerTool, Description("Convert a switch statement whose every section returns a value, or assigns one variable and breaks, into a switch expression")]
    public static async Task<string> ConvertSwitchStatementToExpression(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line of the switch keyword (1-based)")] int line,
        [Description("Column of the switch keyword (1-based)")] int column,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var caret = await CaretDocument.FindAsync(solutionPath, filePath, line, column, cancellationToken);
            var statement = caret.SwitchStatement();
            var arms = statement.Sections.Select(Arm).ToList();

            var values = arms.Where(a => a.Kind != ArmKind.Throw).ToList();
            if (values.Count == 0 || values.Any(a => a.Kind != values[0].Kind))
                throw new McpException("Error: A section does more than return a value or assign one variable, so it cannot become an arm");

            var target = values[0].Target;
            if (target is not null && values.Any(a => !SyntaxFactory.AreEquivalent(a.Target, target)))
                throw new McpException("Error: The sections assign different variables, so no single assignment can take the switch expression");

            // A switch statement with no default does nothing for an unmatched
            // value, where a switch expression would throw. A return after the
            // switch runs exactly then, so it becomes the discard arm.
            var replaced = new List<StatementSyntax> { statement };
            if (!arms.Any(a => a.IsDefault))
            {
                var following = FollowingStatement(statement);
                if (values[0].Kind != ArmKind.Return || following is not (ReturnStatementSyntax { Expression: not null } or ThrowStatementSyntax))
                    throw new McpException("Error: The switch has no default, so a switch expression would throw where the statement does nothing");

                arms.Add(Arm(following, new[] { SyntaxFactory.DefaultSwitchLabel() }, following));
                replaced.Add(following);
            }

            var prefix = target is null ? "return " : $"{target.WithoutTrivia()} = ";
            var expression = SwitchExpressionText(prefix, statement, arms.Where(a => !a.IsDefault).Concat(arms.Where(a => a.IsDefault)).ToList());
            var converted = SyntaxFactory.ParseStatement(expression)
                .WithLeadingTrivia(statement.GetLeadingTrivia())
                .WithTrailingTrivia(replaced[^1].GetTrailingTrivia());

            var newRoot = caret.Root.TrackNodes(replaced);
            newRoot = newRoot.ReplaceNode(newRoot.GetCurrentNode(statement)!, converted);
            if (replaced.Count > 1)
                newRoot = newRoot.RemoveNode(newRoot.GetCurrentNode(replaced[1])!, SyntaxRemoveOptions.KeepNoTrivia)!;

            await caret.ApplyAsync(newRoot, cancellationToken);
            return $"Successfully converted the switch statement to a switch expression in {filePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error converting switch statement to expression: {ex.Message}", ex);
        }
    }

    private enum ArmKind
    {
        Return,
        Assign,
        Throw,
    }

    /// <summary>
    /// What one section becomes: its patterns, the value it produces and how,
    /// and the comments written on it.
    /// </summary>
    private sealed record SectionArm(
        ArmKind Kind,
        IReadOnlyList<string> Patterns,
        bool IsDefault,
        ExpressionSyntax? Target,
        string Value,
        IReadOnlyList<string> Comments,
        string? TrailingComment);

    private static SectionArm Arm(SwitchSectionSyntax section)
    {
        var statements = section.Statements is [BlockSyntax block] ? block.Statements : section.Statements;
        return Arm(section, section.Labels, statements.ToArray());
    }

    private static SectionArm Arm(SyntaxNode commented, IEnumerable<SwitchLabelSyntax> labels, params StatementSyntax[] statements)
    {
        var (kind, target, value, last) = statements switch
        {
            [ReturnStatementSyntax { Expression: { } returned } r] => (ArmKind.Return, (ExpressionSyntax?)null, returned.WithoutTrivia().ToString(), (SyntaxNode)r),
            [ThrowStatementSyntax { Expression: { } thrown } t] => (ArmKind.Throw, null, $"throw {thrown.WithoutTrivia()}", t),
            [ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.SimpleAssignmentExpression } assignment }, BreakStatementSyntax b] =>
                (ArmKind.Assign, assignment.Left, assignment.Right.WithoutTrivia().ToString(), b),
            _ => throw new McpException("Error: A section does more than return a value or assign one variable, so it cannot become an arm"),
        };

        var labelList = labels.ToList();
        var isDefault = labelList.Any(l => l is DefaultSwitchLabelSyntax);
        var patterns = isDefault ? new List<string> { "_" } : Patterns(labelList);

        var comments = commented.DescendantTrivia(n => n is not ExpressionSyntax)
            .Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) || t.IsKind(SyntaxKind.MultiLineCommentTrivia))
            .ToList();
        var trailing = last.GetTrailingTrivia().Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia)).ToList();

        return new SectionArm(
            kind,
            patterns,
            isDefault,
            target,
            value,
            comments.Except(trailing).Select(t => t.ToString()).ToList(),
            trailing.Count > 0 ? trailing[0].ToString() : null);
    }

    /// <summary>
    /// A section's labels as arm patterns. Several labels become one pattern
    /// joined by <c>or</c>, unless one has a <c>when</c> clause or declares a
    /// variable, in which case each label becomes its own arm.
    /// </summary>
    private static List<string> Patterns(List<SwitchLabelSyntax> labels)
    {
        var patterns = labels.Select(label => label switch
        {
            CaseSwitchLabelSyntax constant => constant.Value.WithoutTrivia().ToString(),
            CasePatternSwitchLabelSyntax pattern => pattern.Pattern.WithoutTrivia() + (pattern.WhenClause is { } when ? $" {when.WithoutTrivia()}" : ""),
            _ => throw new McpException("Error: A section has a label that cannot become a pattern"),
        }).ToList();

        var separate = labels.OfType<CasePatternSwitchLabelSyntax>()
            .Any(l => l.WhenClause is not null || l.Pattern.DescendantNodes().OfType<SingleVariableDesignationSyntax>().Any());
        return separate || patterns.Count == 1 ? patterns : new List<string> { string.Join(" or ", patterns) };
    }

    private static StatementSyntax? FollowingStatement(SwitchStatementSyntax statement)
    {
        if (CaretDocument.Siblings(statement) is not { } siblings)
            return null;

        var index = siblings.IndexOf(statement);
        return index + 1 < siblings.Count ? siblings[index + 1] : null;
    }

    /// <summary>
    /// The statement's text, one arm per line, laid out as the switch
    /// statement was indented, with a trailing comma after the last arm.
    /// </summary>
    private static string SwitchExpressionText(string prefix, SwitchStatementSyntax statement, IReadOnlyList<SectionArm> arms)
    {
        var indentation = statement.GetLeadingTrivia().LastOrDefault(t => t.IsKind(SyntaxKind.WhitespaceTrivia)).ToString();
        var armIndentation = indentation + "    ";
        var endOfLine = statement.GetTrailingTrivia().FirstOrDefault(t => t.IsKind(SyntaxKind.EndOfLineTrivia)).ToString();
        if (endOfLine.Length == 0)
            endOfLine = "\n";

        var text = new StringBuilder();
        text.Append(prefix).Append(statement.Expression.WithoutTrivia()).Append(" switch").Append(endOfLine);
        text.Append(indentation).Append('{').Append(endOfLine);
        foreach (var arm in arms)
        {
            foreach (var comment in arm.Comments)
                text.Append(armIndentation).Append(comment).Append(endOfLine);

            foreach (var pattern in arm.Patterns)
            {
                text.Append(armIndentation).Append(pattern).Append(" => ").Append(arm.Value).Append(',');
                if (arm.TrailingComment is not null)
                    text.Append(' ').Append(arm.TrailingComment);
                text.Append(endOfLine);
            }
        }

        text.Append(indentation).Append("};");
        return text.ToString();
    }
}
