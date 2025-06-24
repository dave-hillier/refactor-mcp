using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace RefactorMCP.ConsoleApp.SyntaxWalkers
{
    internal class UseInterfaceWalker : CSharpSyntaxWalker
    {
        private readonly SemanticModel? _model;
        public List<string> Suggestions { get; } = new();

        public UseInterfaceWalker(SemanticModel? model)
        {
            _model = model;
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            base.VisitMethodDeclaration(node);
            if (_model == null) return;

            foreach (var param in node.ParameterList.Parameters)
            {
                if (param.Type == null) continue;
                var typeInfo = _model.GetTypeInfo(param.Type);
                if (typeInfo.Type is not INamedTypeSymbol named || named.TypeKind != TypeKind.Class)
                    continue;
                var interfaces = named.AllInterfaces;
                if (interfaces.Length == 0) continue;

                var collector = new ParameterMemberCollector(_model, param.Identifier.ValueText);
                collector.Visit(node);
                if (collector.Members.Count == 0) continue;

                foreach (var iface in interfaces)
                {
                    if (collector.Members.All(m => iface.GetMembers(m.Name).Any()))
                    {
                        Suggestions.Add($"Parameter '{param.Identifier.ValueText}' in method '{node.Identifier.ValueText}' only uses members of interface '{iface.Name}' -> use-interface");
                        break;
                    }
                }
            }
        }

        private class ParameterMemberCollector : CSharpSyntaxWalker
        {
            private readonly SemanticModel _model;
            private readonly string _name;
            public List<ISymbol> Members { get; } = new();

            public ParameterMemberCollector(SemanticModel model, string name)
            {
                _model = model;
                _name = name;
            }

            public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
            {
                if (node.Expression is IdentifierNameSyntax id && id.Identifier.ValueText == _name)
                {
                    var symbol = _model.GetSymbolInfo(node).Symbol;
                    if (symbol != null)
                        Members.Add(symbol);
                }
                base.VisitMemberAccessExpression(node);
            }
        }
    }
}
