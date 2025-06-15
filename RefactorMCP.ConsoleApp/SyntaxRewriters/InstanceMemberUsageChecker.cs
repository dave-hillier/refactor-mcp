using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

internal class InstanceMemberUsageChecker : CSharpSyntaxWalker
{
    private readonly HashSet<string> _knownInstanceMembers;
    public bool HasInstanceMemberUsage { get; private set; }

    public InstanceMemberUsageChecker(HashSet<string> knownInstanceMembers)
    {
        _knownInstanceMembers = knownInstanceMembers;
    }

    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        var parent = node.Parent;
        if (parent is ParameterSyntax || parent is TypeSyntax)
        {
            base.VisitIdentifierName(node);
            return;
        }

        if (_knownInstanceMembers.Contains(node.Identifier.ValueText))
        {
            if (parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Expression == node)
            {
                HasInstanceMemberUsage = true;
            }
            else if (parent is not MemberAccessExpressionSyntax && parent is not InvocationExpressionSyntax)
            {
                HasInstanceMemberUsage = true;
            }
        }

        base.VisitIdentifierName(node);
    }

    public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        if (node.Expression is ThisExpressionSyntax && _knownInstanceMembers.Contains(node.Name.Identifier.ValueText))
        {
            HasInstanceMemberUsage = true;
        }
        base.VisitMemberAccessExpression(node);
    }
}

