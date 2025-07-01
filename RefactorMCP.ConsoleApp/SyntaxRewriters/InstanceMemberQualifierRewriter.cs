using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

internal class InstanceMemberQualifierRewriter : CSharpSyntaxRewriter
{
    private readonly string _parameterName;
    private readonly SemanticModel? _semanticModel;
    private readonly INamedTypeSymbol? _typeSymbol;
    private readonly HashSet<string>? _knownMembers;

    public InstanceMemberQualifierRewriter(
        string parameterName,
        SemanticModel? semanticModel = null,
        INamedTypeSymbol? typeSymbol = null,
        HashSet<string>? knownMembers = null)
    {
        _parameterName = parameterName;
        _semanticModel = semanticModel;
        _typeSymbol = typeSymbol;
        _knownMembers = knownMembers;
    }

    public override SyntaxNode VisitThisExpression(ThisExpressionSyntax node)
        => SyntaxFactory.IdentifierName(_parameterName).WithTriviaFrom(node);

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        var parent = node.Parent;
        if (parent is ParameterSyntax or TypeSyntax)
            return base.VisitIdentifierName(node);

        bool qualify = false;
        if (_semanticModel != null && _typeSymbol != null)
        {
            var sym = _semanticModel.GetSymbolInfo(node).Symbol;
            if (sym is IFieldSymbol or IPropertySymbol or IMethodSymbol &&
                !sym.IsStatic && parent is not MemberAccessExpressionSyntax &&
                sym.ContainingType is INamedTypeSymbol ct &&
                IsInTypeHierarchy(ct))
            {
                qualify = true;
            }
        }
        else if (_knownMembers != null &&
                 _knownMembers.Contains(node.Identifier.ValueText) &&
                 parent is not MemberAccessExpressionSyntax)
        {
            qualify = true;
        }

        if (qualify)
        {
            return SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(_parameterName),
                    node.WithoutTrivia())
                .WithTriviaFrom(node);
        }

        return base.VisitIdentifierName(node);
    }

    private bool IsInTypeHierarchy(INamedTypeSymbol containing)
    {
        if (_typeSymbol == null)
            return false;

        var current = _typeSymbol;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, containing))
                return true;
            current = current.BaseType;
        }

        foreach (var iface in _typeSymbol.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(iface, containing))
                return true;
        }

        return false;
    }
}
