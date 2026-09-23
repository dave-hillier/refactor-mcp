using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Text;
using System.Threading;

/// <summary>
/// A document and a caret in it, for the refactorings that act on the
/// statement or expression under the cursor, and writing the changed document
/// back once it is known to compile.
/// </summary>
internal sealed class CaretDocument
{
    private CaretDocument(Document document, SyntaxNode root, SemanticModel model, int position)
    {
        Document = document;
        Root = root;
        Model = model;
        Position = position;
    }

    public Document Document { get; }

    public SyntaxNode Root { get; }

    public SemanticModel Model { get; }

    public int Position { get; }

    public static async Task<CaretDocument> FindAsync(
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

        return new CaretDocument(document, root, model, text.Lines[line - 1].Start + column - 1);
    }

    /// <summary>
    /// The innermost if statement whose header, from <c>if</c> to the closing
    /// parenthesis, holds the caret.
    /// </summary>
    public IfStatementSyntax IfStatement() =>
        Innermost<IfStatementSyntax>(s => TextSpan.FromBounds(s.IfKeyword.SpanStart, s.CloseParenToken.Span.End))
        ?? throw new McpException("Error: The caret is not on an if statement");

    /// <summary>The innermost switch statement whose header, from <c>switch</c> to the open brace, holds the caret.</summary>
    public SwitchStatementSyntax SwitchStatement() =>
        Innermost<SwitchStatementSyntax>(s => TextSpan.FromBounds(s.SwitchKeyword.SpanStart, s.OpenBraceToken.SpanStart))
        ?? throw new McpException("Error: The caret is not on a switch statement");

    /// <summary>The innermost switch expression whose governing expression or <c>switch</c> keyword holds the caret.</summary>
    public SwitchExpressionSyntax SwitchExpression() =>
        Innermost<SwitchExpressionSyntax>(s => TextSpan.FromBounds(s.SpanStart, s.OpenBraceToken.SpanStart))
        ?? throw new McpException("Error: The caret is not on a switch expression");

    private T? Innermost<T>(Func<T, TextSpan> header) where T : SyntaxNode =>
        Root.FindToken(Position).Parent?
            .AncestorsAndSelf()
            .OfType<T>()
            .FirstOrDefault(node => header(node).Contains(Position) || header(node).End == Position);

    /// <summary>
    /// Adds the imports, simplifies and formats what the edit annotated, refuses the change if it
    /// adds compile errors, then writes the document and makes it current.
    /// </summary>
    public async Task ApplyAsync(SyntaxNode newRoot, CancellationToken cancellationToken)
    {
        var changed = Document.WithSyntaxRoot(newRoot);
        changed = await ImportAdder.AddImportsAsync(changed, Simplifier.AddImportsAnnotation, cancellationToken: cancellationToken);
        changed = await Simplifier.ReduceAsync(changed, Simplifier.Annotation, cancellationToken: cancellationToken);
        changed = await Formatter.FormatAsync(changed, Formatter.Annotation, cancellationToken: cancellationToken);

        var original = Document.Project.Solution;
        await SolutionEdits.EnsureCompilesAsync(original, changed.Project.Solution, cancellationToken);
        await SolutionEdits.WriteAsync(original, changed.Project.Solution, cancellationToken);
    }

    /// <summary>The statements of a block, or the statement itself when it is not a block.</summary>
    public static IReadOnlyList<StatementSyntax> Statements(StatementSyntax statement) =>
        statement is BlockSyntax block ? block.Statements : new[] { statement };

    /// <summary>The statements a statement sits among, when it is in a block or switch section.</summary>
    public static SyntaxList<StatementSyntax>? Siblings(StatementSyntax statement) => statement.Parent switch
    {
        BlockSyntax block => block.Statements,
        SwitchSectionSyntax section => section.Statements,
        _ => null,
    };

    /// <summary>Whether control can run off the end of the statements, rather than always jumping away.</summary>
    public bool EndPointIsReachable(IReadOnlyList<StatementSyntax> statements) =>
        statements.Count == 0 || Model.AnalyzeControlFlow(statements[0], statements[^1]).EndPointIsReachable;

    /// <summary>The statement without the blank lines that separated it from what came before.</summary>
    public static T WithoutLeadingBlankLines<T>(T node) where T : SyntaxNode
    {
        var leading = node.GetLeadingTrivia();
        var start = 0;
        for (var i = 0; i < leading.Count; i++)
        {
            if (leading[i].IsKind(SyntaxKind.EndOfLineTrivia))
                start = i + 1;
            else if (!leading[i].IsKind(SyntaxKind.WhitespaceTrivia))
                break;
        }

        return node.WithLeadingTrivia(leading.Skip(start));
    }

    /// <summary>The statement with a blank line before it, unless it already has one.</summary>
    public static T WithBlankLineBefore<T>(T node) where T : SyntaxNode
    {
        var leading = node.GetLeadingTrivia();
        if (leading.SkipWhile(t => t.IsKind(SyntaxKind.WhitespaceTrivia)).FirstOrDefault().IsKind(SyntaxKind.EndOfLineTrivia))
            return node;

        var endOfLine = node.GetTrailingTrivia().LastOrDefault(t => t.IsKind(SyntaxKind.EndOfLineTrivia));
        return node.WithLeadingTrivia(leading.Insert(0, endOfLine == default ? SyntaxFactory.ElasticCarriageReturnLineFeed : endOfLine));
    }

    /// <summary>A block holding the statements, laid out by the formatter.</summary>
    public static BlockSyntax Block(IEnumerable<StatementSyntax> statements) =>
        SyntaxFactory.Block(statements).WithAdditionalAnnotations(Formatter.Annotation);
}
