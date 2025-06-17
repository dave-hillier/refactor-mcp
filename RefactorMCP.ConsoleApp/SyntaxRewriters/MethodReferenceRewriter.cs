using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

internal class MethodReferenceRewriter : CSharpSyntaxRewriter
{
    private readonly HashSet<string> _methodNames;
    private readonly string _parameterName;

    public MethodReferenceRewriter(HashSet<string> methodNames, string parameterName)
    {
        _methodNames = methodNames;
        _parameterName = parameterName;
    }

    public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
    {
        if (_methodNames.Contains(node.Identifier.ValueText))
        {
            var parent = node.Parent;
            if (parent is not InvocationExpressionSyntax && parent is not MemberAccessExpressionSyntax)
            {
                var memberAccess = SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(_parameterName),
                    node.WithoutTrivia());
                return memberAccess.WithTriviaFrom(node);
            }
        }
        return base.VisitIdentifierName(node);
    }

    public override SyntaxNode VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        if (node.Expression is ThisExpressionSyntax &&
            node.Name is IdentifierNameSyntax id &&
            _methodNames.Contains(id.Identifier.ValueText) &&
            node.Parent is not InvocationExpressionSyntax)
        {
            var updated = node.WithExpression(SyntaxFactory.IdentifierName(_parameterName));
            return base.VisitMemberAccessExpression(updated);
        }
        return base.VisitMemberAccessExpression(node);
    }
}
