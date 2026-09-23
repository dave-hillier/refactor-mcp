using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Threading;

[McpServerToolType]
public static class SplitIfTool
{
    [McpServerTool, Description("Split an if statement on its first && into nested ifs, or on its first || into two ifs with the same body")]
    public static async Task<string> SplitIf(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line of the if keyword (1-based)")] int line,
        [Description("Column of the if keyword (1-based)")] int column,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var caret = await CaretDocument.FindAsync(solutionPath, filePath, line, column, cancellationToken);
            var statement = caret.IfStatement();
            if (WithoutParentheses(statement.Condition) is not BinaryExpressionSyntax condition
                || condition.Kind() is not (SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression))
                throw new McpException("Error: The condition is not joined by && or ||, so there is nothing to split");

            var (first, rest) = SplitAtFirstOperator(condition);
            var newRoot = condition.IsKind(SyntaxKind.LogicalAndExpression)
                ? caret.Root.ReplaceNode(statement, Nest(statement, first, rest))
                : SplitOr(caret, statement, first, rest);

            await caret.ApplyAsync(newRoot, cancellationToken);
            return $"Successfully split the if statement in {filePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error splitting if: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// The leftmost operand of a chain of the same operator, and the chain
    /// without it: <c>a &amp;&amp; b &amp;&amp; c</c> splits into <c>a</c> and
    /// <c>b &amp;&amp; c</c>.
    /// </summary>
    private static (ExpressionSyntax First, ExpressionSyntax Remainder) SplitAtFirstOperator(BinaryExpressionSyntax condition)
    {
        var leftmost = condition;
        while (leftmost.Left is BinaryExpressionSyntax left && left.IsKind(condition.Kind()))
            leftmost = left;

        var rest = leftmost == condition ? condition.Right : condition.ReplaceNode(leftmost, leftmost.Right.WithoutTrivia());
        return (WithoutParentheses(leftmost.Left).WithoutTrivia(), WithoutParentheses(rest).WithoutTrivia());
    }

    /// <summary><c>if (a &amp;&amp; b) S</c> becomes <c>if (a) { if (b) S }</c>.</summary>
    private static IfStatementSyntax Nest(IfStatementSyntax statement, ExpressionSyntax first, ExpressionSyntax rest)
    {
        if (statement.Else is not null)
            throw new McpException("Error: The if statement splits on && and has an else, which both ifs would need");

        var inner = statement.WithCondition(rest).WithoutLeadingTrivia();
        return statement
            .WithCondition(first)
            .WithStatement(CaretDocument.Block(new[] { inner }).WithTriviaFrom(statement.Statement));
    }

    /// <summary>
    /// <c>if (a || b) S</c> becomes <c>if (a) S else if (b) S</c>, keeping any
    /// else on the second. When there is no else and S always jumps away, the
    /// second if need not be an else, so the two ifs follow one another.
    /// </summary>
    private static SyntaxNode SplitOr(CaretDocument caret, IfStatementSyntax statement, ExpressionSyntax first, ExpressionSyntax rest)
    {
        var second = statement.WithCondition(rest).WithoutLeadingTrivia();
        if (statement.Else is null
            && CaretDocument.Siblings(statement) is not null
            && !caret.EndPointIsReachable(CaretDocument.Statements(statement.Statement)))
        {
            var indentation = statement.GetLeadingTrivia().LastOrDefault(t => t.IsKind(SyntaxKind.WhitespaceTrivia));
            second = CaretDocument.WithBlankLineBefore(indentation == default ? second : second.WithLeadingTrivia(indentation));
            return caret.Root.ReplaceNode(statement, new SyntaxNode[] { statement.WithCondition(first), second });
        }

        var elseClause = SyntaxFactory.ElseClause(second).WithAdditionalAnnotations(Formatter.Annotation);
        return caret.Root.ReplaceNode(statement, statement.WithCondition(first).WithElse(elseClause));
    }

    private static ExpressionSyntax WithoutParentheses(ExpressionSyntax expression) =>
        expression is ParenthesizedExpressionSyntax parenthesized ? WithoutParentheses(parenthesized.Expression) : expression;
}
