using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace RefactorMCP.ConsoleApp.SyntaxWalkers
{
    internal class MethodCollectorWalker : CSharpSyntaxWalker
    {
        private readonly HashSet<string> _targets;
        public Dictionary<string, MethodDeclarationSyntax> Methods { get; } = new();

        public MethodCollectorWalker(HashSet<string> targets)
        {
            _targets = targets;
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (node.Parent is ClassDeclarationSyntax cls)
            {
                var key = $"{cls.Identifier.ValueText}.{node.Identifier.ValueText}";
                if (_targets.Contains(key) && !Methods.ContainsKey(key))
                {
                    Methods[key] = node;
                }
            }
            base.VisitMethodDeclaration(node);
        }
    }
}
