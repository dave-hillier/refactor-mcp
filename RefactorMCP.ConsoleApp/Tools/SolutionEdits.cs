using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Finding the declaration a tool acts on, checking a changed solution still
/// compiles, and writing it back to disk.
/// </summary>
internal static class SolutionEdits
{
    /// <summary>
    /// The method or constructor named <paramref name="name"/> in a file. A
    /// constructor is named by its type. <paramref name="line"/>, any line of
    /// the declaration, chooses between overloads.
    /// </summary>
    public static async Task<IMethodSymbol> FindMethodAsync(
        Solution solution,
        string filePath,
        string name,
        int? line,
        CancellationToken cancellationToken = default)
    {
        var symbol = await FindDeclarationAsync(
            solution,
            filePath,
            name,
            line,
            node => node is MethodDeclarationSyntax or ConstructorDeclarationSyntax or LocalFunctionStatementSyntax,
            "method",
            cancellationToken);
        return (IMethodSymbol)symbol;
    }

    /// <summary>The type or member named <paramref name="name"/> in a file.</summary>
    public static Task<ISymbol> FindMemberAsync(
        Solution solution,
        string filePath,
        string name,
        int? line,
        CancellationToken cancellationToken = default) =>
        FindDeclarationAsync(
            solution,
            filePath,
            name,
            line,
            node => node is MemberDeclarationSyntax or VariableDeclaratorSyntax,
            "member",
            cancellationToken);

    private static async Task<ISymbol> FindDeclarationAsync(
        Solution solution,
        string filePath,
        string name,
        int? line,
        Func<SyntaxNode, bool> kind,
        string description,
        CancellationToken cancellationToken)
    {
        var document = RefactoringHelpers.GetDocumentByPath(solution, filePath)
            ?? throw new McpException($"Error: File {filePath} not found in solution");
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var model = await document.GetSemanticModelAsync(cancellationToken);

        var candidates = root!.DescendantNodes()
            .Where(kind)
            .Where(node => DeclaredName(node) == name)
            .Where(node => line is null || SpansLine(node, line.Value))
            .Select(node => (Node: node, Symbol: model!.GetDeclaredSymbol(node, cancellationToken)))
            .Where(candidate => candidate.Symbol is not null)
            .ToList();

        // A line inside a type and one of its members matches both; the member is meant.
        if (line is not null && candidates.Count > 1)
            candidates = candidates.Where(c => !candidates.Any(o => o.Node != c.Node && c.Node.Span.Contains(o.Node.Span))).ToList();

        return candidates.Count switch
        {
            0 => throw new McpException($"Error: No {description} named '{name}' found in {filePath}"),
            1 => candidates[0].Symbol!,
            _ => throw new McpException(
                $"Error: Several {description}s named '{name}' found in {filePath}; pass the line of the one to change"),
        };
    }

    private static string? DeclaredName(SyntaxNode node) => node switch
    {
        MethodDeclarationSyntax method => method.Identifier.ValueText,
        ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
        LocalFunctionStatementSyntax local => local.Identifier.ValueText,
        BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
        DelegateDeclarationSyntax @delegate => @delegate.Identifier.ValueText,
        PropertyDeclarationSyntax property => property.Identifier.ValueText,
        EventDeclarationSyntax @event => @event.Identifier.ValueText,
        VariableDeclaratorSyntax variable when variable.Parent?.Parent is BaseFieldDeclarationSyntax => variable.Identifier.ValueText,
        IndexerDeclarationSyntax => "this",
        _ => null,
    };

    private static bool SpansLine(SyntaxNode node, int line)
    {
        var span = node.GetLocation().GetLineSpan();
        return span.StartLinePosition.Line + 1 <= line && line <= span.EndLinePosition.Line + 1;
    }

