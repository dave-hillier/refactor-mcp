using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

internal class InterfaceCollectorWalker : CSharpSyntaxWalker
{
    public Dictionary<string, InterfaceDeclarationSyntax> Interfaces { get; } = new();

    public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
    {
        var name = node.Identifier.ValueText;
        if (!Interfaces.ContainsKey(name))
            Interfaces[name] = node;
        base.VisitInterfaceDeclaration(node);
    }
}
