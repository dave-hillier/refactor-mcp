using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// The bodies of the declarations that can have either a block or an expression body:
/// methods, constructors, operators, accessors and local functions, and properties and
/// indexers, whose expression body stands for a get accessor.
/// </summary>
internal static class MemberBody
{
    /// <summary>The innermost declaration with a body at the position.</summary>
    public static SyntaxNode Find(PositionTarget target) =>
        target.Token.Parent?.AncestorsAndSelf().FirstOrDefault(n => n is AccessorDeclarationSyntax
            or LocalFunctionStatementSyntax
            or BaseMethodDeclarationSyntax
            or PropertyDeclarationSyntax
            or IndexerDeclarationSyntax)
        ?? throw new McpException(
            $"Error: There is no method, property, accessor, constructor, operator or local function at {target.Describe()}");

    public static BlockSyntax? Block(SyntaxNode node) => node switch
    {
        AccessorDeclarationSyntax accessor => accessor.Body,
        LocalFunctionStatementSyntax function => function.Body,
        BaseMethodDeclarationSyntax method => method.Body,
        _ => null,
    };

    public static ArrowExpressionClauseSyntax? Arrow(SyntaxNode node) => node switch
    {
        AccessorDeclarationSyntax accessor => accessor.ExpressionBody,
        LocalFunctionStatementSyntax function => function.ExpressionBody,
        BaseMethodDeclarationSyntax method => method.ExpressionBody,
        PropertyDeclarationSyntax property => property.ExpressionBody,
        IndexerDeclarationSyntax indexer => indexer.ExpressionBody,
        _ => null,
    };

    /// <summary>Replaces the body of a method, accessor or local function.</summary>
    public static SyntaxNode WithBody(SyntaxNode node, BlockSyntax? block, ArrowExpressionClauseSyntax? arrow, SyntaxToken semicolon) => node switch
    {
        AccessorDeclarationSyntax accessor => accessor.WithBody(block).WithExpressionBody(arrow).WithSemicolonToken(semicolon),
        LocalFunctionStatementSyntax function => function.WithBody(block).WithExpressionBody(arrow).WithSemicolonToken(semicolon),
        BaseMethodDeclarationSyntax method => method.WithBody(block).WithExpressionBody(arrow).WithSemicolonToken(semicolon),
        _ => throw new InvalidOperationException($"{node.Kind()} has no body of its own"),
    };

    public static string Describe(SyntaxNode node) => node switch
    {
        AccessorDeclarationSyntax accessor => $"{accessor.Keyword.ValueText} accessor",
        LocalFunctionStatementSyntax function => $"local function '{function.Identifier.ValueText}'",
        MethodDeclarationSyntax method => $"method '{method.Identifier.ValueText}'",
        ConstructorDeclarationSyntax constructor => $"constructor of '{constructor.Identifier.ValueText}'",
        DestructorDeclarationSyntax destructor => $"finalizer of '{destructor.Identifier.ValueText}'",
        OperatorDeclarationSyntax op => $"operator {op.OperatorToken.ValueText}",
        ConversionOperatorDeclarationSyntax conversion => $"conversion to '{conversion.Type}'",
        PropertyDeclarationSyntax property => $"property '{property.Identifier.ValueText}'",
        IndexerDeclarationSyntax => "indexer",
        _ => node.Kind().ToString(),
    };

    /// <summary>The line ending the node's file uses, for trivia added to it.</summary>
    public static SyntaxTrivia EndOfLine(SyntaxNode node) =>
        node.SyntaxTree.GetRoot().DescendantTrivia().FirstOrDefault(t => t.IsKind(SyntaxKind.EndOfLineTrivia)) is { RawKind: not 0 } found
            ? found
            : SyntaxFactory.EndOfLine("\n");

    public static bool IsComment(SyntaxTrivia trivia) =>
        trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia);
}
