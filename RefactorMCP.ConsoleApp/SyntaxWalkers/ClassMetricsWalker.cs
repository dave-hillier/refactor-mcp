using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace RefactorMCP.ConsoleApp.SyntaxWalkers
{
    internal class ClassMetricsWalker : CSharpSyntaxWalker
    {
        public List<string> Suggestions { get; } = new();

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            base.VisitClassDeclaration(node);

            var members = node.Members.Count;
            var span = node.GetLocation().GetLineSpan();
            var lines = span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
            if (members > 15 || lines > 300)
                Suggestions.Add($"Class '{node.Identifier}' is large ({members} members) -> consider splitting or move-method");
        }
    }
}
