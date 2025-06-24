using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
namespace RefactorMCP.ConsoleApp.SyntaxWalkers
{

    internal abstract class NameCollectorWalker : CSharpSyntaxWalker
    {
        public HashSet<string> Names { get; } = new();

        protected void Add(string name) => Names.Add(name);
    }
}
