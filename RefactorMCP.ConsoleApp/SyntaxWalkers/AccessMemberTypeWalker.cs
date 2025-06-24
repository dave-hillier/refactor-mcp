using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactorMCP.ConsoleApp.SyntaxWalkers
{
    internal class AccessMemberTypeWalker : CSharpSyntaxWalker
    {
        private readonly string _memberName;
        public string? MemberType { get; private set; }

        public AccessMemberTypeWalker(string memberName)
        {
            _memberName = memberName;
        }

        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            foreach (var variable in node.Declaration.Variables)
            {
                if (variable.Identifier.ValueText == _memberName)
                {
                    MemberType = "field";
                    return;
                }
            }
            base.VisitFieldDeclaration(node);
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            if (node.Identifier.ValueText == _memberName)
            {
                MemberType = "property";
                return;
            }
            base.VisitPropertyDeclaration(node);
        }
    }
}
