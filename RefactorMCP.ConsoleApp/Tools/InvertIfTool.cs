using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Threading;

[McpServerToolType]
public static class InvertIfTool
{
    [McpServerTool, Description("Negate the condition of an if statement and swap its branches, introducing or removing an early return or continue when it has no else")]
    public static async Task<string> InvertIf(
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
            var condition = BooleanNegation.Negate(statement.Condition, b => BooleanNegation.CanFlip(b, caret.Model));

            var newRoot = statement.Else is { } elseClause
                ? caret.Root.ReplaceNode(statement, SwapBranches(statement, elseClause, condition))
                : InvertWithoutElse(caret, statement, condition);

            await caret.ApplyAsync(newRoot, cancellationToken);
            return $"Successfully inverted the if statement in {filePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error inverting if: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// The else branch becomes the then branch and the other way round, each
    /// keeping the trivia of the place it moves to. An else if is braced as
    /// the new then branch, so the new else cannot attach to it.
    /// </summary>
    private static IfStatementSyntax SwapBranches(IfStatementSyntax statement, ElseClauseSyntax elseClause, ExpressionSyntax condition)
    {
        var then = statement.Statement;
        if (elseClause.Statement is IfStatementSyntax elseIf)
        {
            // else if sits on one line; the old then branch goes where it would after an else.
            return statement
                .WithCondition(condition)
                .WithStatement(CaretDocument.Block(new[] { elseIf.WithoutLeadingTrivia() }).WithTriviaFrom(then))
                .WithElse(elseClause
                    .WithElseKeyword(elseClause.ElseKeyword.WithTrailingTrivia(statement.CloseParenToken.TrailingTrivia))
                    .WithStatement(then));
        }

        return statement
            .WithCondition(condition)
            .WithStatement(elseClause.Statement.WithTriviaFrom(then))
            .WithElse(elseClause.WithStatement(then.WithTriviaFrom(elseClause.Statement)));
    }

    /// <summary>
    /// Without an else, the code after the if takes the place of the then
    /// branch. When nothing follows, that is the jump the enclosing method or
    /// loop body makes by ending: <c>return</c> or <c>continue</c>. When code
    /// follows, the then branch must always jump away from it; if it is that
    /// same implicit jump it is dropped, and otherwise the code after it must
    /// always jump away too, so the two can swap places.
    /// </summary>
    private static SyntaxNode InvertWithoutElse(CaretDocument caret, IfStatementSyntax statement, ExpressionSyntax condition)
    {
        if (statement.Parent is not BlockSyntax block)
            throw new McpException("Error: The if statement has no else and is not in a block, so there is no implicit exit to invert into");

        var index = block.Statements.IndexOf(statement);
        var following = block.Statements.Skip(index + 1).ToList();
        var then = CaretDocument.Statements(statement.Statement);
        var exit = ImplicitExit(block, caret.Model);

        List<StatementSyntax> newThen;
        List<StatementSyntax> after;
        if (following.Count == 0)
        {
            newThen = new List<StatementSyntax> { exit ?? throw new McpException(
                "Error: The if statement has no else and ends a block that does not end the method or a loop body, so there is no implicit exit to invert into") };
            after = then.ToList();
        }
        else if (caret.EndPointIsReachable(then))
        {
            throw new McpException("Error: The if statement has no else and is followed by code its branch can fall through to");
        }
        else if (exit is not null && then.Count == 1 && SyntaxFactory.AreEquivalent(then[0], exit))
        {
            newThen = following;
            after = new List<StatementSyntax>();
        }
        else if (!caret.EndPointIsReachable(following))
        {
            newThen = following;
            after = then.ToList();
        }
        else
        {
            throw new McpException("Error: The if statement has no else and is followed by code that does not always jump away, so its branch cannot follow it");
        }

        var inverted = statement
            .WithCondition(condition)
            .WithStatement(CaretDocument.Block(newThen.Select((s, i) => i == 0 ? CaretDocument.WithoutLeadingBlankLines(s) : s))
                .WithTriviaFrom(statement.Statement));

        // Statements that leave the if's block are separated from it by a blank line.
        var moved = after.Select((s, i) =>
            (i == 0 ? CaretDocument.WithBlankLineBefore(s) : s).WithAdditionalAnnotations(Formatter.Annotation));
        var statements = block.Statements.Take(index).Append(inverted).Concat(moved);
        return caret.Root.ReplaceNode(block, block.WithStatements(SyntaxFactory.List(statements)));
    }

    /// <summary>
    /// The jump that running off the end of the block makes, when the block is
    /// the body of a method or function returning nothing, or of a loop.
    /// </summary>
    internal static StatementSyntax? ImplicitExit(BlockSyntax block, SemanticModel model)
    {
        switch (block.Parent)
        {
            case WhileStatementSyntax or DoStatementSyntax or ForStatementSyntax or CommonForEachStatementSyntax:
                return SyntaxFactory.ContinueStatement();
            case ConstructorDeclarationSyntax or DestructorDeclarationSyntax:
            case AccessorDeclarationSyntax accessor when !accessor.IsKind(SyntaxKind.GetAccessorDeclaration):
                return SyntaxFactory.ReturnStatement();
        }

        var function = block.Parent switch
        {
            BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax => model.GetDeclaredSymbol(block.Parent) as IMethodSymbol,
            AnonymousFunctionExpressionSyntax lambda => model.GetSymbolInfo(lambda).Symbol as IMethodSymbol,
            _ => null,
        };
        var isIterator = block
            .DescendantNodes(n => n is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
            .OfType<YieldStatementSyntax>()
            .Any();
        if (function is null || isIterator)
            return null;

        var returnsNothing = function.ReturnsVoid
            || (function.IsAsync && function.ReturnType is INamedTypeSymbol { IsGenericType: false, Name: "Task" or "ValueTask" });
        return returnsNothing ? SyntaxFactory.ReturnStatement() : null;
    }
}
