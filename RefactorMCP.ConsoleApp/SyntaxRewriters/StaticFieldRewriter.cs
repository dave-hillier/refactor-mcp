using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;

internal class StaticFieldRewriter : CSharpSyntaxRewriter
{
    private readonly HashSet<string> _staticFieldNames;
    private readonly string _sourceClassName;

    public StaticFieldRewriter(HashSet<string> staticFieldNames, string sourceClassName)
    {
        _staticFieldNames = staticFieldNames;
        _sourceClassName = sourceClassName;
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        var parent = node.Parent;
        if (parent is ParameterSyntax or TypeSyntax)
            return base.VisitIdentifierName(node);

        if (_staticFieldNames.Contains(node.Identifier.ValueText))
        {
            if (parent is not MemberAccessExpressionSyntax ||
                (parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Name == node))
            {
                return SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(_sourceClassName),
                    node);
            }
        }

        return base.VisitIdentifierName(node);
    }
}

