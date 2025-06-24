using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
namespace RefactorMCP.ConsoleApp.SyntaxWalkers
{

    internal class InterfaceCollectorWalker : TypeCollectorWalker<InterfaceDeclarationSyntax>
    {
        public Dictionary<string, InterfaceDeclarationSyntax> Interfaces => Types;
    }
}
