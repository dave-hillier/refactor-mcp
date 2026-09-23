using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace RefactorMCP.ConsoleApp.Tools.Generators;

/// <summary>
/// Turning an expression-bodied method into a block, for generators that add
/// statements to a body.
/// </summary>
internal static class BlockBodies
{
    /// <summary>
    /// Gives an expression-bodied declaration a block body holding the
    /// expression as a statement, returned unless the method returns nothing.
    /// </summary>
    public static async Task<(Document Document, BaseMethodDeclarationSyntax Declaration)> ConvertAsync(
        Document document,
        BaseMethodDeclarationSyntax declaration,
        IMethodSymbol method,
        CancellationToken cancellationToken)
    {
        var expression = declaration.ExpressionBody!.Expression.WithoutTrivia();
        StatementSyntax statement = ReturnsNothing(method)
            ? SyntaxFactory.ExpressionStatement(expression)
            : SyntaxFactory.ReturnStatement(expression);
        var mark = new SyntaxAnnotation();
        var converted = declaration
            .WithExpressionBody(null)
            .WithSemicolonToken(default)
            .WithParameterList(declaration.ParameterList.WithoutTrailingTrivia())
            .WithBody(SyntaxFactory.Block(statement).WithTrailingTrivia(declaration.SemicolonToken.TrailingTrivia))
            .WithAdditionalAnnotations(Formatter.Annotation, mark);

        var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
        document = document.WithSyntaxRoot(root.ReplaceNode(declaration, converted));
        document = await Formatter.FormatAsync(document, Formatter.Annotation, cancellationToken: cancellationToken);
        root = (await document.GetSyntaxRootAsync(cancellationToken))!;
        return (document, (BaseMethodDeclarationSyntax)root.GetAnnotatedNodes(mark).Single());
    }

    public static bool ReturnsNothing(IMethodSymbol method) =>
        method.ReturnsVoid
        || method.MethodKind == MethodKind.Constructor
        || (method.IsAsync && method.ReturnType is INamedTypeSymbol { Arity: 0, Name: "Task" or "ValueTask" });

    /// <summary>The whitespace a token's line starts with.</summary>
    public static string IndentationOf(SyntaxToken token)
    {
        var line = token.SyntaxTree!.GetText().Lines.GetLineFromPosition(token.SpanStart).ToString();
        return line[..(line.Length - line.TrimStart().Length)];
    }
}