    /// <summary>
    /// Finds a symbol again in another version of the solution, such as one
    /// where its declaring document has been annotated or its body edited.
    /// </summary>
    public static async Task<TSymbol> ResolveAsync<TSymbol>(
        Solution solution,
        ProjectId project,
        TSymbol symbol,
        CancellationToken cancellationToken = default)
        where TSymbol : class, ISymbol
    {
        var id = symbol.OriginalDefinition.GetDocumentationCommentId()
            ?? throw new McpException($"Error: '{symbol.Name}' cannot be found again after an edit");
        var compilation = await solution.GetProject(project)!.GetCompilationAsync(cancellationToken);
        return DocumentationCommentId.GetFirstSymbolForDeclarationId(id, compilation!) as TSymbol
            ?? throw new McpException($"Error: '{symbol.Name}' cannot be found again after an edit");
    }

    /// <summary>
    /// Diagnostics the changed solution reports that the original did not, in
    /// every changed project and the projects that depend on them.
    /// </summary>
    public static async Task<IReadOnlyList<Diagnostic>> NewDiagnosticsAsync(
        Solution original,
        Solution changed,
        Func<Diagnostic, bool> include,
        CancellationToken cancellationToken = default)
    {
        var graph = changed.GetProjectDependencyGraph();
        var affected = changed.GetChanges(original).GetProjectChanges()
            .Select(p => p.ProjectId)
            .SelectMany(id => graph.GetProjectsThatTransitivelyDependOnThisProject(id).Append(id))
            .Distinct();

        var added = new List<Diagnostic>();
        foreach (var projectId in affected)
        {
            var before = await DiagnosticsAsync(original.GetProject(projectId), include, cancellationToken);
            var after = await DiagnosticsAsync(changed.GetProject(projectId), include, cancellationToken);
            var counts = before.GroupBy(Key).ToDictionary(g => g.Key, g => g.Count());
            foreach (var group in after.GroupBy(Key))
                added.AddRange(group.Skip(counts.GetValueOrDefault(group.Key)));
        }

        return added;
    }

    /// <summary>Refuses a change that would add compile errors.</summary>
    public static async Task EnsureCompilesAsync(Solution original, Solution changed, CancellationToken cancellationToken = default)
    {
        var errors = await NewDiagnosticsAsync(original, changed, d => d.Severity == DiagnosticSeverity.Error, cancellationToken);
        if (errors.Count > 0)
            throw new McpException($"Error: The change would not compile: {Describe(errors[0])}");
    }

    public static string Describe(Diagnostic diagnostic)
    {
        var position = diagnostic.Location.GetLineSpan();
        var where = position.IsValid
            ? $"{Path.GetFileName(position.Path)}({position.StartLinePosition.Line + 1},{position.StartLinePosition.Character + 1}): "
            : "";
        return $"{where}{diagnostic.Id} {diagnostic.GetMessage()}";
    }

    /// <summary>Writes every changed document to disk and makes the changed solution current.</summary>
    public static async Task WriteAsync(Solution original, Solution changed, CancellationToken cancellationToken = default)
    {
        foreach (var projectChange in changed.GetChanges(original).GetProjectChanges())
        {
            foreach (var documentId in projectChange.GetChangedDocuments(onlyGetDocumentsWithTextChanges: true))
            {
                var document = changed.GetDocument(documentId)!;
                var text = await document.GetTextAsync(cancellationToken);
                var encoding = await RefactoringHelpers.GetFileEncodingAsync(document.FilePath!, cancellationToken);
                await File.WriteAllTextAsync(document.FilePath!, text.ToString(), encoding, cancellationToken);
                RefactoringHelpers.UpdateSolutionCache(document);
            }
        }
    }

    private static async Task<IReadOnlyList<Diagnostic>> DiagnosticsAsync(
        Project? project,
        Func<Diagnostic, bool> include,
        CancellationToken cancellationToken)
    {
        var compilation = project is null ? null : await project.GetCompilationAsync(cancellationToken);
        return compilation is null
            ? Array.Empty<Diagnostic>()
            : compilation.GetDiagnostics(cancellationToken).Where(include).ToList();
    }

    private static string Key(Diagnostic diagnostic) => $"{diagnostic.Id}|{diagnostic.GetMessage()}";
}
