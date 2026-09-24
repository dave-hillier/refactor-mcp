using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using System.IO;
using System.Threading;

/// <summary>
/// Finds the symbol a request names in a file: the one under a line and
/// column, one named on or declared across a line, or, given the name alone,
/// the only symbol of that name the file declares, the project declares or
/// the file uses. A name that could mean several symbols is refused with the
/// candidates listed, rather than one of them being picked.
/// </summary>
internal static class SymbolLookup
{
    public static async Task<ISymbol> FindAsync(
        Solution solution,
        string filePath,
        string name,
        int? line,
        int? column,
        CancellationToken cancellationToken = default)
    {
        var document = RefactoringHelpers.GetDocumentByPath(solution, filePath)
            ?? throw new McpException($"Error: File {filePath} not found in solution");
        var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
        var model = (await document.GetSemanticModelAsync(cancellationToken))!;
        var text = await document.GetTextAsync(cancellationToken);
        name = name.TrimStart('@');

        if (line is not null)
        {
            if (line < 1 || line > text.Lines.Count)
                throw new McpException($"Error: Line {line} is outside {filePath}");

            var lineSpan = text.Lines[line.Value - 1].Span;
            if (column is not null && column >= 1 && column - 1 <= lineSpan.Length
                && AtPosition(root, model, lineSpan.Start + column.Value - 1, name, cancellationToken) is { } atCaret)
            {
                return Definition(atCaret);
            }

            var onLine = Single(UsesOf(root, model, name, lineSpan, cancellationToken), name, $"on line {line}");
            if (onLine is not null)
                return onLine;

            // A line inside a member's body picks the member, not its type.
            var enclosing = Declarations(root, model, name, cancellationToken)
                .Where(d => d.Node.Span.IntersectsWith(lineSpan))
                .ToList();
            var innermost = enclosing
                .Where(d => !enclosing.Any(o => o.Node != d.Node && d.Node.Span.Contains(o.Node.Span)))
                .Select(d => d.Symbol);
            return Single(innermost, name, $"at line {line}")
                ?? throw new McpException($"Error: No symbol named '{name}' found at line {line} of {filePath}");
        }

        return Single(Declarations(root, model, name, cancellationToken).Select(d => d.Symbol), name, $"declared in {Path.GetFileName(filePath)}")
            ?? Single(await SymbolFinder.FindSourceDeclarationsAsync(document.Project, n => n == name, cancellationToken), name, $"declared in {document.Project.Name}")
            ?? Single(UsesOf(root, model, name, root.FullSpan, cancellationToken), name, $"used in {Path.GetFileName(filePath)}")
            ?? throw new McpException($"Error: Symbol '{name}' not found");
    }

    /// <summary>A symbol as its kind and display name, such as <c>method Sample.Run(int)</c>.</summary>
    public static string Describe(ISymbol symbol) => $"{Kind(symbol)} {Display(symbol)}";

    public static string Display(ISymbol symbol) =>
        symbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);

    public static string Kind(ISymbol symbol) => symbol switch
    {
        INamedTypeSymbol { IsRecord: true, TypeKind: TypeKind.Struct } => "record struct",
        INamedTypeSymbol { IsRecord: true } => "record",
        INamedTypeSymbol type => type.TypeKind.ToString().ToLowerInvariant(),
        IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor } => "constructor",
        IMethodSymbol { MethodKind: MethodKind.LocalFunction } => "local function",
        IMethodSymbol { MethodKind: MethodKind.PropertyGet or MethodKind.PropertySet } => "accessor",
        IMethodSymbol { MethodKind: MethodKind.UserDefinedOperator or MethodKind.Conversion } => "operator",
        IMethodSymbol => "method",
        IPropertySymbol { IsIndexer: true } => "indexer",
        IPropertySymbol => "property",
        IFieldSymbol { ContainingType.TypeKind: TypeKind.Enum } => "enum member",
        IFieldSymbol => "field",
        IEventSymbol => "event",
        ILocalSymbol => "local",
        IParameterSymbol => "parameter",
        ITypeParameterSymbol => "type parameter",
        INamespaceSymbol => "namespace",
        _ => symbol.Kind.ToString().ToLowerInvariant(),
    };

    /// <summary>
    /// The symbol named <paramref name="name"/> that the token at a position
    /// declares or refers to, or that a node around it does: the caret on a
    /// constructor's name finds its type.
    /// </summary>
    private static ISymbol? AtPosition(SyntaxNode root, SemanticModel model, int position, string name, CancellationToken cancellationToken)
    {
        for (var node = root.FindToken(position).Parent; node is not null; node = node.Parent)
        {
            if (SymbolOf(model, node, cancellationToken) is { } symbol && symbol.Name == name)
                return symbol;
        }

        return null;
    }

    /// <summary>What each identifier spelled <paramref name="name"/> in a span declares or refers to.</summary>
    private static IEnumerable<ISymbol> UsesOf(SyntaxNode root, SemanticModel model, string name, TextSpan span, CancellationToken cancellationToken) =>
        root.DescendantTokens(span)
            .Where(token => token.Span.IntersectsWith(span) && token.ValueText == name)
            .Select(token => AtPosition(root, model, token.SpanStart, name, cancellationToken))
            .OfType<ISymbol>();

    private static IEnumerable<(SyntaxNode Node, ISymbol Symbol)> Declarations(SyntaxNode root, SemanticModel model, string name, CancellationToken cancellationToken) =>
        root.DescendantNodes()
            .Select(node => (Node: node, Symbol: model.GetDeclaredSymbol(node, cancellationToken)))
            .Where(d => d.Symbol is not null && d.Symbol.Name == name)
            .Select(d => (d.Node, d.Symbol!));

    private static ISymbol? SymbolOf(SemanticModel model, SyntaxNode node, CancellationToken cancellationToken)
    {
        if (model.GetDeclaredSymbol(node, cancellationToken) is { } declared)
            return declared;

        var info = model.GetSymbolInfo(node, cancellationToken);
        return info.Symbol ?? (info.CandidateSymbols.Length == 1 ? info.CandidateSymbols[0] : null);
    }

    /// <summary>
    /// The one distinct symbol among <paramref name="candidates"/>, null when
    /// there is none, refusing when there are several.
    /// </summary>
    private static ISymbol? Single(IEnumerable<ISymbol> candidates, string name, string where)
    {
        var distinct = candidates
            .Select(Definition)
            .Distinct(SymbolEqualityComparer.Default)
            .OfType<ISymbol>()
            .ToList();

        return distinct.Count switch
        {
            0 => null,
            1 => distinct[0],
            _ => throw new McpException(
                $"Error: Multiple symbols named '{name}' {where}: " +
                string.Join("; ", distinct.Take(5).Select(DescribeWithLocation)) +
                "; pass line and column to choose one"),
        };
    }

    /// <summary>
    /// The declared symbol behind a use: a generic member rather than its
    /// instantiation, an extension method rather than its reduced form.
    /// </summary>
    public static ISymbol Definition(ISymbol symbol) =>
        symbol is IMethodSymbol { ReducedFrom: { } extension } ? extension.OriginalDefinition : symbol.OriginalDefinition;

    private static string DescribeWithLocation(ISymbol symbol)
    {
        var location = symbol.Locations.FirstOrDefault(l => l.IsInSource);
        return location is null ? Describe(symbol) : $"{Describe(symbol)} at {SolutionEdits.Describe(location)}";
    }
}
