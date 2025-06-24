using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace RefactorMCP.ConsoleApp.SyntaxWalkers
{
    internal class ClassCollectorWalker : TypeCollectorWalker<ClassDeclarationSyntax>
    {
        public Dictionary<string, ClassDeclarationSyntax> Classes => Types;
    }
}
