using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

[McpServerToolType]
public static class ReplaceTempWithQueryTool
{
    [McpServerTool, Description("Replace a local variable that holds the result of an expression with a query method, called wherever the local was used")]
    public static async Task<string> ReplaceTempWithQuery(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line of the local's declaration or of a use of it (1-based)")] int line,
        [Description("Column of the local's name on that line (1-based)")] int column,
        [Description("Name of the query method")] string queryName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var target = await LocalVariableTarget.FindAsync(solutionPath, filePath, line, column, cancellationToken);
            var statement = target.DeclarationStatement
                ?? throw new McpException($"Error: '{target.Name}' is not declared by a local declaration statement");

            if (target.Declarator.Initializer is not { } initializer)
                throw new McpException($"Error: '{target.Name}' has no initializer to become the query");
            if (statement.UsingKeyword != default)
                throw new McpException($"Error: '{target.Name}' is a using declaration, which would no longer be disposed");
            if (target.Local.IsRef)
                throw new McpException($"Error: '{target.Name}' is a ref local");
            if (target.SiblingStatements() is null)
                throw new McpException($"Error: The declaration of '{target.Name}' is not in a block");
            if (target.EnclosingMember() is not MethodDeclarationSyntax { Body: not null, Parent: ClassDeclarationSyntax })
                throw new McpException($"Error: '{target.Name}' is not declared in the body of a class's method");

            var references = target.References().ToList();
            var write = references.FirstOrDefault(LocalVariableTarget.IsWrite);
            if (write != null)
                throw new McpException($"Error: '{target.Name}' is assigned after its declaration, at line {LineOf(write)}");
            if (references.Count == 0)
                throw new McpException($"Error: '{target.Name}' is never used");
            if (references.Count > 1 && ExpressionFacts.HasSideEffects(initializer.Value))
                throw new McpException(
                    $"Error: The initializer of '{target.Name}' has side effects, which would run at each of its {references.Count} uses");

            InlineLocalVariableTool.EnsureInputsUnchanged(target, initializer.Value, references);

            var type = target.Local.ContainingType;
            if (type.GetMembers(queryName).Any())
                throw new McpException($"Error: '{type.Name}' already has a member named '{queryName}'");

            var root = await ReplaceAsync(target, statement, references, queryName);
            var formatted = Formatter.Format(root, target.Document.Project.Solution.Workspace);
            await RefactoringHelpers.WriteAndUpdateCachesAsync(target.Document, formatted);

            return $"Successfully replaced '{target.Name}' with the query '{queryName}' at {references.Count} use(s) in {filePath}";
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error replacing temp with query: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Extracts the initializer into the query, as Extract Method does, then inlines
    /// the local: each use becomes a call and the declaration goes. The query returns
    /// the local's type, so a conversion the declaration made is made by the query.
    /// </summary>
    private static async Task<SyntaxNode> ReplaceAsync(
        LocalVariableTarget target,
        LocalDeclarationStatementSyntax statement,
        List<IdentifierNameSyntax> references,
        string queryName)
    {
        var declarationMark = new SyntaxAnnotation();
        var useMark = new SyntaxAnnotation();
        var annotated = target.Root.ReplaceNodes(
            references.Cast<SyntaxNode>().Append(target.Declarator),
            (original, current) => current.WithAdditionalAnnotations(original == target.Declarator ? declarationMark : useMark));

        var document = target.Document.WithSyntaxRoot(annotated);
        var root = (await document.GetSyntaxRootAsync())!;
        var model = (await document.GetSemanticModelAsync())!;
        var declarator = (VariableDeclaratorSyntax)root.GetAnnotatedNodes(declarationMark).Single();
        var method = declarator.Ancestors().OfType<MethodDeclarationSyntax>().First();
        var extraction = new ExtractMethodRewriter(
            method,
            (ClassDeclarationSyntax)method.Parent!,
            declarator.Initializer!.Value,
            queryName,
            model,
            target.Local.Type);
        root = extraction.Visit(root)!;

        declarator = (VariableDeclaratorSyntax)root.GetAnnotatedNodes(declarationMark).Single();
        var call = declarator.Initializer!.Value.WithoutTrivia();
        if (call is AwaitExpressionSyntax)
            call = SyntaxFactory.ParenthesizedExpression(call);

        root = root.ReplaceNodes(root.GetAnnotatedNodes(useMark), (_, current) => call.WithTriviaFrom(current));
        declarator = (VariableDeclaratorSyntax)root.GetAnnotatedNodes(declarationMark).Single();
        return RemoveDeclaration(root, declarator);
    }

    /// <summary>
    /// Removes the local from its declaration, or the whole statement when it declares
    /// nothing else. Comments above a removed statement stay in place, above the
    /// statement that followed it.
    /// </summary>
    private static SyntaxNode RemoveDeclaration(SyntaxNode root, VariableDeclaratorSyntax declarator)
    {
        var declaration = (VariableDeclarationSyntax)declarator.Parent!;
        if (declaration.Variables.Count > 1)
            return root.RemoveNode(declarator, SyntaxRemoveOptions.KeepNoTrivia)!;

        var statement = (StatementSyntax)declaration.Parent!;
        var statements = statement.Parent switch
        {
            BlockSyntax block => block.Statements,
            SwitchSectionSyntax section => section.Statements,
            _ => throw new McpException("Error: The declaration is not in a block"),
        };

        var index = statements.IndexOf(statement);
        var leading = statement.GetLeadingTrivia();
        if (leading.Count > 0 && leading.Last().IsKind(SyntaxKind.WhitespaceTrivia))
            leading = leading.RemoveAt(leading.Count - 1);

        var remaining = statements.RemoveAt(index);
        if (index < remaining.Count && leading.Any(t => !t.IsKind(SyntaxKind.WhitespaceTrivia) && !t.IsKind(SyntaxKind.EndOfLineTrivia)))
            remaining = remaining.Replace(remaining[index], remaining[index].WithLeadingTrivia(leading.AddRange(remaining[index].GetLeadingTrivia())));

        SyntaxNode updated = statement.Parent switch
        {
            BlockSyntax block => block.WithStatements(remaining),
            SwitchSectionSyntax section => section.WithStatements(remaining),
            _ => statement.Parent!,
        };
        return root.ReplaceNode(statement.Parent!, updated);
    }

    private static int LineOf(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
}
