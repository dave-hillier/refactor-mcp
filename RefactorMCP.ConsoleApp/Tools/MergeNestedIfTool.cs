using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Threading;

[McpServerToolType]
public static class MergeNestedIfTool
{
    [McpServerTool, Description("Merge an if statement whose only statement is another if into one if on both conditions joined by &&")]
    public static async Task<string> MergeNestedIf(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line of the outer if keyword (1-based)")] int line,
        [Description("Column of the outer if keyword (1-based)")] int column,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var caret = await CaretDocument.FindAsync(solutionPath, filePath, line, column, cancellationToken);
            var outer = caret.IfStatement();
            if (outer.Else is not null)
                throw new McpException("Error: The outer if has an else, which would also run when only the inner condition is false");

            var inner = InnerIf(outer);
            if (inner.Else is not null)
                throw new McpException("Error: The inner if has an else, which would also run when the outer condition is false");

            var condition = SyntaxFactory.BinaryExpression(
                SyntaxKind.LogicalAndExpression,
                outer.Condition.WithoutTrivia(),
                SyntaxFactory.Token(SyntaxKind.AmpersandAmpersandToken).WithLeadingTrivia(SyntaxFactory.Space).WithTrailingTrivia(SyntaxFactory.Space),
                inner.Condition.WithoutTrivia());
            condition = condition.ReplaceNodes(
                new[] { condition.Left, condition.Right },
                (original, _) => ExpressionPlacement.Fit(original, original));

            var merged = outer
                .WithCondition(condition.WithTriviaFrom(outer.Condition))
                .WithStatement(BodyKeepingComments(inner).WithTriviaFrom(outer.Statement).WithAdditionalAnnotations(Formatter.Annotation));

            await caret.ApplyAsync(caret.Root.ReplaceNode(outer, merged), cancellationToken);
            return $"Successfully merged the nested if statements in {filePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error merging nested if: {ex.Message}", ex);
        }
    }

    private static IfStatementSyntax InnerIf(IfStatementSyntax outer) => outer.Statement switch
    {
        IfStatementSyntax inner => inner,
        BlockSyntax { Statements: [IfStatementSyntax inner] } => inner,
        BlockSyntax block when block.Statements.OfType<IfStatementSyntax>().Any() =>
            throw new McpException("Error: The outer if does more than the inner if, so its other statements would come under both conditions"),
        _ => throw new McpException("Error: The outer if does not contain an if statement to merge"),
    };

    /// <summary>
    /// The inner if's body. Comments written above the inner if stay inside
    /// the braces they were in, at the top of the merged body.
    /// </summary>
    private static StatementSyntax BodyKeepingComments(IfStatementSyntax inner)
    {
        var body = inner.Statement;
        var comments = inner.GetLeadingTrivia();
        if (body is not BlockSyntax { Statements.Count: > 0 } block
            || !comments.Any(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) || t.IsKind(SyntaxKind.MultiLineCommentTrivia)))
            return body;

        // The inner if's own indentation ends its leading trivia.
        if (comments.Last().IsKind(SyntaxKind.WhitespaceTrivia))
            comments = comments.RemoveAt(comments.Count - 1);

        var first = block.Statements[0];
        return block.ReplaceNode(first, first.WithLeadingTrivia(comments.AddRange(first.GetLeadingTrivia())));
    }
}
