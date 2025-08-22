using ModelContextProtocol.Server;
using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

[McpServerToolType]
public static class FindReferencesTool
{
    // Find references with a minimal, LLM-friendly response and simple 1-based range selection.
    // Collapsed access kinds: Read | Write | Invocation | CreateInstance | TypeUse | Other
    [McpServerTool, Description("Find references to a symbol across the solution with simple range selection")]
    public static async Task<string> FindReferences(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file containing the symbol")] string filePath,
        [Description("Name of the symbol to search for")] string symbolName,
        [Description("Line number of the symbol (1-based, optional)")] int? line = null,
        [Description("Column number of the symbol (1-based, optional)")] int? column = null,
        [Description("Optional expected symbol kind: Namespace | Type | Member | Parameter | Variable")] string? symbolKind = null,
        [Description("Optional comma-separated access filter: Read,Write,Invocation,CreateInstance,TypeUse,Other")] string? accessFilter = null,
        [Description("1-based start index of results to return (inclusive)")] int from = 1,
        [Description("1-based end index of results to return (inclusive)")] int to = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (from <= 0 || to <= 0 || to < from)
                throw new McpException("Error: 'from' and 'to' must be positive and 'to' >= 'from' (1-based).");

            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var document = RefactoringHelpers.GetDocumentByPath(solution, filePath);
            if (document == null)
                throw new McpException($"Error: File {filePath} not found in solution");

            var symbol = await FindSymbol(document, symbolName, line, column, symbolKind, cancellationToken);
            if (symbol == null)
                throw new McpException($"Error: Symbol '{symbolName}' not found");

            var references = await SymbolFinder.FindReferencesAsync(symbol, solution, cancellationToken);

            var all = new List<RefItem>(capacity: 256);
            foreach (var r in references)
            {
                foreach (var loc in r.Locations)
                {
                    var l = loc.Location;
                    if (!l.IsInSource) continue;
                    var doc = loc.Document;

                    var text = await doc.GetTextAsync(cancellationToken);
                    var span = l.SourceSpan;
                    var lp = text.Lines.GetLinePositionSpan(span);
                    var lineIndex = lp.Start.Line;
                    var colIndex = lp.Start.Character;

                    var root = await doc.GetSyntaxRootAsync(cancellationToken);
                    if (root == null) continue;
                    var node = root.FindNode(span, getInnermostNodeForTie: true);

                    var kind = ClassifyKindCollapsed(loc, node, r.Definition);
                    all.Add(new RefItem
                    {
                        FilePath = doc.FilePath ?? string.Empty,
                        Line = lineIndex + 1,
                        Column = colIndex + 1,
                        Kind = kind,
                        LineText = TrimLine(text.Lines[lineIndex].ToString(), 160),
                    });
                }
            }

            // Normalize and filter (comma-separated filters supported)
            all.Sort(static (a, b) =>
            {
                var c = StringComparer.OrdinalIgnoreCase.Compare(a.FilePath, b.FilePath);
                if (c != 0) return c;
                c = a.Line.CompareTo(b.Line);
                if (c != 0) return c;
                return a.Column.CompareTo(b.Column);
            });

            var filterSet = ParseFilter(accessFilter);
            if (filterSet.Count > 0)
            {
                all = all.Where(i => filterSet.Contains(i.Kind)).ToList();
            }

            var total = all.Count;
            var startIdx = Math.Min(Math.Max(from - 1, 0), Math.Max(total - 1, 0));
            var endIdx = Math.Min(to - 1, Math.Max(total - 1, -1));
            var take = endIdx >= startIdx ? (endIdx - startIdx + 1) : 0;
            var items = (take > 0 ? all.Skip(startIdx).Take(take) : Enumerable.Empty<RefItem>()).ToArray();

            var files = all.Select(i => i.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            var byKind = all
                .GroupBy(i => i.Kind, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            var result = new Result
            {
                Symbol = symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                Kind = GetSymbolKindDisplay(symbol),
                Range = new ResultRange { From = from, To = to },
                Summary = new Summary
                {
                    Files = files,
                    ByKind = byKind,
                    Total = total > to ? total : (int?)null
                },
                Items = items
            };

            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            return json;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error finding references: {ex.Message}", ex);
        }
    }

    private static async Task<ISymbol?> FindSymbol(
        Document document,
        string name,
        int? line,
        int? column,
        string? symbolKind,
        CancellationToken ct)
    {
        var model = await document.GetSemanticModelAsync(ct);
        var root = await document.GetSyntaxRootAsync(ct);
        if (model == null || root == null)
            return null;

        if (line.HasValue && column.HasValue)
        {
            var text = await document.GetTextAsync(ct);
            if (line.Value > 0 && line.Value <= text.Lines.Count && column.Value > 0)
            {
                var pos = text.Lines[line.Value - 1].Start + column.Value - 1;
                var token = root.FindToken(pos);
                var node = token.Parent;
                while (node != null)
                {
                    var sym = model.GetDeclaredSymbol(node, ct) ?? model.GetSymbolInfo(node, ct).Symbol;
                    if (sym != null && sym.Name == name && MatchesKind(sym, symbolKind))
                        return sym;
                    node = node.Parent;
                }
            }
        }

        var decls = await SymbolFinder.FindDeclarationsAsync(document.Project, name, ignoreCase: false, ct);
        var match = decls.FirstOrDefault(s => MatchesKind(s, symbolKind));
        if (match != null)
            return match;

        // Parameter fallback without precise column: search parameters by name (optionally closest to provided line)
        if (string.Equals(symbolKind, "Parameter", StringComparison.OrdinalIgnoreCase))
        {
            var param = await FindParameterByName(document, name, line, ct);
            if (param != null) return param;
        }

        return null;
    }

    private static bool MatchesKind(ISymbol symbol, string? symbolKind)
    {
        if (string.IsNullOrWhiteSpace(symbolKind)) return true;
        var k = symbolKind.Trim();
        // Allowed generic groups only: Namespace | Type | Member | Parameter | Variable
        if (k.Equals("Namespace", StringComparison.OrdinalIgnoreCase))
            return symbol is INamespaceSymbol;
        if (k.Equals("Type", StringComparison.OrdinalIgnoreCase))
            return symbol is ITypeSymbol;
        if (k.Equals("Member", StringComparison.OrdinalIgnoreCase))
            return symbol is IMethodSymbol or IPropertySymbol or IFieldSymbol or IEventSymbol;
        if (k.Equals("Parameter", StringComparison.OrdinalIgnoreCase))
            return symbol is IParameterSymbol;
        if (k.Equals("Variable", StringComparison.OrdinalIgnoreCase))
            return symbol is ILocalSymbol;
        // Unknown group: accept all to avoid false negatives
        return true;
    }

    private static string GetSymbolKindDisplay(ISymbol symbol)
    {
        switch (symbol)
        {
            case INamedTypeSymbol t:
                if (t.IsRecord)
                    return t.TypeKind == TypeKind.Struct ? "RecordStruct" : "Record";
                return t.TypeKind.ToString(); // Class, Struct, Interface, Enum, Delegate
            case IMethodSymbol m:
                return m.MethodKind == MethodKind.Constructor ? "Constructor" : (m.MethodKind == MethodKind.LocalFunction ? "LocalFunction" : "Method");
            case IPropertySymbol:
                return "Property";
            case IFieldSymbol f:
                return f.IsConst ? "Const" : "Field";
            case IEventSymbol:
                return "Event";
            case IParameterSymbol:
                return "Parameter";
            case ILocalSymbol:
                return "Local";
            case INamespaceSymbol:
                return "Namespace";
            default:
                return symbol.Kind.ToString();
        }
    }

    private static async Task<IParameterSymbol?> FindParameterByName(Document document, string name, int? nearLine, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct);
        var model = await document.GetSemanticModelAsync(ct);
        if (root == null || model == null)
            return null;

        var candidates = root
            .DescendantNodes()
            .OfType<ParameterSyntax>()
            .Where(p => p.Identifier.ValueText == name)
            .ToList();

        if (candidates.Count == 0)
            return null;

        if (candidates.Count == 1)
            return model.GetDeclaredSymbol(candidates[0], ct) as IParameterSymbol;

        if (nearLine.HasValue)
        {
            var text = await document.GetTextAsync(ct);
            var best = candidates
                .Select(p =>
                {
                    var lp = text.Lines.GetLinePosition(p.Identifier.SpanStart);
                    var line = lp.Line + 1;
                    var dist = Math.Abs(line - nearLine.Value);
                    return (param: p, dist);
                })
                .OrderBy(t => t.dist)
                .First().param;
            return model.GetDeclaredSymbol(best, ct) as IParameterSymbol;
        }

        return null;
    }

