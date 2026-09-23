using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

/// <summary>
/// Finding the type a type conversion acts on and the facts about its
/// declarations those tools share.
/// </summary>
internal static class TypeDeclarations
{
    /// <summary>The type named <paramref name="name"/> in a file.</summary>
    public static async Task<INamedTypeSymbol> FindTypeAsync(
        Solution solution,
        string filePath,
        string name,
        int? line,
        CancellationToken cancellationToken = default)
    {
        var symbol = await SolutionEdits.FindMemberAsync(solution, filePath, name, line, cancellationToken);
        return symbol as INamedTypeSymbol
            ?? throw new McpException($"Error: '{name}' in {filePath} is not a type");
    }

    /// <summary>Every declaration of a type, in the order the compiler lists its parts.</summary>
    public static async Task<IReadOnlyList<MemberDeclarationSyntax>> DeclarationsAsync(
        INamedTypeSymbol type,
        CancellationToken cancellationToken = default)
    {
        var declarations = new List<MemberDeclarationSyntax>();
        foreach (var reference in type.DeclaringSyntaxReferences)
            declarations.Add((MemberDeclarationSyntax)await reference.GetSyntaxAsync(cancellationToken));

        return declarations;
    }

    /// <summary>The first declaration of a type; for a type that is not partial, its only one.</summary>
    public static async Task<MemberDeclarationSyntax> SingleDeclarationAsync(
        INamedTypeSymbol type,
        CancellationToken cancellationToken = default) =>
        (await DeclarationsAsync(type, cancellationToken))[0];

    /// <summary>
    /// The names of named arguments, in every call of a constructor in the
    /// solution, that must change because its parameters are renamed.
    /// </summary>
    public static async Task<ILookup<DocumentId, (IdentifierNameSyntax Name, string NewName)>> NamedArgumentRenamesAsync(
        Solution solution,
        IMethodSymbol? constructor,
        IReadOnlyDictionary<string, string> renames,
        CancellationToken cancellationToken = default)
    {
        var found = new List<(DocumentId Document, IdentifierNameSyntax Name, string NewName)>();
        if (constructor is null || renames.All(r => r.Key == r.Value))
            return found.ToLookup(f => f.Document, f => (f.Name, f.NewName));

        var references = await SymbolFinder.FindReferencesAsync(constructor, solution, cancellationToken);
        foreach (var location in references.SelectMany(r => r.Locations))
        {
            var root = await location.Document.GetSyntaxRootAsync(cancellationToken);
            var arguments = root!.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true)
                .AncestorsAndSelf()
                .Select(n => n switch
                {
                    BaseObjectCreationExpressionSyntax creation => creation.ArgumentList,
                    ConstructorInitializerSyntax initializer => initializer.ArgumentList,
                    PrimaryConstructorBaseTypeSyntax primary => primary.ArgumentList,
                    _ => null,
                })
                .FirstOrDefault(a => a is not null);

            foreach (var argument in arguments?.Arguments ?? default)
            {
                if (argument.NameColon?.Name is { } name
                    && renames.TryGetValue(name.Identifier.ValueText, out var newName)
                    && newName != name.Identifier.ValueText)
                {
                    found.Add((location.Document.Id, name, newName));
                }
            }
        }

        return found.ToLookup(f => f.Document, f => (f.Name, f.NewName));
    }

    /// <summary>Refuses when the file's language version is older than a feature needs.</summary>
    public static void EnsureLanguageVersion(SyntaxTree tree, LanguageVersion required, string feature)
    {
        var version = ((CSharpParseOptions)tree.Options).LanguageVersion;
        if (version < required)
            throw new McpException(
                $"Error: {feature} need C# {required.ToDisplayString()}, but the project uses C# {version.ToDisplayString()}; raise the language version first");
    }

    /// <summary>Whether nullable annotations are enabled at a position.</summary>
    public static bool AnnotationsEnabled(SemanticModel model, int position) =>
        model.GetNullableContext(position).AnnotationsEnabled();

    /// <summary>
    /// The namespaces of the named types that do not resolve at a position,
    /// so a file that already imports them, globally or not, gets no
    /// duplicate directive.
    /// </summary>
    public static IEnumerable<string> MissingImports(SemanticModel model, int position, params (string Namespace, string Type)[] needed) =>
        needed
            .Where(n => !model.LookupNamespacesAndTypes(position, name: n.Type)
                .Any(s => s.ContainingNamespace?.ToDisplayString() == n.Namespace))
            .Select(n => n.Namespace)
            .Distinct(StringComparer.Ordinal);

    /// <summary>
    /// Adds <c>using</c> directives for namespaces the file does not import yet.
    /// </summary>
    public static CompilationUnitSyntax WithUsings(CompilationUnitSyntax root, IEnumerable<string> namespaces) =>
        WithUsings(root, namespaces.Select(ns => SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(ns))));

    /// <summary>
    /// Adds the <c>using</c> directives the file lacks, after its existing
    /// ones and keeping them sorted when they were.
    /// </summary>
    public static CompilationUnitSyntax WithUsings(CompilationUnitSyntax root, IEnumerable<UsingDirectiveSyntax> directives)
    {
        var missing = directives
            .Select(d => d.WithoutTrivia().NormalizeWhitespace())
            .DistinctBy(d => d.ToString())
            .Where(d => !root.Usings.Any(u => u.WithoutTrivia().NormalizeWhitespace().ToString() == d.ToString()))
            .ToList();
        if (missing.Count == 0)
            return root;

        var newLine = NewLine(root);
        var usings = root.Usings.ToList();
        var keys = usings.Select(Order).ToList();
        var wasSorted = keys.SequenceEqual(keys.OrderBy(k => k, StringComparer.Ordinal));

        foreach (var directive in missing)
        {
            var index = wasSorted
                ? usings.TakeWhile(u => string.CompareOrdinal(Order(u), Order(directive)) < 0).Count()
                : usings.Count;
            usings.Insert(index, directive.WithTrailingTrivia(newLine));
        }

        if (root.Usings.Count == 0)
        {
            // The file's leading trivia, such as a header comment, stays first.
            var first = root.GetFirstToken(includeZeroWidth: true);
            var leading = first.LeadingTrivia;
            root = root.ReplaceToken(first, first.WithLeadingTrivia());
            usings[0] = usings[0].WithLeadingTrivia(leading);
            usings[^1] = usings[^1].WithTrailingTrivia(newLine, newLine);
        }

        return root.WithUsings(SyntaxFactory.List(usings));
    }

    /// <summary>The end of line the file already uses.</summary>
    public static SyntaxTrivia NewLine(SyntaxNode node) =>
        node.DescendantTrivia().FirstOrDefault(t => t.IsKind(SyntaxKind.EndOfLineTrivia)) is { RawKind: not 0 } existing
            ? SyntaxFactory.EndOfLine(existing.ToString())
            : SyntaxFactory.EndOfLine(Environment.NewLine);

    /// <summary>
    /// <c>System</c> namespaces sort first, as the default editor settings
    /// place them, then other namespaces, then static and alias directives.
    /// </summary>
    private static string Order(UsingDirectiveSyntax directive)
    {
        var name = directive.Name?.ToString() ?? "";
        if (directive.Alias is not null)
            return "3" + directive.Alias.Name.Identifier.ValueText;
        if (!directive.StaticKeyword.IsKind(SyntaxKind.None))
            return "2" + name;
        return name == "System" || name.StartsWith("System.", StringComparison.Ordinal) ? "0" + name : "1" + name;
    }
}
