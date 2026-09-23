using ModelContextProtocol.Server;
using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;
using System.ComponentModel;

[McpServerToolType]
public static class TypeAliasTool
{
    // How an alias names its type: from the global namespace, since the file's
    // other usings do not apply to it, but without the global:: prefix.
    private static readonly SymbolDisplayFormat AliasTargetFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)
        .RemoveMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    [McpServerTool, Description("Add a using alias for the type named at a position and use it wherever the file names that type")]
    public static async Task<string> IntroduceTypeAlias(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line of the type's name (1-based)")] int line,
        [Description("Column of the type's name (1-based)")] int column,
        [Description("Name of the alias")] string aliasName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!SyntaxFacts.IsValidIdentifier(aliasName) || SyntaxFacts.GetKeywordKind(aliasName) != SyntaxKind.None)
                throw new McpException($"Error: '{aliasName}' is not a valid identifier");

            var target = await PositionTarget.FindAsync(solutionPath, filePath, line, column, cancellationToken);
            var type = target.Token.Parent is SimpleNameSyntax name
                ? target.Model.GetSymbolInfo(name, cancellationToken).Symbol as INamedTypeSymbol
                : null;
            if (type is null || type.TypeKind == TypeKind.Error)
                throw new McpException($"Error: {target.Describe()} is not on the name of a type");
            if (MentionsTypeParameter(type))
                throw new McpException($"Error: {type.ToDisplayString()} is built from a type parameter, which an alias cannot name");

            var root = (CompilationUnitSyntax)target.Root;
            var uses = Uses(root, target.Model, type, cancellationToken);
            var clash = uses
                .Select(use => target.Model.LookupSymbols(use.SpanStart, name: aliasName).FirstOrDefault())
                .FirstOrDefault(symbol => symbol is not null)
                ?? root.Usings.Where(u => u.Alias?.Name.Identifier.ValueText == aliasName).Select(u => target.Model.GetDeclaredSymbol(u)).FirstOrDefault();
            if (clash is not null)
                throw new McpException($"Error: '{aliasName}' already means something in {Path.GetFileName(filePath)}: {clash.ToDisplayString()}");

            var aliased = root.ReplaceNodes(uses, (original, _) => SyntaxFactory.IdentifierName(aliasName).WithTriviaFrom(original));
            var directive = SyntaxFactory.UsingDirective(
                    SyntaxFactory.NameEquals(aliasName),
                    SyntaxFactory.ParseName(type.ToDisplayString(AliasTargetFormat)))
                .NormalizeWhitespace()
                .WithTrailingTrivia(TypeRefactoringHelpers.EndOfLine(root));

            var solution = target.Document.Project.Solution;
            var changed = solution.WithDocumentSyntaxRoot(target.Document.Id, WithAlias(aliased, directive));
            await SolutionEdits.EnsureCompilesAsync(solution, changed, cancellationToken);
            await TypeRefactoringHelpers.ApplyAsync(solution, changed, cancellationToken);
            return $"Successfully introduced alias '{aliasName}' for {type.ToDisplayString()} in {filePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error introducing type alias: {ex.Message}", ex);
        }
    }

    [McpServerTool, Description("Replace every use of a using alias with the type it names and remove the alias")]
    public static async Task<string> InlineTypeAlias(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file declaring the alias")] string filePath,
        [Description("Name of the alias")] string aliasName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (solution, document) = await TypeRefactoringHelpers.LoadDocumentAsync(solutionPath, filePath, cancellationToken);
            var root = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync(cancellationToken))!;
            var model = (await document.GetSemanticModelAsync(cancellationToken))!;
            var directive = root.DescendantNodes().OfType<UsingDirectiveSyntax>()
                .FirstOrDefault(u => u.Alias?.Name.Identifier.ValueText == aliasName)
                ?? throw new McpException($"Error: {Path.GetFileName(filePath)} declares no alias named '{aliasName}'");

            var alias = model.GetDeclaredSymbol(directive, cancellationToken)!;
            if (alias.Target is not ITypeSymbol type)
                throw new McpException($"Error: '{aliasName}' names a namespace, not a type");

            // A global alias applies to every file of the project.
            var documents = directive.GlobalKeyword.IsKind(SyntaxKind.None)
                ? new[] { document }
                : document.Project.Documents.ToArray();

            // Every file is rewritten against the original, where the alias
            // still binds, before any is simplified, so the simplifier cannot
            // write the alias back.
            var changed = solution;
            foreach (var current in documents)
            {
                var edited = await InlineAsync(current, alias, type, directive, cancellationToken);
                changed = edited is null
                    ? changed.RemoveDocument(current.Id)
                    : changed.WithDocumentSyntaxRoot(current.Id, edited);
            }

            foreach (var current in documents.Where(d => changed.GetDocument(d.Id) is not null))
            {
                var simplified = await Simplifier.ReduceAsync(changed.GetDocument(current.Id)!, Simplifier.Annotation, cancellationToken: cancellationToken);
                changed = simplified.Project.Solution;
            }

            await SolutionEdits.EnsureCompilesAsync(solution, changed, cancellationToken);
            await TypeRefactoringHelpers.ApplyAsync(solution, changed, cancellationToken);
            return $"Successfully inlined alias '{aliasName}' as {type.ToDisplayString()}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error inlining type alias: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// The document's root with each use of the alias written as its type,
    /// marked for simplification, importing the namespaces the type needs, and
    /// without the alias's directive when it is declared here. Null when that
    /// leaves the file empty.
    /// </summary>
    private static async Task<CompilationUnitSyntax?> InlineAsync(
        Document document,
        IAliasSymbol alias,
        ITypeSymbol type,
        UsingDirectiveSyntax directive,
        CancellationToken cancellationToken)
    {
        var root = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync(cancellationToken))!;
        var model = (await document.GetSemanticModelAsync(cancellationToken))!;
        var uses = root.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(n => n.Identifier.ValueText == alias.Name && !n.Ancestors().Any(a => a is UsingDirectiveSyntax))
            .Where(n => SymbolEqualityComparer.Default.Equals(model.GetAliasInfo(n, cancellationToken), alias))
            .ToList();

        var written = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        root = root.ReplaceNodes(uses, (original, _) =>
        {
            var replacement = original.Parent is MemberAccessExpressionSyntax access && access.Expression == original
                ? SyntaxFactory.ParseExpression(written)
                : SyntaxFactory.ParseTypeName(written);
            return replacement.WithTriviaFrom(original).WithAdditionalAnnotations(Simplifier.Annotation);
        });

        if (root.DescendantNodes().OfType<UsingDirectiveSyntax>().FirstOrDefault(u => u.IsEquivalentTo(directive)) is { } declared
            && directive.SyntaxTree.FilePath == document.FilePath)
        {
            root = (CompilationUnitSyntax)DeclarationRemoval.RemoveUsings(root, new[] { declared });
            if (DeclarationRemoval.IsEmpty(root) && root.Usings.Count == 0)
                return null;
        }

        if (uses.Count > 0)
            root = WithImports(root, Namespaces(type));

        return root;
    }

    /// <summary>Imports namespaces, keeping a header comment above the first directive at the top.</summary>
    private static CompilationUnitSyntax WithImports(CompilationUnitSyntax root, IEnumerable<string> namespaces)
    {
        if (root.Usings.Count == 0)
            return TypeRefactoringHelpers.AddUsings(root, namespaces);

        var header = root.Usings[0].GetLeadingTrivia();
        var bare = root.WithUsings(root.Usings.Replace(root.Usings[0], root.Usings[0].WithLeadingTrivia()));
        var imported = TypeRefactoringHelpers.AddUsings(bare, namespaces);
        return imported.WithUsings(imported.Usings.Replace(imported.Usings[0], imported.Usings[0].WithLeadingTrivia(header)));
    }

    /// <summary>
    /// The outermost names in the file, in types and in expressions, that
    /// name <paramref name="type"/>, leaving out using directives.
    /// </summary>
    private static List<SyntaxNode> Uses(CompilationUnitSyntax root, SemanticModel model, INamedTypeSymbol type, CancellationToken cancellationToken)
    {
        var matches = root.DescendantNodes()
            .Where(n => n is NameSyntax or MemberAccessExpressionSyntax)
            .Where(n => !n.Ancestors().Any(a => a is UsingDirectiveSyntax))
            .Where(n => SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(n, cancellationToken).Symbol, type))
            .ToList();

        return matches.Where(m => !m.Ancestors().Any(a => matches.Contains(a))).ToList();
    }

    /// <summary>
    /// Adds the alias after the file's other using directives, or at the top
    /// below any header comment when it has none.
    /// </summary>
    private static CompilationUnitSyntax WithAlias(CompilationUnitSyntax root, UsingDirectiveSyntax directive)
    {
        if (root.Usings.Count > 0)
            return root.WithUsings(root.Usings.Add(directive));

        var first = root.Members.FirstOrDefault()
            ?? throw new McpException("Error: The file declares nothing to use the alias");
        var eol = TypeRefactoringHelpers.EndOfLine(root);
        var updated = root.ReplaceNode(first, first.WithLeadingTrivia(eol));
        return updated.WithUsings(SyntaxFactory.SingletonList(directive.WithLeadingTrivia(first.GetLeadingTrivia())));
    }

    private static bool MentionsTypeParameter(ITypeSymbol type) => type switch
    {
        ITypeParameterSymbol => true,
        IArrayTypeSymbol array => MentionsTypeParameter(array.ElementType),
        INamedTypeSymbol named => named.TypeArguments.Any(MentionsTypeParameter)
            || (named.ContainingType is not null && MentionsTypeParameter(named.ContainingType)),
        _ => false,
    };

    /// <summary>The namespaces of the type and of every type in its type arguments.</summary>
    private static IEnumerable<string> Namespaces(ITypeSymbol type)
    {
        var named = type as INamedTypeSymbol;
        var own = type.ContainingNamespace is { IsGlobalNamespace: false } ns && type.SpecialType == SpecialType.None
            ? new[] { ns.ToDisplayString() }
            : Array.Empty<string>();
        var arguments = named?.TypeArguments.SelectMany(Namespaces) ?? Enumerable.Empty<string>();
        var element = type is IArrayTypeSymbol array ? Namespaces(array.ElementType) : Enumerable.Empty<string>();
        return own.Concat(arguments).Concat(element).Distinct();
    }
}
