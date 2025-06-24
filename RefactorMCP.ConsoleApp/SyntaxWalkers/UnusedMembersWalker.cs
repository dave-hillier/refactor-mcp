using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RefactorMCP.ConsoleApp.SyntaxWalkers
{
    internal class UnusedMembersWalker : CSharpSyntaxWalker
    {
        private readonly SemanticModel? _model;
        private readonly Solution? _solution;

        private readonly Dictionary<string, MethodDeclarationSyntax> _methods = new();
        private readonly Dictionary<string, VariableDeclaratorSyntax> _fields = new();
        private readonly Dictionary<string, int> _invocations = new();
        private readonly Dictionary<string, int> _fieldRefs = new();

        public List<string> Suggestions { get; } = new();

        public UnusedMembersWalker(SemanticModel? model = null, Solution? solution = null)
            : base(SyntaxWalkerDepth.Token)
        {
            _model = model;
            _solution = solution;
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            base.VisitMethodDeclaration(node);
            _methods.TryAdd(node.Identifier.ValueText, node);
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            base.VisitInvocationExpression(node);
            if (node.Expression is IdentifierNameSyntax id)
            {
                _invocations.TryGetValue(id.Identifier.ValueText, out var count);
                _invocations[id.Identifier.ValueText] = count + 1;
            }
        }

        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            base.VisitFieldDeclaration(node);
            foreach (var variable in node.Declaration.Variables)
            {
                _fields.TryAdd(variable.Identifier.ValueText, variable);
            }
        }

        public override void VisitIdentifierName(IdentifierNameSyntax node)
        {
            base.VisitIdentifierName(node);
            var name = node.Identifier.ValueText;
            _fieldRefs.TryGetValue(name, out var count);
            _fieldRefs[name] = count + 1;
        }

        public async Task PostProcessAsync()
        {
            if (_model != null && _solution != null)
            {
                await AnalyzeWithModelAsync();
            }
            else
            {
                AnalyzeSingleFile();
            }
        }

        private async Task AnalyzeWithModelAsync()
        {
            foreach (var method in _methods.Values)
            {
                if (method.Modifiers.Any(SyntaxKind.PublicKeyword))
                    continue;

                if (_model!.GetDeclaredSymbol(method) is IMethodSymbol symbol)
                {
                    var refs = await SymbolFinder.FindReferencesAsync(symbol, _solution!);
                    if (refs.All(r => r.Locations.All(l => l.Location.SourceSpan == method.Identifier.Span)))
                        Suggestions.Add($"Method '{method.Identifier}' appears unused -> safe-delete-method");
                }
            }

            foreach (var variable in _fields.Values)
            {
                if (_model!.GetDeclaredSymbol(variable) is IFieldSymbol symbol)
                {
                    var refs = await SymbolFinder.FindReferencesAsync(symbol, _solution!);
                    if (refs.All(r => r.Locations.All(l => l.Location.SourceSpan == variable.Identifier.Span)))
                        Suggestions.Add($"Field '{variable.Identifier}' appears unused -> safe-delete-field");
                }
            }
        }

        private void AnalyzeSingleFile()
        {
            foreach (var (name, method) in _methods)
            {
                if (method.Modifiers.Any(SyntaxKind.PublicKeyword))
                    continue;
                _invocations.TryGetValue(name, out var count);
                if (count == 0)
                    Suggestions.Add($"Method '{name}' appears unused -> safe-delete-method");
            }

            foreach (var (name, _) in _fields)
            {
                _fieldRefs.TryGetValue(name, out var count);
                if (count <= 1)
                    Suggestions.Add($"Field '{name}' appears unused -> safe-delete-field");
            }
        }
    }
}
