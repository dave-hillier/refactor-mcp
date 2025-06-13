using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

internal class StaticFieldChecker : CSharpSyntaxWalker
{
    private readonly HashSet<string> _staticFieldNames;
    public bool HasStaticFieldReferences { get; private set; }

    public StaticFieldChecker(HashSet<string> staticFieldNames)
    {
        _staticFieldNames = staticFieldNames;
    }

    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        var parent = node.Parent;
        if (parent is ParameterSyntax || parent is TypeSyntax)
        {
            base.VisitIdentifierName(node);
            return;
        }

        if (_staticFieldNames.Contains(node.Identifier.ValueText))
        {
            if (parent is not MemberAccessExpressionSyntax ||
                (parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Name == node))
            {
                HasStaticFieldReferences = true;
            }
        }

        base.VisitIdentifierName(node);
    }
}

