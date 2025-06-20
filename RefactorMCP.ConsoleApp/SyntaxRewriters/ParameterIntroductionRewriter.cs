using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

internal class ParameterIntroductionRewriter : ExpressionIntroductionRewriter<MethodDeclarationSyntax>
{
    private readonly string _methodName;
    private readonly SyntaxGenerator _generator;

    public ParameterIntroductionRewriter(
        ExpressionSyntax targetExpression,
        string methodName,
        ParameterSyntax parameter,
        IdentifierNameSyntax parameterReference,
        SyntaxGenerator generator)
        : base(targetExpression, parameterReference, parameter, null)
    {
        _methodName = methodName;
        _generator = generator;
    }

    protected override MethodDeclarationSyntax InsertDeclaration(MethodDeclarationSyntax node, SyntaxNode declaration)
    {
        return node.AddParameterListParameters((ParameterSyntax)declaration);
    }

    public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        var visited = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
        if (InvocationHelpers.IsInvocationOf(visited, _methodName))
        {
            visited = AstTransformations.AddArgument(
                visited,
                TargetExpression.WithoutTrivia(),
                _generator);
        }

        return visited;
    }

    public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var visited = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;
        bool shouldInsert = node.Identifier.ValueText == _methodName;
        return MaybeInsertDeclaration(node, visited, shouldInsert);
    }
}
