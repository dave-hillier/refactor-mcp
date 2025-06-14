using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

internal class InstanceMethodAnalysisWalker : CSharpSyntaxWalker
{
    private readonly HashSet<string> _instanceMembers;
    private readonly HashSet<string> _methodNames;
    private readonly string _methodName;

    public bool UsesInstanceMembers { get; private set; }
    public bool CallsOtherMethods { get; private set; }
    public bool IsRecursive { get; private set; }

    public InstanceMethodAnalysisWalker(HashSet<string> instanceMembers, HashSet<string> methodNames, string methodName)
    {
        _instanceMembers = instanceMembers;
        _methodNames = methodNames;
        _methodName = methodName;
    }

    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        var parent = node.Parent;
        if (parent is ParameterSyntax || parent is TypeSyntax)
        {
            base.VisitIdentifierName(node);
            return;
        }

        if (_instanceMembers.Contains(node.Identifier.ValueText))
        {
            if (parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Expression == node)
            {
                UsesInstanceMembers = true;
            }
            else if (parent is not MemberAccessExpressionSyntax && parent is not InvocationExpressionSyntax)
            {
                UsesInstanceMembers = true;
            }
        }

        base.VisitIdentifierName(node);
    }

    public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        if (node.Expression is ThisExpressionSyntax && _instanceMembers.Contains(node.Name.Identifier.ValueText))
        {
            UsesInstanceMembers = true;
        }
        base.VisitMemberAccessExpression(node);
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        string? calledName = node.Expression switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            MemberAccessExpressionSyntax ma => ma.Name.Identifier.ValueText,
            _ => null
        };

        if (calledName != null)
        {
            if (_methodNames.Contains(calledName))
                CallsOtherMethods = true;
            if (calledName == _methodName)
                IsRecursive = true;
        }

        base.VisitInvocationExpression(node);
    }
}
