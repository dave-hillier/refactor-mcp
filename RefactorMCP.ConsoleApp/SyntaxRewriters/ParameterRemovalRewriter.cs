using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Editing;
using System.Collections.Generic;
using System.Linq;

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
        bool isTarget = false;
        if (visited.Expression is IdentifierNameSyntax id && id.Identifier.ValueText == _methodName)
            isTarget = true;
        else if (visited.Expression is MemberAccessExpressionSyntax ma && ma.Name.Identifier.ValueText == _methodName)
            isTarget = true;

        if (isTarget && _parameterIndex < visited.ArgumentList.Arguments.Count)
        {
            visited = AstTransformations.RemoveArgument(visited, _parameterIndex);
        }

        return visited;
    }
}

