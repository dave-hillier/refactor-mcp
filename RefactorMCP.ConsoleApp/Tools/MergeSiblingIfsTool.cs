using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Threading;

[McpServerToolType]
public static class MergeSiblingIfsTool
{
    [McpServerTool, Description("Merge an if statement with the else if or the if statement after it when both have the same body, into one if on both conditions joined by ||; a following if statement must share a body that always jumps away")]
    public static async Task<string> MergeSiblingIfs(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line of the first if keyword (1-based)")] int line,
        [Description("Column of the first if keyword (1-based)")] int column,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var caret = await CaretDocument.FindAsync(solutionPath, filePath, line, column, cancellationToken);
            var statement = caret.IfStatement();
            var (next, following) = Next(statement, caret)
                ?? throw new McpException("Error: The if statement is not followed by an else if or an if statement with the same body");

            if (DeclaresVariable(statement.Condition) || DeclaresVariable(next.Condition))
                throw new McpException("Error: A condition declares a variable, which joining the conditions with || would leave unassigned");

            var condition = Combine(new[] { statement.Condition, next.Condition }, SyntaxKind.LogicalOrExpression);
            var merged = statement.WithCondition(condition.WithTriviaFrom(statement.Condition)).WithElse(next.Else);

            SyntaxNode newRoot;
            if (following)
            {
                merged = merged.WithLeadingTrivia(LeadingComments(statement, new[] { next }));
                newRoot = caret.Root.TrackNodes(statement, next);
                newRoot = newRoot.ReplaceNode(newRoot.GetCurrentNode(statement)!, merged);
                newRoot = newRoot.RemoveNode(newRoot.GetCurrentNode(next)!, SyntaxRemoveOptions.KeepNoTrivia)!;
            }
            else
            {
                newRoot = caret.Root.ReplaceNode(statement, merged);
            }

            await caret.ApplyAsync(newRoot, cancellationToken);
            return $"Successfully merged the if statements in {filePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error merging sibling ifs: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// The if to merge with: the else if that continues the chain, or, when
    /// the if has no else, the if statement directly after it. Either needs
    /// the same body. A following if only runs when the first condition was
    /// false if that body always jumps away, which is what <c>||</c> does.
    /// </summary>
    private static (IfStatementSyntax Next, bool Following)? Next(IfStatementSyntax statement, CaretDocument caret)
    {
        if (statement.Else is { } elseClause)
        {
            return elseClause.Statement is IfStatementSyntax elseIf && SyntaxFactory.AreEquivalent(elseIf.Statement, statement.Statement)
                ? (elseIf, false)
                : null;
        }

        if (CaretDocument.Siblings(statement) is not { } siblings)
            return null;

        var index = siblings.IndexOf(statement);
        if (index + 1 >= siblings.Count
            || siblings[index + 1] is not IfStatementSyntax next
            || !SyntaxFactory.AreEquivalent(next.Statement, statement.Statement))
            return null;

        if (caret.EndPointIsReachable(CaretDocument.Statements(statement.Statement)))
        {
            throw new McpException(
                "Error: The ifs share a body that can fall through, so both run it when both conditions hold, where one if would run it once");
        }

        return (next, true);
    }

    internal static bool DeclaresVariable(ExpressionSyntax condition) =>
        condition.DescendantNodes().Any(n => n is SingleVariableDesignationSyntax or DeclarationExpressionSyntax);

    /// <summary>The conditions joined in order, parenthesised only where precedence needs it.</summary>
    internal static ExpressionSyntax Combine(IReadOnlyList<ExpressionSyntax> conditions, SyntaxKind kind)
    {
        var token = SyntaxFactory.Token(kind == SyntaxKind.LogicalAndExpression ? SyntaxKind.AmpersandAmpersandToken : SyntaxKind.BarBarToken)
            .WithLeadingTrivia(SyntaxFactory.Space)
            .WithTrailingTrivia(SyntaxFactory.Space);

        var combined = conditions[0].WithoutTrivia();
        foreach (var next in conditions.Skip(1))
        {
            var binary = SyntaxFactory.BinaryExpression(kind, combined, token, next.WithoutTrivia());
            combined = binary.ReplaceNodes(
                new[] { binary.Left, binary.Right },
                (original, _) => ExpressionPlacement.Fit(original, original));
        }

        return combined;
    }

    /// <summary>
    /// The first if's leading trivia followed by the comments above the ifs
    /// it absorbs, so that they stay above the combined if.
    /// </summary>
    internal static SyntaxTriviaList LeadingComments(IfStatementSyntax statement, IEnumerable<IfStatementSyntax> absorbed)
    {
        var leading = statement.GetLeadingTrivia();
        var commented = absorbed.Where(HasComments).ToList();
        if (commented.Count == 0)
            return leading;

        // The if's own indentation ends its trivia; it goes after the comments.
        var indentation = leading.LastOrDefault();
        if (indentation.IsKind(SyntaxKind.WhitespaceTrivia))
            leading = leading.RemoveAt(leading.Count - 1);
        foreach (var next in commented)
        {
            leading = leading.AddRange(HierarchyMemberHelpers.WithoutLeadingBlankLines(next.GetLeadingTrivia()));
            if (leading.LastOrDefault().IsKind(SyntaxKind.WhitespaceTrivia))
                leading = leading.RemoveAt(leading.Count - 1);
        }

        return indentation.IsKind(SyntaxKind.WhitespaceTrivia) ? leading.Add(indentation) : leading;
    }

    private static bool HasComments(SyntaxNode node) =>
        node.GetLeadingTrivia().Any(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) || t.IsKind(SyntaxKind.MultiLineCommentTrivia));
}
