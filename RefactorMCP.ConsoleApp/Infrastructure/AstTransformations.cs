using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System.Linq;

internal static class AstTransformations
{

    internal static MethodDeclarationSyntax EnsureStaticModifier(MethodDeclarationSyntax method)
    {
        var modifiers = method.Modifiers;
        if (!modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
            modifiers = modifiers.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));
        return method.WithModifiers(modifiers);
    }

}
