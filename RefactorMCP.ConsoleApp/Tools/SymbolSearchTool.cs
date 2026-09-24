using ModelContextProtocol.Server;
using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading;

/// <summary>
/// Read-only questions about where a symbol is used and what builds on it.
/// Each answers with source locations relative to the solution's directory,
/// grouped by file, with the line of code at each so the answer can usually
/// be acted on without reading the files.
/// </summary>
[McpServerToolType]
public static class SymbolSearchTool
{
    private const int MaxLineLength = 160;

    [McpServerTool(ReadOnly = true, Idempotent = true), Description("List every reference to a symbol across the solution, grouped by file with the line of code at each; writes, implicit uses and uses through an interface or base member are marked")]
    public static async Task<string> FindReferences(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to a C# file declaring or using the symbol")] string filePath,
        [Description("Name of the symbol")] string symbolName,
        [Description("Line of the symbol's declaration or of a use of it, to choose between symbols of the same name (1-based, optional)")] int? line = null,
        [Description("Column of the symbol's name on that line (1-based, optional)")] int? column = null,
        [Description("Also list the symbol's declarations (default false)")] bool includeDeclarations = false,
        [Description("Most locations to list (default 200)")] int maxResults = 200,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var symbol = await SymbolLookup.FindAsync(solution, filePath, symbolName, line, column, cancellationToken);
            var referenced = await SymbolFinder.FindReferencesAsync(symbol, solution, cancellationToken);

            var hits = new List<Hit>();
            if (includeDeclarations)
                hits.AddRange(symbol.Locations.Where(l => l.IsInSource).Select(l => new Hit(l, new[] { "declaration" })));

            // References to the symbol itself come first, so a location found
            // through a cascaded symbol as well is reported as a direct use.
            foreach (var group in referenced.OrderBy(g => IsSameOrPartOf(g.Definition, symbol) ? 0 : 1))
            {
                var via = IsSameOrPartOf(group.Definition, symbol) ? null : $"via {SymbolLookup.Describe(group.Definition)}";
                foreach (var reference in group.Locations.Where(r => r.Location.IsInSource))
                    hits.Add(new Hit(reference.Location, Tags(reference, via, cancellationToken)));
            }

            hits = hits
                .GroupBy(h => (h.Location.SourceTree!.FilePath, h.Location.SourceSpan.Start))
                .Select(g => g.First())
                .ToList();

            var subject = SymbolLookup.Describe(symbol);
            if (hits.Count == 0)
                return $"No references to {subject}";

            var header = $"{Count(hits.Count, "reference")} to {subject} in {Count(hits.Select(h => h.Location.SourceTree!.FilePath).Distinct().Count(), "file")}";
            return Report(solution, header, hits, maxResults);
        }
        catch (Exception ex)
        {
            throw new McpException($"Error finding references: {ex.Message}", ex);
        }
    }

    [McpServerTool(ReadOnly = true, Idempotent = true), Description("List the types implementing an interface, or the members implementing an interface member, across the solution")]
    public static async Task<string> FindImplementations(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to a C# file declaring or using the interface or member")] string filePath,
        [Description("Name of the interface or interface member")] string symbolName,
        [Description("Line of the symbol's declaration or of a use of it, to choose between symbols of the same name (1-based, optional)")] int? line = null,
        [Description("Column of the symbol's name on that line (1-based, optional)")] int? column = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var symbol = await SymbolLookup.FindAsync(solution, filePath, symbolName, line, column, cancellationToken);

            IEnumerable<ISymbol> implementations = symbol switch
            {
                INamedTypeSymbol { TypeKind: TypeKind.Interface } type =>
                    await SymbolFinder.FindImplementationsAsync(type, solution, transitive: true, cancellationToken: cancellationToken),
                IMethodSymbol or IPropertySymbol or IEventSymbol when symbol.ContainingType?.TypeKind == TypeKind.Interface =>
                    await SymbolFinder.FindImplementationsAsync(symbol, solution, cancellationToken: cancellationToken),
                _ => throw new McpException(
                    $"Error: {SymbolLookup.Describe(symbol)} is not an interface or a member of one; use find-overrides for virtual and abstract members"),
            };

            return Declarations(solution, "implementation", symbol, implementations, _ => null);
        }
        catch (Exception ex)
        {
            throw new McpException($"Error finding implementations: {ex.Message}", ex);
        }
    }

    [McpServerTool(ReadOnly = true, Idempotent = true), Description("List the members overriding a virtual, abstract or override method, property or event, at every level of the hierarchy below it")]
    public static async Task<string> FindOverrides(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to a C# file declaring or using the member")] string filePath,
        [Description("Name of the member")] string memberName,
        [Description("Line of the member's declaration or of a use of it, to choose between members of the same name (1-based, optional)")] int? line = null,
        [Description("Column of the member's name on that line (1-based, optional)")] int? column = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var symbol = await SymbolLookup.FindAsync(solution, filePath, memberName, line, column, cancellationToken);
            if (symbol is not (IMethodSymbol or IPropertySymbol or IEventSymbol) || symbol.ContainingType?.TypeKind == TypeKind.Interface)
                throw new McpException($"Error: {SymbolLookup.Describe(symbol)} is not a class member; use find-implementations for interfaces");
            if (!(symbol.IsVirtual || symbol.IsAbstract || symbol.IsOverride))
                throw new McpException($"Error: {SymbolLookup.Describe(symbol)} is not virtual, abstract or an override, so nothing can override it");

            var overrides = await SymbolFinder.FindOverridesAsync(symbol, solution, cancellationToken: cancellationToken);

            // An override further down overrides an intermediate one; saying
            // which shows the shape of the hierarchy.
            return Declarations(solution, "override", symbol, overrides, member =>
                Overridden(member) is { } overridden && !SymbolEqualityComparer.Default.Equals(overridden.OriginalDefinition, symbol)
                    ? $"overrides {SymbolLookup.Display(overridden)}"
                    : null);
        }
        catch (Exception ex)
        {
            throw new McpException($"Error finding overrides: {ex.Message}", ex);
        }
    }

    [McpServerTool(ReadOnly = true, Idempotent = true), Description("List the methods, properties and other members that call a method, or use a property or event, grouped by caller with the line of code at each call; calls made through an interface or base member are marked")]
    public static async Task<string> FindCallers(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to a C# file declaring or using the member")] string filePath,
        [Description("Name of the method, property or event")] string memberName,
        [Description("Line of the member's declaration or of a use of it, to choose between members of the same name (1-based, optional)")] int? line = null,
        [Description("Column of the member's name on that line (1-based, optional)")] int? column = null,
        [Description("Most call sites to list (default 200)")] int maxResults = 200,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var symbol = await SymbolLookup.FindAsync(solution, filePath, memberName, line, column, cancellationToken);
            if (symbol is not (IMethodSymbol or IPropertySymbol or IEventSymbol))
                throw new McpException($"Error: {SymbolLookup.Describe(symbol)} is not a method, property or event; use find-references to see where it is used");

            var callers = (await SymbolFinder.FindCallersAsync(symbol, solution, cancellationToken))
                .Select(caller => (Caller: caller, Sites: caller.Locations.Where(l => l.IsInSource).OrderBy(l => l.SourceTree!.FilePath, StringComparer.Ordinal).ThenBy(l => l.SourceSpan.Start).ToList()))
                .Where(c => c.Sites.Count > 0)
                .OrderBy(c => c.Sites[0].SourceTree!.FilePath, StringComparer.Ordinal)
                .ThenBy(c => c.Sites[0].SourceSpan.Start)
                .ToList();

            var subject = SymbolLookup.Describe(symbol);
            if (callers.Count == 0)
                return $"No callers of {subject}";

            var total = callers.Sum(c => c.Sites.Count);
            var report = new StringBuilder($"{Count(total, "call")} to {subject} from {Count(callers.Count, "caller")}");
            var shown = 0;
            foreach (var (caller, sites) in callers)
            {
                if (shown >= maxResults)
                    break;

                var through = caller.IsDirect ? "" : $" [through {SymbolLookup.Describe(caller.CalledSymbol)}]";
                report.Append($"\n{SymbolLookup.Describe(caller.CallingSymbol)}{through}");
                foreach (var site in sites.Take(maxResults - shown))
                {
                    report.Append($"\n  {Position(solution, site, withPath: true)}  {SourceLine(site)}");
                    shown++;
                }
            }

            return report.Append(More(total, shown)).ToString();
        }
        catch (Exception ex)
        {
            throw new McpException($"Error finding callers: {ex.Message}", ex);
        }
    }

    private sealed record Hit(Location Location, IReadOnlyList<string> Tags);

    /// <summary>
    /// Whether a definition the reference search reports is the symbol itself
    /// or one of its parts, such as a property's accessor or a type's
    /// constructor, rather than a symbol it cascaded to.
    /// </summary>
    private static bool IsSameOrPartOf(ISymbol definition, ISymbol symbol) =>
        SymbolEqualityComparer.Default.Equals(definition, symbol)
        || definition is IMethodSymbol { AssociatedSymbol: { } associated } && SymbolEqualityComparer.Default.Equals(associated, symbol)
        || definition is IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor } && SymbolEqualityComparer.Default.Equals(definition.ContainingType, symbol);

    private static IReadOnlyList<string> Tags(ReferenceLocation reference, string? via, CancellationToken cancellationToken)
    {
        var tags = new List<string>();
        if (IsWrite(reference.Location, cancellationToken))
            tags.Add("write");
        if (reference.IsImplicit)
            tags.Add("implicit");
        if (reference.IsCandidateLocation)
            tags.Add($"candidate: {reference.CandidateReason}");
        if (via is not null)
            tags.Add(via);
        return tags;
    }

    /// <summary>
    /// Whether the reference is assigned, incremented, passed by <c>ref</c> or
    /// <c>out</c>, or assigned by deconstruction, rather than only read.
    /// </summary>
    private static bool IsWrite(Location location, CancellationToken cancellationToken)
    {
        var root = location.SourceTree!.GetRoot(cancellationToken);
        if (root.FindNode(location.SourceSpan, getInnermostNodeForTie: true) is not ExpressionSyntax expression)
            return false;

        // Widen the name to the expression that denotes the variable: this.x,
        // a.b.x, a?.x and (x).
        while (expression.Parent switch
        {
            MemberAccessExpressionSyntax access => access.Name == expression,
            MemberBindingExpressionSyntax => true,
            ConditionalAccessExpressionSyntax conditional => conditional.WhenNotNull == expression,
            ParenthesizedExpressionSyntax => true,
            _ => false,
        })
        {
            expression = (ExpressionSyntax)expression.Parent!;
        }

        return IsAssignedTo(expression);
    }

    private static bool IsAssignedTo(ExpressionSyntax expression) => expression.Parent switch
    {
        AssignmentExpressionSyntax assignment => assignment.Left == expression,
        PrefixUnaryExpressionSyntax unary => unary.IsKind(SyntaxKind.PreIncrementExpression) || unary.IsKind(SyntaxKind.PreDecrementExpression),
        PostfixUnaryExpressionSyntax unary => unary.IsKind(SyntaxKind.PostIncrementExpression) || unary.IsKind(SyntaxKind.PostDecrementExpression),
        ArgumentSyntax { Parent: TupleExpressionSyntax tuple } => IsAssignedTo(tuple),
        ArgumentSyntax argument => argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword) || argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword),
        _ => false,
    };

    /// <summary>
    /// The symbols found for <paramref name="symbol"/>, one line each at its
    /// first declaration in source, with an optional note.
    /// </summary>
    private static string Declarations(Solution solution, string noun, ISymbol symbol, IEnumerable<ISymbol> found, Func<ISymbol, string?> note)
    {
        var entries = found
            .Select(s => (Symbol: s, Location: s.Locations.Where(l => l.IsInSource).OrderBy(l => l.SourceTree!.FilePath, StringComparer.Ordinal).ThenBy(l => l.SourceSpan.Start).FirstOrDefault()))
            .Where(e => e.Location is not null)
            .GroupBy(e => e.Symbol, SymbolEqualityComparer.Default)
            .Select(g => g.First())
            .OrderBy(e => e.Location!.SourceTree!.FilePath, StringComparer.Ordinal)
            .ThenBy(e => e.Location!.SourceSpan.Start)
            .ToList();

        var subject = SymbolLookup.Describe(symbol);
        if (entries.Count == 0)
            return $"No {noun}s of {subject}";

        var report = new StringBuilder($"{Count(entries.Count, noun)} of {subject}");
        foreach (var (member, location) in entries)
        {
            var extra = note(member) is { } text ? $" [{text}]" : "";
            report.Append($"\n  {Position(solution, location!, withPath: true)}  {SymbolLookup.Describe(member)}{extra}");
        }

        return report.ToString();
    }

    private static ISymbol? Overridden(ISymbol member) => member switch
    {
        IMethodSymbol method => method.OverriddenMethod,
        IPropertySymbol property => property.OverriddenProperty,
        IEventSymbol @event => @event.OverriddenEvent,
        _ => null,
    };

    /// <summary>The hits grouped by file, in file and then source order, up to <paramref name="maxResults"/>.</summary>
    private static string Report(Solution solution, string header, IReadOnlyList<Hit> hits, int maxResults)
    {
        var report = new StringBuilder(header);
        var shown = 0;
        foreach (var file in hits
                     .GroupBy(h => h.Location.SourceTree!.FilePath)
                     .OrderBy(g => RelativePath(solution, g.Key), StringComparer.Ordinal))
        {
            if (shown >= maxResults)
                break;

            report.Append('\n').Append(RelativePath(solution, file.Key));
            foreach (var hit in file.OrderBy(h => h.Location.SourceSpan.Start).Take(maxResults - shown))
            {
                var tags = hit.Tags.Count == 0 ? "" : $"[{string.Join(", ", hit.Tags)}] ";
                report.Append($"\n  {Position(solution, hit.Location, withPath: false)}  {tags}{SourceLine(hit.Location)}");
                shown++;
            }
        }

        return report.Append(More(hits.Count, shown)).ToString();
    }

    private static string More(int total, int shown) =>
        shown < total ? $"\n... {total - shown} more not shown; raise maxResults to see them" : "";

    private static string Position(Solution solution, Location location, bool withPath)
    {
        var start = location.GetLineSpan().StartLinePosition;
        var position = $"{start.Line + 1}:{start.Character + 1}";
        return withPath ? $"{RelativePath(solution, location.SourceTree!.FilePath)}:{position}" : position;
    }

    /// <summary>The trimmed line of code a location starts on, shortened when long.</summary>
    private static string SourceLine(Location location)
    {
        var text = location.SourceTree!.GetText();
        var line = text.Lines.GetLineFromPosition(location.SourceSpan.Start).ToString().Trim();
        return line.Length <= MaxLineLength ? line : line[..(MaxLineLength - 3)] + "...";
    }

    /// <summary>A path relative to the solution's directory, with forward slashes.</summary>
    private static string RelativePath(Solution solution, string path)
    {
        var directory = Path.GetDirectoryName(solution.FilePath);
        var relative = string.IsNullOrEmpty(directory) ? path : Path.GetRelativePath(directory, path);
        return relative.Replace('\\', '/');
    }

    private static string Count(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
