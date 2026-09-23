using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace RefactorMCP.Tests.Catalog;

/// <summary>
/// Maps catalog refactorings onto today's tools. The mappings are the only
/// place that knows tool names, parameter names and error wording, so
/// reshaping a tool changes one mapping and no fixture.
///
/// Mappings live in classes implementing <see cref="ICatalogMappings"/>, one
/// per catalog group, and are discovered by reflection so groups can be added
/// without touching a shared list.
/// </summary>
internal static class CatalogAdapter
{
    private static readonly Lazy<Dictionary<string, CatalogMapping>> Mappings = new(Discover);

    public static bool Supports(string refactoring) => Mappings.Value.ContainsKey(refactoring);

    public static async Task<(string Tool, Dictionary<string, JsonElement> Arguments)> TranslateAsync(StepContext context)
    {
        var mapping = MappingFor(context.Step.Refactoring);
        return (mapping.Tool, await mapping.Arguments(context));
    }

    /// <summary>The message fragment the current tool reports for a catalog error code.</summary>
    public static string MessageFor(string refactoring, string errorCode)
    {
        var mapping = MappingFor(refactoring);
        return mapping.ErrorCodes.TryGetValue(errorCode, out var fragment)
            ? fragment
            : throw new InvalidOperationException(
                $"The adapter has no message for error code '{errorCode}' of '{refactoring}'. Add it to the mapping for '{refactoring}'.");
    }

    private static CatalogMapping MappingFor(string refactoring) =>
        Mappings.Value.TryGetValue(refactoring, out var mapping)
            ? mapping
            : throw new NotSupportedException($"The adapter has no mapping for '{refactoring}'");

    private static Dictionary<string, CatalogMapping> Discover()
    {
        var mappings = new Dictionary<string, CatalogMapping>(StringComparer.Ordinal);
        var sources = typeof(CatalogAdapter).Assembly.GetTypes()
            .Where(t => typeof(ICatalogMappings).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false });

        foreach (var source in sources)
        {
            var instance = (ICatalogMappings)Activator.CreateInstance(source)!;
            foreach (var mapping in instance.Mappings)
            {
                if (!mappings.TryAdd(mapping.Refactoring, mapping))
                    throw new InvalidOperationException($"'{mapping.Refactoring}' is mapped twice; the second is in {source.Name}");
            }
        }

        return mappings;
    }
}

/// <summary>A group of catalog-to-tool mappings, discovered by <see cref="CatalogAdapter"/>.</summary>
internal interface ICatalogMappings
{
    IEnumerable<CatalogMapping> Mappings { get; }
}

