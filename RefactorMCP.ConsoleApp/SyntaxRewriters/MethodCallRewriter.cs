using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;

internal class MethodCallRewriter : CSharpSyntaxRewriter
{
    private readonly HashSet<string> _classMethodNames;
    private readonly string _parameterName;

    public MethodCallRewriter(HashSet<string> classMethodNames, string parameterName)
    {
        _classMethodNames = classMethodNames;
        _parameterName = parameterName;
    }

    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (node.Expression is IdentifierNameSyntax identifier)
        {
            if (_classMethodNames.Contains(identifier.Identifier.ValueText))
            {
                var memberAccess = SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(_parameterName),
                    identifier);

                return node.WithExpression(memberAccess);
            }
        }
        else if (node.Expression is MemberAccessExpressionSyntax member &&
                 member.Expression is ThisExpressionSyntax &&
                 member.Name is IdentifierNameSyntax id &&
                 _classMethodNames.Contains(id.Identifier.ValueText))
        {
            var updatedMember = member.WithExpression(SyntaxFactory.IdentifierName(_parameterName));
            return node.WithExpression(updatedMember);
        }

        return base.VisitInvocationExpression(node);
    }
}

