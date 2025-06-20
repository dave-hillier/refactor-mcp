using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

public class ParameterRemovalRewriter : CSharpSyntaxRewriter
{
    private readonly string _methodName;
    private readonly int _parameterIndex;
    private readonly SyntaxGenerator _generator;

    public ParameterRemovalRewriter(string methodName, int parameterIndex, SyntaxGenerator generator)
    {
        _methodName = methodName;
        _parameterIndex = parameterIndex;
        _generator = generator;
    }

    public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var visited = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;
        if (node.Identifier.ValueText == _methodName && _parameterIndex < node.ParameterList.Parameters.Count)
        {
            var newParams = visited.ParameterList.Parameters.RemoveAt(_parameterIndex);
            visited = visited.WithParameterList(visited.ParameterList.WithParameters(newParams));
        }
        return visited;
    }

    public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        var visited = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
        if (InvocationHelpers.IsInvocationOf(visited, _methodName) && _parameterIndex < visited.ArgumentList.Arguments.Count)
        {
            visited = AstTransformations.RemoveArgument(visited, _parameterIndex);
        }

        return visited;
    }
}
