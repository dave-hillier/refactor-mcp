using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

internal abstract class IdentifierUsageWalker : CSharpSyntaxWalker
{
    private readonly HashSet<string> _identifiers;

    protected IdentifierUsageWalker(HashSet<string> identifiers)
    {
        _identifiers = identifiers;
    }

    protected bool IsTarget(string name) => _identifiers.Contains(name);

    protected static bool IsParameterOrType(SyntaxNode? node) =>
        node is ParameterSyntax || node is TypeSyntax;

    protected static bool IsThisMember(MemberAccessExpressionSyntax ma, IdentifierNameSyntax node) =>
        ma.Expression is ThisExpressionSyntax && ma.Name == node;

    protected static bool IsMemberExpression(IdentifierNameSyntax node, MemberAccessExpressionSyntax ma) =>
        ma.Expression == node;

    protected static string? GetInvocationName(InvocationExpressionSyntax node)
    {
        return node.Expression switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: IdentifierNameSyntax id } => id.Identifier.ValueText,
            _ => null
        };
    }

    protected virtual bool ShouldRecordIdentifier(IdentifierNameSyntax node)
    {
        var parent = node.Parent;
        if (!IsTarget(node.Identifier.ValueText) || IsParameterOrType(parent))
            return false;

        if (parent is MemberAccessExpressionSyntax memberAccess)
        {
            if (IsThisMember(memberAccess, node) || IsMemberExpression(node, memberAccess))
                return true;
            return false;
        }

        if (parent is InvocationExpressionSyntax)
            return false;

        return true;
    }

    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        if (ShouldRecordIdentifier(node))
            RecordUsage(node.Identifier.ValueText);
        base.VisitIdentifierName(node);
    }

    protected virtual bool TryRecordInvocation(InvocationExpressionSyntax node) => false;

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (!TryRecordInvocation(node))
            base.VisitInvocationExpression(node);
    }

    protected abstract void RecordUsage(string name);
}
