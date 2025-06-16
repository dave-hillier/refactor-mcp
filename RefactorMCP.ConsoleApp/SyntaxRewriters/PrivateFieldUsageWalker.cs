using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

internal class PrivateFieldUsageWalker : CSharpSyntaxWalker
{
    private readonly HashSet<string> _privateFieldNames;
    public HashSet<string> UsedFields { get; } = new();

    public PrivateFieldUsageWalker(HashSet<string> privateFieldNames)
    {
        _privateFieldNames = privateFieldNames;
    }

    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        var parent = node.Parent;
        if (_privateFieldNames.Contains(node.Identifier.ValueText))
        {
            if (parent is MemberAccessExpressionSyntax ma && ma.Expression is ThisExpressionSyntax && ma.Name == node)
            {
                UsedFields.Add(node.Identifier.ValueText);
            }
            else if (parent is not MemberAccessExpressionSyntax ||
                     (parent is MemberAccessExpressionSyntax ma2 && ma2.Expression == node))
            {
                UsedFields.Add(node.Identifier.ValueText);
            }
        }

        base.VisitIdentifierName(node);
    }
}