    // Collapsed kinds: Read | Write | Invocation | CreateInstance | TypeUse | Other
    private static string ClassifyKindCollapsed(ReferenceLocation loc, SyntaxNode node, ISymbol targetSymbol)
    {
        // Invocation
        if (targetSymbol is IMethodSymbol && node.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().Any())
            return "Invocation";

        // CreateInstance
        if ((targetSymbol is IMethodSymbol ms && ms.MethodKind == MethodKind.Constructor) ||
            targetSymbol is INamedTypeSymbol)
        {
            if (node.AncestorsAndSelf().OfType<ObjectCreationExpressionSyntax>().Any() ||
                node.AncestorsAndSelf().OfType<ImplicitObjectCreationExpressionSyntax>().Any())
                return "CreateInstance";
        }

        // Write (includes compound/++, --) – detect before TypeUse
        if (IsWrite(node) || IsCompoundWrite(node))
            return "Write";

        // TypeUse – only for type symbols
        if (targetSymbol is ITypeSymbol && node.AncestorsAndSelf().OfType<TypeSyntax>().Any())
            return "TypeUse";

        // Other (nameof, attribute, implicit)
        if (loc.IsImplicit ||
            IsNameOf(node) ||
            node.AncestorsAndSelf().OfType<AttributeSyntax>().Any())
            return "Other";

        // Read (fallback)
        return "Read";
    }

