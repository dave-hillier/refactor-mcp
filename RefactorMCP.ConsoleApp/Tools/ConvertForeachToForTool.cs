using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Threading;

[McpServerToolType]
public static class ConvertForeachToForTool
{
    private static readonly string[] IndexNames = { "i", "j", "k", "index" };

    [McpServerTool, Description("Convert a foreach loop over an indexable collection (array, string, span or list) into a for loop over its indices")]
    public static async Task<string> ConvertForeachToFor(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line of the foreach loop (1-based)")] int line,
        [Description("Column on that line inside the loop (1-based)")] int column,
        [Description("Name of the index variable (optional; defaults to the first of i, j, k and index that is free)")] string? name = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var target = await CaretTarget.FindAsync(solutionPath, filePath, line, column, cancellationToken);
            var loop = target.Enclosing<CommonForEachStatementSyntax>()
                ?? throw new McpException($"Error: {line}:{column} is not in a foreach loop");
            var model = target.Model;
            var collection = loop.Expression;

            var type = model.GetTypeInfo(collection, cancellationToken).Type;
            var indexed = type is null || loop.AwaitKeyword != default ? null : IndexedCollection.For(type, model, loop.SpanStart);
            var info = model.GetForEachStatementInfo(loop);
            if (indexed is null || !SymbolEqualityComparer.Default.Equals(indexed.ElementType, info.ElementType))
                throw new McpException($"Error: '{collection}' cannot be indexed by position, so a for loop cannot walk it");

            if (!ExpressionFacts.IsSimple(collection))
                throw new McpException($"Error: '{collection}' is not a variable, so a for loop's condition would be evaluated on every iteration");

            if (name is not null && !CaretTarget.IsValidName(name))
                throw new McpException($"Error: '{name}' is not a valid name");
            var index = name is not null
                ? (target.NameTaken(loop, name) ? throw new McpException($"Error: '{name}' is already declared or used in the loop's scope; pass another name") : name)
                : IndexNames.FirstOrDefault(n => !target.NameTaken(loop, n))
                  ?? throw new McpException("Error: The names i, j, k and index are all already declared; pass a name for the index");

            ExpressionSyntax element = SyntaxFactory.ElementAccessExpression(
                collection.WithoutTrivia(),
                SyntaxFactory.BracketedArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(SyntaxFactory.IdentifierName(index)))));

            StatementSyntax declaration = loop switch
            {
                ForEachStatementSyntax single => SyntaxFactory.LocalDeclarationStatement(
                    SyntaxFactory.VariableDeclaration(
                        single.Type.WithoutTrivia(),
                        SyntaxFactory.SingletonSeparatedList(SyntaxFactory.VariableDeclarator(
                            single.Identifier.WithoutTrivia(),
                            null,
                            SyntaxFactory.EqualsValueClause(info.ElementConversion.IsImplicit
                                ? element
                                : ExpressionPlacement.Cast(single.Type.WithoutTrivia(), element)))))),
                ForEachVariableStatementSyntax deconstruction => SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        deconstruction.Variable.WithoutTrivia(),
                        element)),
                _ => throw new McpException("Error: Unsupported foreach loop"),
            };
            declaration = declaration.NormalizeWhitespace()
                .WithTrailingTrivia(TypeRefactoringHelpers.EndOfLine(target.Root))
                .WithAdditionalAnnotations(Formatter.Annotation);

            var body = loop.Statement is BlockSyntax block
                ? block.WithStatements(block.Statements.Insert(0, declaration))
                : SyntaxFactory.Block(declaration, loop.Statement.WithoutLeadingTrivia()).WithAdditionalAnnotations(Formatter.Annotation);

            var header = (ForStatementSyntax)SyntaxFactory.ParseStatement(
                $"for (int {index} = 0; {index} < {collection.WithoutTrivia()}.{indexed.CountProperty}; {index}++) {{ }}");
            var forLoop = header
                .WithForKeyword(header.ForKeyword.WithLeadingTrivia(loop.ForEachKeyword.LeadingTrivia))
                .WithCloseParenToken(header.CloseParenToken.WithTrailingTrivia(loop.CloseParenToken.TrailingTrivia))
                .WithStatement(body)
                .WithTrailingTrivia(loop.GetTrailingTrivia());

            await target.ApplyAsync(target.Root.ReplaceNode(loop, forLoop), cancellationToken);
            return $"Successfully converted the foreach loop over '{collection}' to a for loop in {filePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error converting foreach to for: {ex.Message}", ex);
        }
    }
}
