using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace RefactorMCP.ConsoleApp.SyntaxWalkers
{
    internal class MethodMetricsWalker : CSharpSyntaxWalker
    {
        private readonly SemanticModel? _model;
        public List<string> Suggestions { get; } = new();

        public MethodMetricsWalker(SemanticModel? model)
        {
            _model = model;
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            base.VisitMethodDeclaration(node);

            var span = node.GetLocation().GetLineSpan();
            var lines = span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
            if (lines > 30)
                Suggestions.Add($"Method '{node.Identifier}' is {lines} lines long -> consider extract-method");

            var parameters = node.ParameterList.Parameters.Count;
            if (parameters >= 5)
                Suggestions.Add($"Method '{node.Identifier}' has {parameters} parameters -> consider introducing parameter object");

            if (_model != null && !node.Modifiers.Any(SyntaxKind.StaticKeyword))
            {
                var accessesInstance = node.DescendantNodes().Any(n => n is ThisExpressionSyntax ||
                    n is MemberAccessExpressionSyntax ma && _model.GetSymbolInfo(ma).Symbol is { IsStatic: false });
                if (!accessesInstance)
                    Suggestions.Add($"Method '{node.Identifier}' does not access instance state -> make-static");
            }
        }
    }
}
