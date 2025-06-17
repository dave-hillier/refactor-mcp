using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

internal class IdentifierRenameRewriter : CSharpSyntaxRewriter
{
    private readonly SemanticModel? _semanticModel;
    private readonly Dictionary<ISymbol, string>? _symbolMap;
    private readonly Dictionary<string, string>? _nameMap;

    public IdentifierRenameRewriter(
        SemanticModel? semanticModel = null,
        Dictionary<ISymbol, string>? symbolMap = null,
        Dictionary<string, string>? nameMap = null)
    {
        _semanticModel = semanticModel;
        _symbolMap = symbolMap;
        _nameMap = nameMap;
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        if (_semanticModel != null && _symbolMap != null)
        {
            var sym = _semanticModel.GetSymbolInfo(node).Symbol;
            if (sym != null && _symbolMap.TryGetValue(sym, out var newName))
                return SyntaxFactory.IdentifierName(newName).WithTriviaFrom(node);
        }

        if (_nameMap != null && _nameMap.TryGetValue(node.Identifier.ValueText, out var name))
            return SyntaxFactory.IdentifierName(name).WithTriviaFrom(node);

        return base.VisitIdentifierName(node);
    }
}