/// <summary>
/// How one catalog refactoring runs today: the tool to invoke, its arguments
/// built from the step, and the message fragment for each catalog error code.
/// </summary>
internal sealed record CatalogMapping(
    string Refactoring,
    string Tool,
    Func<StepContext, Task<Dictionary<string, JsonElement>>> Arguments,
    IReadOnlyDictionary<string, string> ErrorCodes)
{
    public CatalogMapping(
        string refactoring,
        string tool,
        Func<StepContext, Dictionary<string, JsonElement>> arguments,
        IReadOnlyDictionary<string, string>? errorCodes = null)
        : this(refactoring, tool, context => Task.FromResult(arguments(context)), errorCodes ?? NoErrorCodes)
    {
    }

    public static IReadOnlyDictionary<string, string> NoErrorCodes { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public static JsonElement Json(string value) => JsonSerializer.SerializeToElement(value);

    public static JsonElement Json<T>(T value) => JsonSerializer.SerializeToElement(value);
}

/// <summary>A resolved symbol and the 1-based position of its declared name.</summary>
internal sealed record SymbolLocation(ISymbol Symbol, string FilePath, int Line, int Column);

/// <summary>What an argument mapper can ask about the step it is translating.</summary>
internal sealed class StepContext
{
    public StepContext(CaseStep step, CatalogWorkspace workspace, bool isFirstStep)
    {
        Step = step;
        Workspace = workspace;
        IsFirstStep = isFirstStep;
    }

    public CaseStep Step { get; }

    public CatalogWorkspace Workspace { get; }

    /// <summary>
    /// Markers are read from <c>before/</c>, so they only describe the source
    /// the first step sees. Later steps target by symbol.
    /// </summary>
    public bool IsFirstStep { get; }

    public string SolutionPath => Workspace.SolutionPath;

    /// <summary>The absolute path of <c>target.file</c>.</summary>
    public string TargetFilePath() => Workspace.PathFor(TargetFile());

    /// <summary>The selection as <c>startLine:startColumn-endLine:endColumn</c>, end exclusive.</summary>
    public string SelectionRange()
    {
        var target = Step.Target ?? throw Missing("target");

        if (target.Range is not null)
            return target.Range;

        if (target.Selection == "marker")
            return MarkersForFirstStep().SelectionRange(TargetFile());

        throw Missing("target.selection or target.range");
    }

    /// <summary>The caret's 1-based line and column.</summary>
    public (int Line, int Column) Caret()
    {
        if (Step.Target?.Caret != "marker")
            throw Missing("target.caret");

        var position = MarkersForFirstStep().CaretOrThrow(TargetFile());
        return (position.Line, position.Column);
    }

    /// <summary>
    /// Resolves <c>target.symbol</c>, a documentation comment id, against the
    /// solution as the previous steps left it.
    /// </summary>
    public async Task<ISymbol> SymbolAsync()
    {
        var id = Step.Target?.Symbol ?? throw Missing("target.symbol");
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);

        foreach (var project in solution.Projects)
        {
            var compilation = await project.GetCompilationAsync();
            var symbol = compilation is null
                ? null
                : DocumentationCommentId.GetFirstSymbolForDeclarationId(id, compilation);

            if (symbol is not null && symbol.Locations.Any(l => l.IsInSource))
                return symbol;
        }

        // Roslyn cannot resolve some ids it produces, such as a property whose
        // type is a type parameter, so fall back to comparing declared symbols.
        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                var model = await document.GetSemanticModelAsync();
                var root = await document.GetSyntaxRootAsync();
                if (model is null || root is null)
                    continue;

                var declared = root.DescendantNodes()
                    .Select(node => model.GetDeclaredSymbol(node))
                    .FirstOrDefault(s => s?.GetDocumentationCommentId() == id);
                if (declared is not null)
                    return declared;
            }
        }

        throw new InvalidOperationException($"No symbol in the solution has the id '{id}'");
    }

    /// <summary>The file that declares <c>target.symbol</c>, or <c>target.file</c> when given.</summary>
    public async Task<string> SymbolFilePathAsync()
    {
        if (Step.Target?.File is not null)
            return TargetFilePath();

        var symbol = await SymbolAsync();
        return symbol.Locations.First(l => l.IsInSource).SourceTree!.FilePath;
    }

    /// <summary>
    /// Where <c>target.symbol</c> is declared: the file and the 1-based line
    /// and column of its name, which is what tools that find a symbol by
    /// position expect.
    /// </summary>
    public async Task<SymbolLocation> SymbolLocationAsync()
    {
        var symbol = await SymbolAsync();
        var location = symbol.Locations.First(l => l.IsInSource);
        var start = location.GetLineSpan().StartLinePosition;
        return new SymbolLocation(symbol, location.SourceTree!.FilePath, start.Line + 1, start.Character + 1);
    }

    /// <summary>The identifier or keyword the caret sits on, read from <c>before/</c>.</summary>
    public string TokenAtCaret()
    {
        var (line, column) = Caret();
        var text = MarkersForFirstStep().Text.Replace("\r\n", "\n").Split('\n')[line - 1];
        var start = column - 1;
        var end = start;
        while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_'))
            end++;

        return end > start
            ? text[start..end]
            : throw new InvalidOperationException($"The caret in {TargetFile()} is not on an identifier");
    }

    public bool HasArgument(string name) => Step.Arguments?.ContainsKey(name) == true;

    public JsonElement RequiredArgument(string name) =>
        Step.Arguments is not null && Step.Arguments.TryGetValue(name, out var value)
            ? value
            : throw Missing($"arguments.{name}");

    public string RequiredString(string name) =>
        RequiredArgument(name).GetString() ?? throw Missing($"arguments.{name} as a string");

    public string? OptionalString(string name) =>
        Step.Arguments is not null && Step.Arguments.TryGetValue(name, out var value) ? value.GetString() : null;

    /// <summary>An absolute path inside the workspace for a path relative to <c>before/</c>.</summary>
    public string WorkspacePath(string relative) => Workspace.PathFor(relative);

    private SourceMarkers MarkersForFirstStep()
    {
        if (!IsFirstStep)
            throw new InvalidOperationException("Markers describe before/, so only the first step can use them");

        return Workspace.MarkersFor(TargetFile());
    }

    private string TargetFile() => Step.Target?.File ?? throw Missing("target.file");

    private InvalidOperationException Missing(string what) =>
        new($"'{Step.Refactoring}' needs {what}");
}
