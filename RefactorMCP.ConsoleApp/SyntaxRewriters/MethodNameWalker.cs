using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

internal class MethodNameWalker : CSharpSyntaxWalker
{
    public HashSet<string> Names { get; } = new();

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        Names.Add(node.Identifier.ValueText);
        base.VisitMethodDeclaration(node);
    }
}
