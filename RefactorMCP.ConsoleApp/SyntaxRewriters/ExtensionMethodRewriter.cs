using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;

internal class ExtensionMethodRewriter : CSharpSyntaxRewriter
{
    private readonly string _parameterName;
    private readonly string _parameterType;
    private readonly SemanticModel? _semanticModel;
    private readonly INamedTypeSymbol? _typeSymbol;
    private readonly HashSet<string>? _knownMembers;

    public ExtensionMethodRewriter(string parameterName, string parameterType, SemanticModel semanticModel, INamedTypeSymbol typeSymbol)
    {
        _parameterName = parameterName;
        _parameterType = parameterType;
        _semanticModel = semanticModel;
        _typeSymbol = typeSymbol;
    }

    public ExtensionMethodRewriter(string parameterName, string parameterType, HashSet<string> knownMembers)
    {
        _parameterName = parameterName;
        _parameterType = parameterType;
        _knownMembers = knownMembers;
    }

    public MethodDeclarationSyntax Rewrite(MethodDeclarationSyntax method)
    {
        return (MethodDeclarationSyntax)Visit(method)!;
    }

    public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var thisParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier(_parameterName))
            .WithType(SyntaxFactory.ParseTypeName(_parameterType))
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.ThisKeyword));

        var parameters = node.ParameterList.Parameters.Insert(0, thisParam);
        var updated = node.WithParameterList(node.ParameterList.WithParameters(parameters));
        updated = AstTransformations.EnsureStaticModifier(updated);

        // Remove explicit interface specifier when converting to an extension method
        if (updated.ExplicitInterfaceSpecifier != null)
        {
            updated = updated.WithExplicitInterfaceSpecifier(null)
                .WithIdentifier(updated.Identifier.WithoutTrivia())
                .WithTriviaFrom(updated);
        }

        return base.VisitMethodDeclaration(updated)!;
    }

    public override SyntaxNode VisitThisExpression(ThisExpressionSyntax node)
    {
        return SyntaxFactory.IdentifierName(_parameterName).WithTriviaFrom(node);
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        bool qualify = false;

        if (_semanticModel != null)
        {
            var sym = _semanticModel.GetSymbolInfo(node).Symbol;
            if (sym is IFieldSymbol or IPropertySymbol or IMethodSymbol &&
                SymbolEqualityComparer.Default.Equals(sym.ContainingType, _typeSymbol) &&
                !sym.IsStatic && node.Parent is not MemberAccessExpressionSyntax)
            {
                qualify = true;
            }
        }
        else if (_knownMembers != null &&
                 _knownMembers.Contains(node.Identifier.ValueText) &&
                 node.Parent is not MemberAccessExpressionSyntax)
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
}

