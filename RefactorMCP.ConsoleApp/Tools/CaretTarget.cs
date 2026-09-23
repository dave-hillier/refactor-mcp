using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Linq;
using System.Threading;

/// <summary>
/// The token a caret points at, with its document and semantic model, for the
/// tools that act on the statement or expression around it.
/// </summary>
internal sealed class CaretTarget
{
    private CaretTarget(Document document, SyntaxNode root, SemanticModel model, SyntaxToken token)
    {
        Document = document;
        Root = root;
        Model = model;
        Token = token;
    }

    public Document Document { get; }

    public SyntaxNode Root { get; }

    public SemanticModel Model { get; }

    public SyntaxToken Token { get; }

    public static async Task<CaretTarget> FindAsync(
        string solutionPath,
        string filePath,
        int line,
        int column,
        CancellationToken cancellationToken)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var document = RefactoringHelpers.GetDocumentByPath(solution, filePath)
            ?? throw new McpException($"Error: File {filePath} not found in solution");
        var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
        var model = (await document.GetSemanticModelAsync(cancellationToken))!;
        var text = await document.GetTextAsync(cancellationToken);

        if (line < 1 || line > text.Lines.Count || column < 1)
            throw new McpException($"Error: {line}:{column} is outside {filePath}");

        var position = text.Lines[line - 1].Start + column - 1;
        return new CaretTarget(document, root, model, root.FindToken(position));
    }

    /// <summary>The innermost node of a kind that contains the caret.</summary>
    public TNode? Enclosing<TNode>() where TNode : SyntaxNode =>
        Token.Parent?.AncestorsAndSelf().OfType<TNode>().FirstOrDefault();

    /// <summary>
    /// Formats the nodes carrying <see cref="Formatter.Annotation"/>, refuses the
    /// change if it adds compile errors, then writes it.
    /// </summary>
    public async Task ApplyAsync(SyntaxNode newRoot, CancellationToken cancellationToken)
    {
        var changed = Document.WithSyntaxRoot(newRoot);
        changed = await Formatter.FormatAsync(changed, Formatter.Annotation, cancellationToken: cancellationToken);

        var original = Document.Project.Solution;
        await SolutionEdits.EnsureCompilesAsync(original, changed.Project.Solution, cancellationToken);
        await SolutionEdits.WriteAsync(original, changed.Project.Solution, cancellationToken);
    }

    /// <summary>
    /// Whether introducing a local named <paramref name="name"/> for <paramref name="scope"/>
    /// would clash: a local or parameter of that name is in scope, the scope
    /// declares one, or the scope uses a member of that name the local would hide.
    /// </summary>
    public bool NameTaken(SyntaxNode scope, string name)
    {
        if (Model.LookupSymbols(scope.SpanStart, name: name).Any(s => s is ILocalSymbol or IParameterSymbol or IRangeVariableSymbol))
            return true;

        return scope.DescendantTokens().Any(t => t.IsKind(SyntaxKind.IdentifierToken) && t.ValueText == name);
    }

    /// <summary>A name that is a valid identifier and not a keyword.</summary>
    public static bool IsValidName(string name) =>
        SyntaxFacts.IsValidIdentifier(name) && SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None;

    /// <summary>
    /// The name of one element of a collection called <paramref name="collection"/>:
    /// <c>orders</c> gives <c>order</c> and <c>entries</c> gives <c>entry</c>, or null when
    /// the name is not a plural.
    /// </summary>
    public static string? Singular(string collection)
    {
        var name = collection.TrimStart('_');
        if (name.Length < 2)
            return null;

        name = char.ToLowerInvariant(name[0]) + name[1..];
        var singular = name switch
        {
            _ when name.EndsWith("ies", StringComparison.Ordinal) && name.Length > 3 => name[..^3] + "y",
            _ when name.EndsWith("ss", StringComparison.Ordinal) => null,
            _ when name.EndsWith('s') => name[..^1],
            _ => null,
        };
        return singular is not null && IsValidName(singular) ? singular : null;
    }
}
