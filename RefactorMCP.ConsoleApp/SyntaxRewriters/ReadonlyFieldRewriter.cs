using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

internal class ReadonlyFieldRewriter : CSharpSyntaxRewriter
{
    private readonly string _fieldName;

    public ReadonlyFieldRewriter(string fieldName)
    {
        _fieldName = fieldName;
    }

    /// <summary>
    /// Adds <c>readonly</c> to the field's declaration. Its initialiser stays
    /// where it is: a readonly field may keep one, and moving it into the
    /// constructors would change when it runs.
    /// </summary>
    public override SyntaxNode? VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        if (!node.Declaration.Variables.Any(v => v.Identifier.ValueText == _fieldName))
            return base.VisitFieldDeclaration(node);

        if (node.Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword)))
            return node;

        return node.WithModifiers(node.Modifiers.Add(SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)));
    }
}
