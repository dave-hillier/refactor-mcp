using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace RefactorMCP.ConsoleApp.SyntaxWalkers
{

    internal class StaticFieldNameWalker : NameCollectorWalker
    {
        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            if (node.Modifiers.Any(SyntaxKind.StaticKeyword))
            {
                foreach (var variable in node.Declaration.Variables)
                    Add(variable.Identifier.ValueText);
            }
            base.VisitFieldDeclaration(node);
        }
    }
}
