using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

internal class CalledMethodCollector : CSharpSyntaxWalker
{
    private readonly HashSet<string> _methodNames;
    public HashSet<string> CalledMethods { get; } = new();

    public CalledMethodCollector(HashSet<string> methodNames)
    {
        _methodNames = methodNames;
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (node.Expression is IdentifierNameSyntax id && _methodNames.Contains(id.Identifier.ValueText))
        {
            CalledMethods.Add(id.Identifier.ValueText);
        }
        else if (node.Expression is MemberAccessExpressionSyntax member &&
                 member.Expression is ThisExpressionSyntax &&
                 member.Name is IdentifierNameSyntax id2 &&
                 _methodNames.Contains(id2.Identifier.ValueText))
        {
            CalledMethods.Add(id2.Identifier.ValueText);
        }
        base.VisitInvocationExpression(node);
    }

    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        if (_methodNames.Contains(node.Identifier.ValueText))
        {
            var parent = node.Parent;
            if (parent is not InvocationExpressionSyntax &&
                (parent is not MemberAccessExpressionSyntax ||
                 (parent is MemberAccessExpressionSyntax ma && ma.Expression is ThisExpressionSyntax)))
            {
                CalledMethods.Add(node.Identifier.ValueText);
            }
        }
        base.VisitIdentifierName(node);
    }
}
