using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace RefactorMCP.ConsoleApp.SyntaxRewriters
{
    internal class ClassCollectorWalker : CSharpSyntaxWalker
    {
        public Dictionary<string, ClassDeclarationSyntax> Classes { get; } = new();

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var name = node.Identifier.ValueText;
            if (!Classes.ContainsKey(name))
                Classes[name] = node;
            base.VisitClassDeclaration(node);
        }
    }
}
