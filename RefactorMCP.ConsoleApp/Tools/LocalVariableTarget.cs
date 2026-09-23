using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using System.Linq;
using System.Threading;

/// <summary>
/// The local variable a caret points at, on its declaration or on any use of it,
/// for the tools that act on a single local.
/// </summary>
internal sealed class LocalVariableTarget
{
    private LocalVariableTarget(
        Document document,
        SyntaxNode root,
        SemanticModel model,
        ILocalSymbol local,
        VariableDeclaratorSyntax declarator,
        IdentifierNameSyntax? atCaret)
    {
        Document = document;
        Root = root;
        Model = model;
        Local = local;
        Declarator = declarator;
        AtCaret = atCaret;
    }

    public Document Document { get; }

    public SyntaxNode Root { get; }

    public SemanticModel Model { get; }

    public ILocalSymbol Local { get; }

    public VariableDeclaratorSyntax Declarator { get; }

    /// <summary>The use of the local the caret is on, or null when it is on the declaration.</summary>
    public IdentifierNameSyntax? AtCaret { get; }

    public VariableDeclarationSyntax Declaration => (VariableDeclarationSyntax)Declarator.Parent!;

    /// <summary>The declaration statement, or null for a local declared by a for or using statement.</summary>
    public LocalDeclarationStatementSyntax? DeclarationStatement => Declaration.Parent as LocalDeclarationStatementSyntax;

    public string Name => Local.Name;

    public static async Task<LocalVariableTarget> FindAsync(
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
        var token = root.FindToken(position);
        var symbol = token.Parent switch
        {
            VariableDeclaratorSyntax declarator => model.GetDeclaredSymbol(declarator, cancellationToken),
            IdentifierNameSyntax name => model.GetSymbolInfo(name, cancellationToken).Symbol,
            _ => null,
        };

        if (symbol is not ILocalSymbol local)
            throw new McpException($"Error: There is no local variable at {line}:{column}");

        var declaration = local.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(cancellationToken) as VariableDeclaratorSyntax
            ?? throw new McpException($"Error: '{local.Name}' is not declared by a local declaration statement");

        return new LocalVariableTarget(document, root, model, local, declaration, token.Parent as IdentifierNameSyntax);
    }

    /// <summary>The references to the local in its enclosing member, in source order.</summary>
    public IEnumerable<IdentifierNameSyntax> References()
    {
        return EnclosingMember().DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(IsReference);
    }

    public bool IsReference(IdentifierNameSyntax name) =>
        name.Identifier.ValueText == Name &&
        SymbolEqualityComparer.Default.Equals(Model.GetSymbolInfo(name).Symbol, Local);

    public bool Mentions(SyntaxNode node) =>
        node.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Any(IsReference);

    /// <summary>The member or accessor the local is declared in.</summary>
    public SyntaxNode EnclosingMember() =>
        Declarator.Ancestors().FirstOrDefault(a => a is MemberDeclarationSyntax or AccessorDeclarationSyntax) ?? Root;

