using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

internal class StaticConversionRewriter
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
        SyntaxNode node = method;

        if (_instanceParameterName != null)
        {
            var qualifier = new InstanceMemberQualifierRewriter(
                _instanceParameterName,
                _semanticModel,
                _typeSymbol,
                _knownInstanceMembers);
            node = qualifier.Visit(node)!;
        }

        if ((_symbolRenameMap != null && _symbolRenameMap.Count > 0) ||
            (_nameRenameMap != null && _nameRenameMap.Count > 0))
        {
            var renamer = new IdentifierRenameRewriter(
                _semanticModel,
                _symbolRenameMap,
                _nameRenameMap);
            node = renamer.Visit(node)!;
        }

        var result = (MethodDeclarationSyntax)node;
        if (_parameters.Count > 0)
        {
            var newParameters = method.ParameterList.Parameters.InsertRange(0, _parameters);
            result = result.WithParameterList(method.ParameterList.WithParameters(newParameters));
        }
        result = AstTransformations.EnsureStaticModifier(result);

        if (result.ExplicitInterfaceSpecifier != null)
        {
            // Remove explicit interface implementation when converting to static
            result = result.WithExplicitInterfaceSpecifier(null)
                           .WithIdentifier(result.Identifier.WithoutTrivia())
                           .WithTriviaFrom(result);

            if (!result.Modifiers.Any(m =>
                    m.IsKind(SyntaxKind.PublicKeyword) ||
                    m.IsKind(SyntaxKind.InternalKeyword) ||
                    m.IsKind(SyntaxKind.ProtectedKeyword) ||
                    m.IsKind(SyntaxKind.PrivateKeyword)))
            {
                SyntaxToken accessToken = SyntaxFactory.Token(SyntaxKind.PublicKeyword);

                if (_semanticModel != null)
                {
                    var ifaceSymbol = _semanticModel.GetSymbolInfo(method.ExplicitInterfaceSpecifier!.Name).Symbol as INamedTypeSymbol;
                    accessToken = ifaceSymbol?.DeclaredAccessibility switch
                    {
                        Accessibility.Internal => SyntaxFactory.Token(SyntaxKind.InternalKeyword),
                        Accessibility.Private => SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                        Accessibility.Protected => SyntaxFactory.Token(SyntaxKind.ProtectedKeyword),
                        Accessibility.ProtectedAndInternal or Accessibility.ProtectedOrInternal => SyntaxFactory.Token(SyntaxKind.InternalKeyword),
                        _ => SyntaxFactory.Token(SyntaxKind.PublicKeyword)
                    };
                }

                result = result.WithModifiers(result.Modifiers.Insert(0, accessToken));
            }
        }

        return result;
    }
}
