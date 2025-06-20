using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

internal class NestedClassNameWalker : CSharpSyntaxWalker
{
    private readonly ClassDeclarationSyntax _origin;
    public HashSet<string> Names { get; } = new();

    public NestedClassNameWalker(ClassDeclarationSyntax origin)
    {
        _origin = origin;
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        if (node.Parent == _origin)
            Names.Add(node.Identifier.ValueText);
        base.VisitClassDeclaration(node);
    }

    public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
    {
        if (node.Parent == _origin)
            Names.Add(node.Identifier.ValueText);
        base.VisitEnumDeclaration(node);
    }
}
