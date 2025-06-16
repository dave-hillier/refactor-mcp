using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;

internal class StaticConversionRewriter : CSharpSyntaxRewriter
{
    private readonly List<ParameterSyntax> _parameters;
    private readonly string? _instanceParameterName;
    private readonly HashSet<string>? _knownInstanceMembers;
    private readonly SemanticModel? _semanticModel;
    private readonly INamedTypeSymbol? _typeSymbol;
    private readonly Dictionary<ISymbol, string>? _symbolRenameMap;
    private readonly Dictionary<string, string>? _nameRenameMap;

    public StaticConversionRewriter(
        IEnumerable<(string Name, string Type)> parameters,
        string? instanceParameterName = null,
        HashSet<string>? knownInstanceMembers = null,
        SemanticModel? semanticModel = null,
        INamedTypeSymbol? typeSymbol = null,
        Dictionary<ISymbol, string>? symbolRenameMap = null,
        Dictionary<string, string>? nameRenameMap = null)
    {
        _parameters = parameters
            .Select(p => SyntaxFactory.Parameter(SyntaxFactory.Identifier(p.Name))
                .WithType(SyntaxFactory.ParseTypeName(p.Type)))
            .ToList();
        _instanceParameterName = instanceParameterName;
        _knownInstanceMembers = knownInstanceMembers;
        _semanticModel = semanticModel;
        _typeSymbol = typeSymbol;
        _symbolRenameMap = symbolRenameMap;
        _nameRenameMap = nameRenameMap;
    }

    public MethodDeclarationSyntax Rewrite(MethodDeclarationSyntax method)
    {
        var visited = (MethodDeclarationSyntax)Visit(method)!;
        if (_parameters.Count > 0)
        {
            var newParameters = method.ParameterList.Parameters.InsertRange(0, _parameters);
            visited = visited.WithParameterList(method.ParameterList.WithParameters(newParameters));
        }
        visited = AstTransformations.EnsureStaticModifier(visited);

        // If the original method was an explicit interface implementation, drop
        // the interface specifier so the transformed static method compiles in
        // the new location.
        if (visited.ExplicitInterfaceSpecifier != null)
        {
            visited = visited.WithExplicitInterfaceSpecifier(null)
                             .WithIdentifier(SyntaxFactory.Identifier(visited.Identifier.ValueText));
        }

        return visited;
    }

    public override SyntaxNode VisitThisExpression(ThisExpressionSyntax node)
    {
        if (_instanceParameterName != null)
            return SyntaxFactory.IdentifierName(_instanceParameterName).WithTriviaFrom(node);
        return base.VisitThisExpression(node);
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        if (_semanticModel != null && _symbolRenameMap != null &&
            _semanticModel.GetSymbolInfo(node).Symbol is ISymbol sym &&
            _symbolRenameMap.TryGetValue(sym, out var newName))
        {
            return SyntaxFactory.IdentifierName(newName).WithTriviaFrom(node);
        }

        if (_nameRenameMap != null &&
            _nameRenameMap.TryGetValue(node.Identifier.ValueText, out var n))
        {
            return SyntaxFactory.IdentifierName(n).WithTriviaFrom(node);
        }

        bool qualify = false;
        if (_instanceParameterName != null)
        {
            if (_semanticModel != null && _typeSymbol != null)
            {
                var s = _semanticModel.GetSymbolInfo(node).Symbol;
                if (s is IFieldSymbol or IPropertySymbol or IMethodSymbol &&
                    SymbolEqualityComparer.Default.Equals(s.ContainingType, _typeSymbol) &&
                    !s.IsStatic && node.Parent is not MemberAccessExpressionSyntax)
                {
                    qualify = true;
                }
            }
            else if (_knownInstanceMembers != null &&
                     _knownInstanceMembers.Contains(node.Identifier.ValueText) &&
                     node.Parent is not MemberAccessExpressionSyntax)
            {
                qualify = true;
            }
        }

        if (qualify)
        {
            return SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(_instanceParameterName!),
                    node.WithoutTrivia())
                .WithTriviaFrom(node);
        }

        return base.VisitIdentifierName(node);
    }
}

