using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Text;
using System.Text;

/// <summary>
/// Plumbing shared by the type and hierarchy refactorings: finding the
/// declarations they act on, resolving type names the caller typed, keeping
/// usings and member layout tidy, and applying a changed solution only when it
/// still compiles.
/// </summary>
internal static class TypeRefactoringHelpers
{
    internal static async Task<(Solution Solution, Document Document)> LoadDocumentAsync(
        string solutionPath,
        string filePath,
        CancellationToken cancellationToken)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var document = RefactoringHelpers.GetDocumentByPath(solution, filePath)
            ?? throw new McpException($"Error: File {filePath} not found in solution");
        return (solution, document);
    }

    /// <summary>A class, record, struct or interface declared in the document, by simple name.</summary>
    internal static async Task<(INamedTypeSymbol Symbol, TypeDeclarationSyntax Declaration)> FindTypeAsync(
        Document document,
        string typeName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var model = await document.GetSemanticModelAsync(cancellationToken);
        var declarations = root!.DescendantNodes().OfType<TypeDeclarationSyntax>()
            .Where(t => t.Identifier.ValueText == typeName)
            .ToList();

        return declarations.Count switch
        {
            0 => throw new McpException($"Error: Type {typeName} not found in {document.FilePath}"),
            > 1 => throw new McpException($"Error: Several types named {typeName} are declared in {document.FilePath}"),
            _ => (model!.GetDeclaredSymbol(declarations[0], cancellationToken)!, declarations[0]),
        };
    }

    /// <summary>
    /// Picks the one declaration a caller meant among several with the same
    /// name, using the 1-based line of its name to tell overloads apart.
    /// </summary>
    internal static T Choose<T>(IReadOnlyList<T> candidates, Func<T, SyntaxToken> identifier, string description, int? line)
        where T : SyntaxNode
    {
        if (line is not null)
        {
            var onLine = candidates
                .Where(c => identifier(c).GetLocation().GetLineSpan().StartLinePosition.Line + 1 == line)
                .ToList();
            if (onLine.Count == 1)
                return onLine[0];
        }

        return candidates.Count switch
        {
            0 => throw new McpException($"Error: {description} not found"),
            1 => candidates[0],
            _ => throw new McpException($"Error: {description} is ambiguous; pass the line of its declaration"),
        };
    }

    /// <summary>
    /// The metadata name of a type named by syntax such as <c>Page&lt;T&gt;</c>:
    /// <c>Page`1</c>.
    /// </summary>
    internal static string MetadataName(SimpleNameSyntax name) =>
        name is GenericNameSyntax generic
            ? $"{generic.Identifier.ValueText}`{generic.TypeArgumentList.Arguments.Count}"
            : name.Identifier.ValueText;

    /// <summary>
    /// Namespaces to import so the unresolved type names in <paramref name="node"/>
    /// bind: each name that binds to nothing is looked up by name and arity
    /// among the types the compilation can see, preferring the solution's own.
    /// Names that match no type, or more than one, are returned as unresolved.
    /// </summary>
    internal static (IReadOnlyList<string> Imports, IReadOnlyList<string> Unresolved) ImportsForUnresolvedTypes(
        SemanticModel model,
        SyntaxNode node)
    {
        var imports = new List<string>();
        var unresolved = new List<string>();
        var names = node.DescendantNodesAndSelf().OfType<SimpleNameSyntax>()
            .Where(n => n.Parent is not QualifiedNameSyntax q || q.Left == n)
            .Where(n => n.Parent is not MemberAccessExpressionSyntax m || m.Expression == n);

        foreach (var name in names)
        {
            var info = model.GetSymbolInfo(name);
            if (info.Symbol is not null || info.CandidateSymbols.Length > 0)
                continue;

            var arity = name is GenericNameSyntax g ? g.TypeArgumentList.Arguments.Count : 0;
            var candidates = TypesNamed(model.Compilation, name.Identifier.ValueText, arity);
            var fromSource = candidates.Where(c => c.Locations.Any(l => l.IsInSource)).ToList();
            var chosen = fromSource.Count > 0 ? fromSource : candidates;

            if (chosen.Count == 1)
                imports.Add(chosen[0].ContainingNamespace.ToDisplayString());
            else
                unresolved.Add(name.ToString());
        }

        return (imports.Distinct().ToList(), unresolved);
    }

    /// <summary>
    /// Binds the type syntax carrying <paramref name="annotation"/>, which holds
    /// a name the caller typed, importing the namespaces it needs to bind.
    /// </summary>
    internal static async Task<(Document Document, ITypeSymbol Type)> ResolveTypeAsync(
        Document document,
        SyntaxAnnotation annotation,
        string typeName,
        CancellationToken cancellationToken)
    {
        var root = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync(cancellationToken))!;
        var model = await document.GetSemanticModelAsync(cancellationToken);
        var (imports, unresolved) = ImportsForUnresolvedTypes(model!, root.GetAnnotatedNodes(annotation).Single());
        if (unresolved.Count > 0)
            throw new McpException($"Error: No type named '{string.Join("', '", unresolved)}' found for {typeName}");

        if (imports.Count > 0)
        {
            document = document.WithSyntaxRoot(AddUsings(root, imports));
            root = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync(cancellationToken))!;
            model = await document.GetSemanticModelAsync(cancellationToken);
        }

        var syntax = (TypeSyntax)root.GetAnnotatedNodes(annotation).Single();
        var type = model!.GetTypeInfo(syntax, cancellationToken).Type;
        if (type is null || type.TypeKind == TypeKind.Error)
            throw new McpException($"Error: No type named '{typeName}' found");

        // A qualified name the usings already cover is written the short way.
        document = document.WithSyntaxRoot(root.ReplaceNode(syntax, syntax.WithAdditionalAnnotations(Simplifier.Annotation)));
        document = await Simplifier.ReduceAsync(document, Simplifier.Annotation, cancellationToken: cancellationToken);
        return (document, type);
    }

    private static List<INamedTypeSymbol> TypesNamed(Compilation compilation, string name, int arity)
    {
        var found = new List<INamedTypeSymbol>();
        var pending = new Stack<INamespaceSymbol>();
        pending.Push(compilation.GlobalNamespace);
        while (pending.Count > 0)
        {
            var ns = pending.Pop();
            foreach (var type in ns.GetTypeMembers(name, arity))
            {
                if (compilation.IsSymbolAccessibleWithin(type, compilation.Assembly))
                    found.Add(type);
            }

            foreach (var child in ns.GetNamespaceMembers())
                pending.Push(child);
        }

        return found;
    }

    /// <summary>
    /// The namespaces a piece of code needs imported where it lands: those of
    /// the types and extension methods it names, less the ones the destination
    /// already sees.
    /// </summary>
    internal static IReadOnlyList<string> NamespacesUsedBy(SyntaxNode node, SemanticModel model)
    {
        var namespaces = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in node.DescendantNodesAndSelf().OfType<SimpleNameSyntax>().Where(n => !n.IsVar))
        {
            var symbol = model.GetSymbolInfo(name).Symbol;
            var owner = symbol switch
            {
                INamedTypeSymbol type when name.Parent is not QualifiedNameSyntax { Right: var r } || r != name => type,
                IMethodSymbol { IsExtensionMethod: true } method => method.ContainingType,
                _ => null,
            };

            if (owner?.ContainingType is null && owner?.ContainingNamespace is { IsGlobalNamespace: false } ns)
                namespaces.Add(ns.ToDisplayString());
        }

        return namespaces.OrderBy(n => n, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Adds using directives for <paramref name="namespaces"/> the file does not
    /// already import and its code does not already sit inside, keeping
    /// <c>System</c> namespaces first as the existing usings are ordered.
    /// </summary>
    internal static CompilationUnitSyntax AddUsings(CompilationUnitSyntax root, IEnumerable<string> namespaces)
    {
        var declared = root.DescendantNodes(n => n is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax)
            .OfType<BaseNamespaceDeclarationSyntax>()
            .Select(n => n.Name.ToString())
            .ToList();
        var missing = namespaces
            .Where(ns => root.Usings.All(u => u.Alias is not null || u.StaticKeyword != default || u.Name?.ToString() != ns))
            .Where(ns => !declared.Any(d => d == ns || d.StartsWith(ns + ".", StringComparison.Ordinal)))
            .Distinct()
            .ToList();
        if (missing.Count == 0)
            return root;

        var eol = EndOfLine(root);
        var usings = root.Usings.ToList();
        var hadUsings = usings.Count > 0;
        foreach (var ns in missing)
        {
            var directive = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(ns))
                .WithUsingKeyword(SyntaxFactory.Token(SyntaxKind.UsingKeyword).WithTrailingTrivia(SyntaxFactory.Space))
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken).WithTrailingTrivia(eol));
            var index = usings.FindIndex(u => u.Alias is null && u.Name is not null && CompareNamespaces(u.Name.ToString(), ns) > 0);
            usings.Insert(index < 0 ? usings.Count : index, directive);
        }

        if (hadUsings)
            return root.WithUsings(SyntaxFactory.List(usings));

        // A file without usings gets them above everything, then a blank line.
        var first = root.Members.FirstOrDefault();
        var updated = root.WithUsings(SyntaxFactory.List(usings));
        if (first is null)
            return updated;

        return updated.ReplaceNode(
            updated.Members.First(),
            first.WithLeadingTrivia(first.GetLeadingTrivia().Insert(0, eol)));
    }

    private static int CompareNamespaces(string left, string right)
    {
        static int Rank(string ns) => ns == "System" || ns.StartsWith("System.", StringComparison.Ordinal) ? 0 : 1;
        var byRank = Rank(left).CompareTo(Rank(right));
        return byRank != 0 ? byRank : string.CompareOrdinal(left, right);
    }

    /// <summary>
    /// Adds a type to a declaration's base list, first (where a base class
    /// goes) or last, creating the list when there is none. What followed the
    /// name, such as the line break before the brace, follows the list.
    /// </summary>
    internal static TypeDeclarationSyntax AddBaseType(TypeDeclarationSyntax declaration, TypeSyntax type, bool first)
    {
        var comma = SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space);
        if (declaration.BaseList is { } list)
        {
            var entry = (BaseTypeSyntax)SyntaxFactory.SimpleBaseType(type);
            if (first)
            {
                return declaration.WithBaseList(list.WithTypes(SyntaxFactory.SeparatedList(
                    list.Types.Prepend(entry),
                    list.Types.GetSeparators().Prepend(comma))));
            }

            var last = list.Types.Last();
            var types = list.Types.Replace(last, last.WithoutTrailingTrivia());
            return declaration.WithBaseList(list.WithTypes(SyntaxFactory.SeparatedList(
                types.Append(entry.WithTrailingTrivia(last.GetTrailingTrivia())),
                types.GetSeparators().Append(comma))));
        }

        var before = declaration.ParameterList?.CloseParenToken
            ?? declaration.TypeParameterList?.GreaterThanToken
            ?? declaration.Identifier;
        var created = SyntaxFactory.BaseList(
                SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(
                    SyntaxFactory.SimpleBaseType(type.WithTrailingTrivia(before.TrailingTrivia))))
            .WithColonToken(SyntaxFactory.Token(SyntaxKind.ColonToken).WithTrailingTrivia(SyntaxFactory.Space));

        return declaration
            .ReplaceToken(before, before.WithTrailingTrivia(SyntaxFactory.Space))
            .WithBaseList(created);
    }

    /// <summary>The line ending a file already uses.</summary>
    internal static SyntaxTrivia EndOfLine(SyntaxNode root) =>
        SyntaxFactory.EndOfLine(root.ToFullString().Contains("\r\n") ? "\r\n" : "\n");

    /// <summary>The line ending the rest of the project uses, for a new file.</summary>
    internal static string EndOfLine(Project project)
    {
        var sample = SourceDocuments(project).FirstOrDefault();
        if (sample is null || !sample.TryGetText(out var text))
            return Environment.NewLine;

        return text.ToString().Contains("\r\n") ? "\r\n" : "\n";
    }

    /// <summary>The project's own source files, leaving out what the build generated under obj/.</summary>
    internal static IEnumerable<Document> SourceDocuments(Project project) =>
        project.Documents.Where(d => d.FilePath is not null
            && !d.FilePath.Replace('\\', '/').Split('/').Any(part => part is "obj" or "bin"));

    /// <summary>
    /// The errors <paramref name="after"/> has that <paramref name="before"/>
    /// did not, compared by code and message so that moved code is not
    /// mistaken for new errors. Only the projects the change touched, and the
    /// projects that depend on them, are compiled.
    /// </summary>
    internal static async Task<IReadOnlyList<Diagnostic>> NewErrorsAsync(
        Solution before,
        Solution after,
        CancellationToken cancellationToken)
    {
        var graph = after.GetProjectDependencyGraph();
        var affected = after.GetChanges(before).GetProjectChanges()
            .Select(c => c.ProjectId)
            .SelectMany(id => graph.GetProjectsThatTransitivelyDependOnThisProject(id).Prepend(id))
            .ToHashSet();

        var existing = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var error in await ErrorsAsync(before, affected, cancellationToken))
            existing[Key(error)] = existing.GetValueOrDefault(Key(error)) + 1;

        var added = new List<Diagnostic>();
        foreach (var error in await ErrorsAsync(after, affected, cancellationToken))
        {
            var key = Key(error);
            if (existing.GetValueOrDefault(key) > 0)
                existing[key]--;
            else
                added.Add(error);
        }

        return added;

        static string Key(Diagnostic d) => $"{d.Id}:{d.GetMessage()}";
    }

    private static async Task<IReadOnlyList<Diagnostic>> ErrorsAsync(
        Solution solution,
        IReadOnlySet<ProjectId> projects,
        CancellationToken cancellationToken)
    {
        var errors = new List<Diagnostic>();
        foreach (var project in solution.Projects.Where(p => projects.Contains(p.Id)))
        {
            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation is not null)
                errors.AddRange(compilation.GetDiagnostics(cancellationToken).Where(d => d.Severity == DiagnosticSeverity.Error));
        }

        return errors;
    }

    /// <summary>
    /// A use whose meaning depends on a type about to change, marked so it can
    /// be found in the changed solution, and the member it bound to before.
    /// </summary>
    internal sealed record Binding(DocumentId Document, SyntaxNode Node, SyntaxAnnotation Mark, ISymbol Bound);

    /// <summary>
    /// The first use that reaches a different member in <paramref name="changed"/>
    /// than it did before, with what it reaches now; null when every use
    /// reaches the same code.
    /// </summary>
    internal static async Task<(Binding Binding, SyntaxNode Node, ISymbol? Now)?> FirstChangedBindingAsync(
        Solution changed,
        IEnumerable<Binding> bindings,
        CancellationToken cancellationToken)
    {
        foreach (var binding in bindings)
        {
            var document = changed.GetDocument(binding.Document)!;
            var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
            var model = (await document.GetSemanticModelAsync(cancellationToken))!;
            var node = root.GetAnnotatedNodes(binding.Mark).Single();
            var now = model.GetSymbolInfo(node, cancellationToken).Symbol;
            if (now is null || !ReachesSameMember(Definition(binding.Bound), Definition(now), model.Compilation))
                return (binding, node, now);
        }

        return null;
    }

    internal static ISymbol Definition(ISymbol symbol) =>
        ((symbol as IMethodSymbol)?.ReducedFrom ?? symbol).OriginalDefinition;

    /// <summary>
    /// Whether <paramref name="after"/> reaches <paramref name="before"/> at
    /// run time: the same member, one it overrides, or an interface member it
    /// implements. <paramref name="compilation"/> is the one
    /// <paramref name="after"/> belongs to.
    /// </summary>
    internal static bool ReachesSameMember(ISymbol before, ISymbol after, Compilation compilation)
    {
        var beforeId = DocumentationCommentId.CreateDeclarationId(before);
        if (beforeId == DocumentationCommentId.CreateDeclarationId(after))
            return true;

        var current = DocumentationCommentId.GetFirstSymbolForDeclarationId(beforeId, compilation);
        if (current is null)
            return false;

        if (after.ContainingType?.TypeKind == TypeKind.Interface)
        {
            var implementation = current.ContainingType.FindImplementationForInterfaceMember(after);
            return SymbolEqualityComparer.Default.Equals(implementation?.OriginalDefinition, current.OriginalDefinition);
        }

        for (var overridden = Overridden(current); overridden is not null; overridden = Overridden(overridden))
        {
            if (SymbolEqualityComparer.Default.Equals(overridden.OriginalDefinition, after))
                return true;
        }

        return false;
    }

    private static ISymbol? Overridden(ISymbol symbol) => symbol switch
    {
        IMethodSymbol method => method.OverriddenMethod,
        IPropertySymbol property => property.OverriddenProperty,
        IEventSymbol @event => @event.OverriddenEvent,
        _ => null,
    };

    internal static string Describe(IEnumerable<Diagnostic> diagnostics) =>
        string.Join("; ", diagnostics.Take(3).Select(d =>
            $"{Path.GetFileName(d.Location.SourceTree?.FilePath)}({d.Location.GetLineSpan().StartLinePosition.Line + 1}): {d.GetMessage()}"));

    /// <summary>
    /// Writes every document <paramref name="after"/> changed, added or removed
    /// relative to <paramref name="before"/>, then makes it the session's
    /// solution so later calls see the refactored code.
    /// </summary>
    internal static async Task ApplyAsync(Solution before, Solution after, CancellationToken cancellationToken)
    {
        foreach (var projectChanges in after.GetChanges(before).GetProjectChanges())
        {
            foreach (var id in projectChanges.GetChangedDocuments().Concat(projectChanges.GetAddedDocuments()))
            {
                var document = after.GetDocument(id)!;
                var text = (await document.GetTextAsync(cancellationToken)).ToString();
                Directory.CreateDirectory(Path.GetDirectoryName(document.FilePath!)!);
                await File.WriteAllTextAsync(document.FilePath!, text, EncodingFor(document.FilePath!), cancellationToken);
                RefactoringHelpers.EvictFileCaches(document.FilePath!);
            }

            foreach (var id in projectChanges.GetRemovedDocuments())
            {
                var path = before.GetDocument(id)!.FilePath!;
                File.Delete(path);
                RefactoringHelpers.EvictFileCaches(path);
            }
        }

        if (!string.IsNullOrEmpty(after.FilePath))
            SessionRegistry.GetOrCreate(after.FilePath).Replace(after);
    }

    /// <summary>
    /// Refuses with <paramref name="failure"/> when the change would add
    /// compile errors, otherwise applies it.
    /// </summary>
    internal static async Task ApplyIfCompilesAsync(
        Solution before,
        Solution after,
        Func<IReadOnlyList<Diagnostic>, string> failure,
        CancellationToken cancellationToken)
    {
        var errors = await NewErrorsAsync(before, after, cancellationToken);
        if (errors.Count > 0)
            throw new McpException(failure(errors));

        await ApplyAsync(before, after, cancellationToken);
    }

    /// <summary>An existing file keeps its byte order mark or lack of one; a new file has none.</summary>
    private static Encoding EncodingFor(string path)
    {
        if (!File.Exists(path))
            return new UTF8Encoding(false);

        var bytes = File.ReadAllBytes(path);
        var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        return hasBom ? new UTF8Encoding(true) : new UTF8Encoding(false);
    }

    /// <summary>The project a new file at <paramref name="path"/> belongs to: the one whose folder most closely contains it.</summary>
    internal static Project ProjectForPath(Solution solution, string path)
    {
        var full = Path.GetFullPath(path);
        return solution.Projects
            .Where(p => p.FilePath is not null
                && full.StartsWith(Path.GetDirectoryName(Path.GetFullPath(p.FilePath))! + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .OrderByDescending(p => p.FilePath!.Length)
            .FirstOrDefault()
            ?? throw new McpException($"Error: No project in the solution contains {path}");
    }

    internal static SourceText Text(string text) => SourceText.From(text, Encoding.UTF8);
}
