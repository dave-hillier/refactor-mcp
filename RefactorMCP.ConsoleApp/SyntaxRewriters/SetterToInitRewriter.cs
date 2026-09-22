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

        // Everything the setter carried comes along: an access modifier such as
        // `private set;` or `protected set;` is part of the property's contract,
        // and an attribute or a body belongs to the accessor being converted.
        //
        // The kind has to be set through AccessorDeclaration(kind) rather than by
        // swapping the keyword on the setter, which would leave a set-accessor
        // node that happens to print `init`.
        var initAccessor = SyntaxFactory.AccessorDeclaration(SyntaxKind.InitAccessorDeclaration)
            .WithAttributeLists(setter.AttributeLists)
            .WithModifiers(setter.Modifiers)
            .WithKeyword(SyntaxFactory.Token(setter.Keyword.LeadingTrivia, SyntaxKind.InitKeyword, setter.Keyword.TrailingTrivia))
            .WithBody(setter.Body)
            .WithExpressionBody(setter.ExpressionBody)
            .WithSemicolonToken(setter.SemicolonToken);

        var newAccessorList = node.AccessorList!.ReplaceNode(setter, initAccessor);
        return node.WithAccessorList(newAccessorList);
    }
}

