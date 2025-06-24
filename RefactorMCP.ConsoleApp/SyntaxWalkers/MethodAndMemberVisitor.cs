using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace RefactorMCP.ConsoleApp.SyntaxWalkers
{
    public class MethodAndMemberVisitor : CSharpSyntaxWalker
    {
        public class MethodInfo
        {
            public bool IsStatic { get; set; }
        }

        public class MemberInfo
        {
            public string Type { get; set; } = string.Empty; // "field" or "property"
        }

        public Dictionary<string, MethodInfo> Methods { get; } = new();
        public Dictionary<string, MemberInfo> Members { get; } = new();

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var methodName = node.Identifier.ValueText;
            if (!Methods.ContainsKey(methodName))
            {
                Methods[methodName] = new MethodInfo
                {
                    IsStatic = node.Modifiers.Any(SyntaxKind.StaticKeyword)
                };
            }
        }

        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            foreach (var variable in node.Declaration.Variables)
            {
                var fieldName = variable.Identifier.ValueText;
                if (!Members.ContainsKey(fieldName))
                {
                    Members[fieldName] = new MemberInfo { Type = "field" };
                }
            }
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            var propertyName = node.Identifier.ValueText;
            if (!Members.ContainsKey(propertyName))
            {
                Members[propertyName] = new MemberInfo { Type = "property" };
            }
        }
    }
}