    private static bool IsCompoundWrite(SyntaxNode node)
    {
        var p = node.Parent;
        if (p is AssignmentExpressionSyntax a)
            return a.Kind() != SyntaxKind.SimpleAssignmentExpression;
        if (p is PrefixUnaryExpressionSyntax pre &&
            (pre.IsKind(SyntaxKind.PreIncrementExpression) || pre.IsKind(SyntaxKind.PreDecrementExpression)))
            return true;
        if (p is PostfixUnaryExpressionSyntax post &&
            (post.IsKind(SyntaxKind.PostIncrementExpression) || post.IsKind(SyntaxKind.PostDecrementExpression)))
            return true;
        return false;
    }

    private static bool IsWrite(SyntaxNode node)
    {
        var parent = node.Parent;
        if (parent is AssignmentExpressionSyntax assign)
        {
            // On left-hand side of assignment
            return assign.Left.Span.Contains(node.Span);
        }
        if (parent is ArgumentSyntax arg)
        {
            // ref/out arguments considered write (or read/write)
            var kind = arg.RefKindKeyword.Kind();
            if (kind == SyntaxKind.RefKeyword || kind == SyntaxKind.OutKeyword)
                return true;
        }
        return false;
    }

    private static bool IsNameOf(SyntaxNode node)
    {
        foreach (var inv in node.AncestorsAndSelf().OfType<InvocationExpressionSyntax>())
        {
            if (inv.Expression is IdentifierNameSyntax id && id.Identifier.ValueText == "nameof")
                return true;
        }
        return false;
    }

    private static string TrimLine(string s, int max)
    {
        s = s.TrimEnd();
        return s.Length <= max ? s : s.Substring(0, Math.Max(0, max - 1)) + "…";
    }

    private static HashSet<string> ParseFilter(string? accessFilter)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(accessFilter)) return set;
        var parts = accessFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var p in parts)
        {
            if (p.Equals("Read", StringComparison.OrdinalIgnoreCase) ||
                p.Equals("Write", StringComparison.OrdinalIgnoreCase) ||
                p.Equals("Invocation", StringComparison.OrdinalIgnoreCase) ||
                p.Equals("CreateInstance", StringComparison.OrdinalIgnoreCase) ||
                p.Equals("TypeUse", StringComparison.OrdinalIgnoreCase) ||
                p.Equals("Other", StringComparison.OrdinalIgnoreCase))
            {
                set.Add(p);
            }
        }
        return set;
    }

    private sealed class RefItem
    {
        public string FilePath { get; set; } = string.Empty;
        public int Line { get; set; }
        public int Column { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string LineText { get; set; } = string.Empty;
    }

    private sealed class Summary
    {
        public int Files { get; set; }
        public Dictionary<string, int> ByKind { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public int? Total { get; set; }
    }

    private sealed class ResultRange { public int From { get; set; } public int To { get; set; } }

    private sealed class Result
    {
        public string Symbol { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public ResultRange Range { get; set; } = new();
        public Summary Summary { get; set; } = new();
        public RefItem[] Items { get; set; } = Array.Empty<RefItem>();
    }
}


