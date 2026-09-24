using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Threading;

[McpServerToolType]
public static class RemoveRedundantElseTool
{
    [McpServerTool, Description("Remove the else of an if statement whose branch always jumps away (returns, throws, breaks or continues), so the else's statements follow the if; an else if becomes an if of its own")]
    public static async Task<string> RemoveRedundantElse(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line of the if keyword (1-based)")] int line,
        [Description("Column of the if keyword (1-based)")] int column,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var caret = await CaretDocument.FindAsync(solutionPath, filePath, line, column, cancellationToken);
            var statement = caret.IfStatement();
            if (statement.Else is not { } elseClause)
                throw new McpException("Error: The if statement has no else to remove");
            if (CaretDocument.Siblings(statement) is not { } siblings)
                throw new McpException("Error: The if statement is not in a block or switch section, so nothing can follow it");
            if (caret.EndPointIsReachable(CaretDocument.Statements(statement.Statement)))
                throw new McpException("Error: The if statement's branch can run on past its end, so the else's statements would then run after it");

            EnsureNamesAreFree(statement, elseClause);

            var following = Following(elseClause);
            var index = siblings.IndexOf(statement);
            var statements = siblings.Take(index)
                .Append(statement.WithElse(null))
                .Concat(following)
                .Concat(siblings.Skip(index + 1));

            SyntaxNode newRoot = statement.Parent switch
            {
                BlockSyntax block => caret.Root.ReplaceNode(block, block.WithStatements(SyntaxFactory.List(statements))),
                SwitchSectionSyntax section => caret.Root.ReplaceNode(section, section.WithStatements(SyntaxFactory.List(statements))),
                _ => throw new InvalidOperationException("Siblings only come from blocks and switch sections"),
            };
            await caret.ApplyAsync(newRoot, cancellationToken);
            return $"Successfully removed the redundant else in {filePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error removing redundant else: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// The else's statements, as statements of the enclosing block: an else
    /// if is an if of its own. Comments above the else keyword go above the
    /// first of them, which is set apart from the if by a blank line.
    /// </summary>
    private static IReadOnlyList<StatementSyntax> Following(ElseClauseSyntax elseClause)
    {
        var statements = CaretDocument.Statements(elseClause.Statement).ToList();
        if (statements.Count == 0)
            return statements;

        var first = CaretDocument.WithoutLeadingBlankLines(statements[0]);
        if (elseClause.ElseKeyword.LeadingTrivia.Any(IsComment))
            first = first.WithLeadingTrivia(WithoutIndentation(elseClause.ElseKeyword.LeadingTrivia).AddRange(first.GetLeadingTrivia()));
        statements[0] = CaretDocument.WithBlankLineBefore(first);

        return statements.Select(s => s.WithAdditionalAnnotations(Formatter.Annotation)).ToList();
    }

    /// <summary>
    /// The else keyword's leading trivia without the indentation before the
    /// keyword itself, since the statement has its own.
    /// </summary>
    private static SyntaxTriviaList WithoutIndentation(SyntaxTriviaList trivia)
    {
        var list = trivia.ToList();
        if (list.Count > 0 && list[^1].IsKind(SyntaxKind.WhitespaceTrivia))
            list.RemoveAt(list.Count - 1);
        return SyntaxFactory.TriviaList(list);
    }

    private static bool IsComment(SyntaxTrivia trivia) =>
        trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia);

    /// <summary>
    /// The else's locals move into the enclosing block's scope, where no
    /// other local of the same name may be declared, including in the if's
    /// own branch.
    /// </summary>
    private static void EnsureNamesAreFree(IfStatementSyntax statement, ElseClauseSyntax elseClause)
    {
        var declared = DeclaredNames(CaretDocument.Statements(elseClause.Statement));
        if (declared.Count == 0)
            return;

        var others = DeclaredNames(statement.Parent!.ChildNodes().OfType<StatementSyntax>().Where(s => s != statement))
            .Concat(DeclaredNames(new[] { statement.Statement }));
        var clash = others.FirstOrDefault(declared.Contains);
        if (clash is not null)
            throw new McpException($"Error: The else declares '{clash}', which is also declared elsewhere in the block it would join");
    }

    private static HashSet<string> DeclaredNames(IEnumerable<StatementSyntax> statements) =>
        statements.SelectMany(s => s.DescendantNodesAndSelf(n => n is not AnonymousFunctionExpressionSyntax))
            .Select(n => n switch
            {
                VariableDeclaratorSyntax v => v.Identifier.ValueText,
                SingleVariableDesignationSyntax d => d.Identifier.ValueText,
                ForEachStatementSyntax f => f.Identifier.ValueText,
                LocalFunctionStatementSyntax l => l.Identifier.ValueText,
                CatchDeclarationSyntax c when c.Identifier != default => c.Identifier.ValueText,
                _ => null,
            })
            .OfType<string>()
            .ToHashSet();
}
