using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Threading;

/// <summary>
/// The token at a 1-based line and column of a document in a solution, with what the
/// tools that act on the code under a caret need to edit it.
/// </summary>
internal sealed class PositionTarget
{
    private PositionTarget(Document document, SyntaxNode root, SemanticModel model, SyntaxToken token)
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

    public static async Task<PositionTarget> FindAsync(
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
        return new PositionTarget(document, root, model, root.FindToken(position));
    }

    /// <summary>The innermost node of the given type containing the token, if any.</summary>
    public T? Enclosing<T>() where T : SyntaxNode =>
        Token.Parent?.AncestorsAndSelf().OfType<T>().FirstOrDefault();

    public string Describe() =>
        $"{Token.GetLocation().GetLineSpan().StartLinePosition.Line + 1}:{Token.GetLocation().GetLineSpan().StartLinePosition.Character + 1}";

    /// <summary>Writes the new root, updating the session's solution.</summary>
    public Task WriteAsync(SyntaxNode newRoot) => RefactoringHelpers.WriteAndUpdateCachesAsync(Document, newRoot);

    /// <summary>
    /// Shifts the lines after the first of a multi-line node by <paramref name="delta"/>
    /// columns, so a node moved to a different indentation keeps its layout.
    /// </summary>
    public static T Reindent<T>(T node, int delta) where T : SyntaxNode
    {
        if (delta == 0)
            return node;

        return node.ReplaceTrivia(
            node.DescendantTrivia().Where(t => t.IsKind(SyntaxKind.WhitespaceTrivia) && StartsLine(t)),
            (original, _) =>
            {
                var width = Math.Max(0, original.Span.Length + delta);
                return SyntaxFactory.Whitespace(new string(' ', width));
            });
    }

    /// <summary>The column the line holding the node starts at, counting its indentation.</summary>
    public static int IndentationOf(SyntaxNode node)
    {
        var text = node.SyntaxTree.GetText();
        var line = text.Lines.GetLineFromPosition(node.SpanStart);
        var column = 0;
        while (line.Start + column < line.End && char.IsWhiteSpace(text[line.Start + column]))
            column++;
        return column;
    }

    private static bool StartsLine(SyntaxTrivia trivia)
    {
        var text = trivia.SyntaxTree!.GetText();
        var line = text.Lines.GetLineFromPosition(trivia.SpanStart);
        return line.Start == trivia.SpanStart;
    }
}
