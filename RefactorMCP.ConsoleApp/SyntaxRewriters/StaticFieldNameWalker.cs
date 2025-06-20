using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

internal class StaticFieldNameWalker : CSharpSyntaxWalker
{
    public HashSet<string> Names { get; } = new();

    public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        if (node.Modifiers.Any(SyntaxKind.StaticKeyword))
        {
            foreach (var variable in node.Declaration.Variables)
                Names.Add(variable.Identifier.ValueText);
        }
        base.VisitFieldDeclaration(node);
    }
}
