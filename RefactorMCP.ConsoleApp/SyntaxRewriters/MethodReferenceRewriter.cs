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
            // Don't rewrite identifiers inside conditional access expressions (?.member)
            // or when they're already part of member access expressions or invocations
            if (parent is not InvocationExpressionSyntax && 
                parent is not MemberAccessExpressionSyntax && 
                parent is not MemberBindingExpressionSyntax)
            {
                var memberAccess = SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(_parameterName),
                    node.WithoutTrivia());
                return memberAccess.WithTriviaFrom(node);
            }
        }
        return base.VisitIdentifierName(node)!;
    }

    public override SyntaxNode VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        if (node.Expression is ThisExpressionSyntax &&
            node.Name is IdentifierNameSyntax id &&
            _methodNames.Contains(id.Identifier.ValueText) &&
            node.Parent is not InvocationExpressionSyntax)
        {
            var updated = node.WithExpression(SyntaxFactory.IdentifierName(_parameterName));
            return base.VisitMemberAccessExpression(updated)!;
        }
        return base.VisitMemberAccessExpression(node)!;
    }

    public override SyntaxNode? VisitConditionalAccessExpression(ConditionalAccessExpressionSyntax node)
    {
        // Handle cases like this.method?.Something
        // We need to rewrite the expression before the ?. but leave the binding expression alone
        var rewrittenExpression = (ExpressionSyntax?)Visit(node.Expression);
        if (rewrittenExpression != node.Expression)
        {
            return node.WithExpression(rewrittenExpression);
        }
        return base.VisitConditionalAccessExpression(node);
    }

    public override SyntaxNode? VisitNameColon(NameColonSyntax node)
    {
        // Override to handle named arguments manually and avoid casting issues
        // Don't call base.VisitNameColon as it has assumptions about syntax structure
        // Just return the node unchanged (parameter names in named args should not be transformed)
        return node;
    }
}
