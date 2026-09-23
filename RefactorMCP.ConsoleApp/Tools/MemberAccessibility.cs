using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Extract Method creates private methods. Composites that extract parts of a method
/// its callers must then call give those parts the method's own accessibility.
/// </summary>
internal static class MemberAccessibility
{
    /// <summary>The root with the named methods of the source's class given the source's accessibility.</summary>
    public static SyntaxNode CopyTo(SyntaxNode root, MethodDeclarationSyntax source, IReadOnlyCollection<string> names)
    {
        var accessibility = source.Modifiers.Where(m => SyntaxFacts.IsAccessibilityModifier(m.Kind())).ToList();
        var targets = ((TypeDeclarationSyntax)source.Parent!).Members.OfType<MethodDeclarationSyntax>()
            .Where(m => names.Contains(m.Identifier.ValueText));

        return root.ReplaceNodes(targets, (_, target) =>
        {
            var others = target.Modifiers.Where(m => !SyntaxFacts.IsAccessibilityModifier(m.Kind()));
            var modifiers = accessibility.Select(m => m.WithoutTrivia().WithTrailingTrivia(SyntaxFactory.Space)).Concat(others).ToList();
            if (modifiers.Count == 0)
                return target;

            modifiers[0] = modifiers[0].WithLeadingTrivia(target.Modifiers.FirstOrDefault().LeadingTrivia);
            return target.WithModifiers(SyntaxFactory.TokenList(modifiers));
        });
    }
}