    /// <summary>
    /// Whether a reference writes the variable it names: as the target of an
    /// assignment, an increment or decrement, or an out or ref argument.
    /// </summary>
    public static bool IsWrite(ExpressionSyntax reference)
    {
        var node = reference.Parent is MemberAccessExpressionSyntax access && access.Name == reference
            ? access
            : (ExpressionSyntax)reference;

        return node.Parent switch
        {
            AssignmentExpressionSyntax assignment => assignment.Left == node,
            PrefixUnaryExpressionSyntax prefix => prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression),
            PostfixUnaryExpressionSyntax postfix => postfix.IsKind(SyntaxKind.PostIncrementExpression) || postfix.IsKind(SyntaxKind.PostDecrementExpression),
            ArgumentSyntax argument => !argument.RefKindKeyword.IsKind(SyntaxKind.None) && !argument.RefKindKeyword.IsKind(SyntaxKind.InKeyword),
            RefExpressionSyntax => true,
            _ => false,
        };
    }

    /// <summary>
    /// Removes the declaration statement, leaving the comments above it in place on the
    /// statement that follows.
    /// </summary>
    public void RemoveDeclarationStatement(SyntaxEditor editor)
    {
        var statement = DeclarationStatement!;
        var statements = SiblingStatements()!.Value;
        var index = statements.IndexOf(statement);
        if (index + 1 < statements.Count)
        {
            var leading = statement.GetLeadingTrivia();
            // The statement's own indentation ends the declaration's leading trivia.
            if (leading.Count > 0 && leading.Last().IsKind(SyntaxKind.WhitespaceTrivia))
                leading = leading.RemoveAt(leading.Count - 1);

            if (leading.Any(t => !t.IsKind(SyntaxKind.WhitespaceTrivia) && !t.IsKind(SyntaxKind.EndOfLineTrivia)))
            {
                editor.ReplaceNode(statements[index + 1], (current, _) =>
                    current.WithLeadingTrivia(leading.AddRange(current.GetLeadingTrivia())));
            }
        }

        editor.RemoveNode(statement, SyntaxRemoveOptions.KeepNoTrivia);
    }

    public async Task<DocumentEditor> EditorAsync() => await DocumentEditor.CreateAsync(Document);

    /// <summary>
    /// Simplifies and formats what the edit annotated, then writes the document.
    /// </summary>
    public async Task WriteAsync(SyntaxEditor editor)
    {
        var changed = Document.WithSyntaxRoot(editor.GetChangedRoot());
        changed = await Simplifier.ReduceAsync(changed, Simplifier.Annotation);
        changed = await Formatter.FormatAsync(changed, Formatter.Annotation);
        await RefactoringHelpers.WriteAndUpdateCachesAsync(Document, (await changed.GetSyntaxRootAsync())!);
    }

    /// <summary>
    /// The statements the declaration statement sits among, or null when it is not in a
    /// block or switch section.
    /// </summary>
    public SyntaxList<StatementSyntax>? SiblingStatements() => DeclarationStatement?.Parent switch
    {
        BlockSyntax block => block.Statements,
        SwitchSectionSyntax section => section.Statements,
        _ => null,
    };

    /// <summary>
    /// Replaces the statements of the block or switch section holding the declaration,
    /// formats what changed and writes the document.
    /// </summary>
    public async Task WriteStatementsAsync(IEnumerable<StatementSyntax> statements)
    {
        var parent = DeclarationStatement!.Parent!;
        var list = SyntaxFactory.List(statements);
        SyntaxNode updated = parent switch
        {
            BlockSyntax block => block.WithStatements(list),
            SwitchSectionSyntax section => section.WithStatements(list),
            _ => throw new InvalidOperationException("The declaration is not in a block"),
        };

        await WriteAsync(Root.ReplaceNode(parent, updated));
    }

    /// <summary>Formats the nodes carrying <see cref="Formatter.Annotation"/> and writes the document.</summary>
    public async Task WriteAsync(SyntaxNode newRoot)
    {
        var formatted = Formatter.Format(newRoot, Formatter.Annotation, Document.Project.Solution.Workspace);
        await RefactoringHelpers.WriteAndUpdateCachesAsync(Document, formatted);
    }

    /// <summary>The local's type as it would be written at the declaration.</summary>
    public TypeSyntax TypeSyntax() =>
        SyntaxFactory.ParseTypeName(Local.Type.ToMinimalDisplayString(Model, Declarator.SpanStart));

    public static bool IsAnonymous(ITypeSymbol type) => type switch
    {
        { IsAnonymousType: true } => true,
        IArrayTypeSymbol array => IsAnonymous(array.ElementType),
        INamedTypeSymbol named => named.TypeArguments.Any(IsAnonymous),
        _ => false,
    };
}
