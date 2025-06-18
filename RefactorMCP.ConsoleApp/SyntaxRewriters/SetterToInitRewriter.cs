using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;

internal class SetterToInitRewriter : CSharpSyntaxRewriter
{
    private readonly string _propertyName;
    public SetterToInitRewriter(string propertyName)
    {
        _propertyName = propertyName;
    }

    public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        if (node.Identifier.ValueText != _propertyName)
            return base.VisitPropertyDeclaration(node);

        var setter = node.AccessorList?.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.SetAccessorDeclaration));
        if (setter == null)
            return base.VisitPropertyDeclaration(node);

        var initAccessor = SyntaxFactory.AccessorDeclaration(SyntaxKind.InitAccessorDeclaration)
            .WithSemicolonToken(setter.SemicolonToken);
        var newAccessorList = node.AccessorList!.ReplaceNode(setter, initAccessor);
        return node.WithAccessorList(newAccessorList);
    }
}

