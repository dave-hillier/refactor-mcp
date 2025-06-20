using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

internal class InstanceMemberNameWalker : CSharpSyntaxWalker
{
    public HashSet<string> Names { get; } = new();

    public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        foreach (var variable in node.Declaration.Variables)
            Names.Add(variable.Identifier.ValueText);
        base.VisitFieldDeclaration(node);
    }

    public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        Names.Add(node.Identifier.ValueText);
        base.VisitPropertyDeclaration(node);
    }
}
