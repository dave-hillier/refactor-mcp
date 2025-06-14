using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;

internal class ParameterIntroductionRewriter : CSharpSyntaxRewriter
{
    private readonly ExpressionSyntax _targetExpression;
    private readonly string _methodName;
    private readonly ParameterSyntax _parameter;
    private readonly IdentifierNameSyntax _parameterReference;

    public ParameterIntroductionRewriter(
        ExpressionSyntax targetExpression,
        string methodName,
        ParameterSyntax parameter,
        IdentifierNameSyntax parameterReference)
    {
        _targetExpression = targetExpression;
        _methodName = methodName;
        _parameter = parameter;
        _parameterReference = parameterReference;
    }

    public override SyntaxNode Visit(SyntaxNode? node)
    {
        if (node is ExpressionSyntax expr && SyntaxFactory.AreEquivalent(expr, _targetExpression))
            return _parameterReference;

        return base.Visit(node);
    }

    public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        var visited = (InvocationExpressionSyntax)base.VisitInvocationExpression(node);
        var isTarget =
            (visited.Expression is IdentifierNameSyntax id && id.Identifier.ValueText == _methodName) ||
            (visited.Expression is MemberAccessExpressionSyntax ma && ma.Name.Identifier.ValueText == _methodName);

        if (isTarget)
        {
            var newArgs = visited.ArgumentList.AddArguments(SyntaxFactory.Argument(_targetExpression.WithoutTrivia()));
            visited = visited.WithArgumentList(newArgs);
        }

        return visited;
    }

    public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var visited = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node);
        if (node.Identifier.ValueText == _methodName)
            visited = visited.AddParameterListParameters(_parameter);

        return visited;

    }
}

