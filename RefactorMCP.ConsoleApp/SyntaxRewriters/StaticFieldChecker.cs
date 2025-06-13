using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;

internal class StaticFieldChecker : CSharpSyntaxRewriter
{
    private readonly HashSet<string> _staticFieldNames;
    public bool HasStaticFieldReferences { get; private set; }

    public StaticFieldChecker(HashSet<string> staticFieldNames)
    {
        _staticFieldNames = staticFieldNames;
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        var parent = node.Parent;
        if (parent is ParameterSyntax || parent is TypeSyntax)
            return base.VisitIdentifierName(node);

        if (_staticFieldNames.Contains(node.Identifier.ValueText))
        {
            if (parent is not MemberAccessExpressionSyntax ||
                (parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Name == node))
            {
                HasStaticFieldReferences = true;
            }
        }

        return base.VisitIdentifierName(node);
    }
}

