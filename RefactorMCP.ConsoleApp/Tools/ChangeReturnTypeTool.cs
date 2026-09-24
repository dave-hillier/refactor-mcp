using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;

[McpServerToolType]
public static class ChangeReturnTypeTool
{
    private const string DeclarationKind = "RefactorMCP.ChangeReturnType.Declaration";
    private const string ReturnTypeKind = "RefactorMCP.ChangeReturnType.ReturnType";
    private const string UseKind = "RefactorMCP.ChangeReturnType.Use";

    [McpServerTool, Description("Change a method's return type, with its overrides and implementations, when its body and every caller still compile and bind as before")]
    public static async Task<string> ChangeReturnType(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the method")] string filePath,
        [Description("Name of the method")] string methodName,
        [Description("The new return type, as written in C#")] string returnType,
        [Description("A line of the method's declaration (1-based), to choose between overloads")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var method = await SolutionEdits.FindMethodAsync(solution, filePath, methodName, line, cancellationToken);
            if (method.MethodKind != MethodKind.Ordinary && method.MethodKind != MethodKind.LocalFunction)
                throw new McpException($"Error: '{methodName}' has no return type to change");

            var type = SyntaxFactory.ParseTypeName(returnType);
            if (type.ContainsDiagnostics || type.ToString() != returnType.Trim())
                throw new McpException($"Error: '{returnType}' is not a type");

            var family = await MethodFamily.FindAsync(solution, method, cancellationToken);
            var edits = await EditsAsync(solution, family, type, cancellationToken);
            var (annotated, changed) = await ApplyAsync(solution, edits, cancellationToken);

            await EnsureCompilesAsync(solution, changed, cancellationToken);
            await EnsureCallsBindAsBeforeAsync(annotated, changed, edits, cancellationToken);
            await EnsureNoNullableWarningsAsync(solution, changed, cancellationToken);
            await SolutionEdits.WriteAsync(solution, changed, cancellationToken);

            return $"Successfully changed the return type of '{methodName}' to {returnType}";
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error changing return type: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// The return types to replace, and the nodes to find again in both the
    /// original and the changed solution: the declarations, their return
    /// types, and around every reference the statement whose calls must bind
    /// as they did before.
    /// </summary>
    private sealed record Edits(
        Dictionary<DocumentId, Dictionary<SyntaxNode, List<SyntaxAnnotation>>> Marks,
        Dictionary<SyntaxNode, TypeSyntax> ReturnTypes);

    private static async Task<Edits> EditsAsync(
        Solution solution,
        IReadOnlyList<IMethodSymbol> family,
        TypeSyntax type,
        CancellationToken cancellationToken)
    {
        var edits = new Edits(new(), new());

        foreach (var member in family)
        {
            foreach (var reference in member.DeclaringSyntaxReferences)
            {
                var declaration = await reference.GetSyntaxAsync(cancellationToken);
                var returnType = declaration switch
                {
                    MethodDeclarationSyntax m => m.ReturnType,
                    LocalFunctionStatementSyntax l => l.ReturnType,
                    _ => null,
                };
                if (returnType is null)
                    continue;

                var document = solution.GetDocument(reference.SyntaxTree)!;
                Mark(edits, document.Id, declaration, DeclarationKind);
                Mark(edits, document.Id, returnType, ReturnTypeKind);
                edits.ReturnTypes[returnType] = type.WithTriviaFrom(returnType);
            }

            var references = await SymbolFinder.FindReferencesAsync(member, solution, cancellationToken);
            foreach (var location in references.SelectMany(r => r.Locations).Where(l => l.Location.IsInSource))
            {
                var root = await location.Document.GetSyntaxRootAsync(cancellationToken);
                var node = root!.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true);
                var context = node.AncestorsAndSelf().FirstOrDefault(n => n is StatementSyntax or MemberDeclarationSyntax)
                    ?? node;
                Mark(edits, location.Document.Id, context, UseKind);
            }
        }

        return edits;
    }

    private static void Mark(Edits edits, DocumentId document, SyntaxNode node, string kind)
    {
        if (!edits.Marks.TryGetValue(document, out var marks))
            edits.Marks[document] = marks = new Dictionary<SyntaxNode, List<SyntaxAnnotation>>();

        if (!marks.TryGetValue(node, out var annotations))
            marks[node] = annotations = new List<SyntaxAnnotation>();

        if (!annotations.Any(a => a.Kind == kind))
            annotations.Add(new SyntaxAnnotation(kind));
    }

    /// <summary>The original solution with the marks only, and the changed one with the new return types as well.</summary>
    private static async Task<(Solution Annotated, Solution Changed)> ApplyAsync(
        Solution solution,
        Edits edits,
        CancellationToken cancellationToken)
    {
        var annotated = solution;
        var changed = solution;
        foreach (var (documentId, marks) in edits.Marks)
        {
            var root = await solution.GetDocument(documentId)!.GetSyntaxRootAsync(cancellationToken);
            annotated = annotated.WithDocumentSyntaxRoot(
                documentId,
                root!.ReplaceNodes(marks.Keys, (original, current) => current.WithAdditionalAnnotations(marks[original])));
            changed = changed.WithDocumentSyntaxRoot(
                documentId,
                root.ReplaceNodes(marks.Keys, (original, current) =>
                    (edits.ReturnTypes.TryGetValue(original, out var type) ? type : current)
                        .WithAdditionalAnnotations(marks[original])));
        }

        return (annotated, changed);
    }

    /// <summary>
    /// Errors in the new return type mean it does not resolve; errors
    /// elsewhere in the method's own declarations mean its body does not fit
    /// the new type; errors anywhere else are callers that no longer compile.
    /// </summary>
    private static async Task EnsureCompilesAsync(Solution solution, Solution changed, CancellationToken cancellationToken)
    {
        var errors = await SolutionEdits.NewDiagnosticsAsync(
            solution,
            changed,
            d => d.Severity == DiagnosticSeverity.Error,
            cancellationToken);
        if (errors.Count == 0)
            return;

        var returnTypes = await MarkedSpansAsync(changed, ReturnTypeKind, cancellationToken);
        var declarations = await MarkedSpansAsync(changed, DeclarationKind, cancellationToken);

        var unknownType = errors.FirstOrDefault(e => Inside(e, returnTypes));
        if (unknownType is not null)
            throw new McpException($"Error: The new return type does not resolve: {SolutionEdits.Describe(unknownType)}");

        var inBody = errors.FirstOrDefault(e => Inside(e, declarations));
        if (inBody is not null)
            throw new McpException($"Error: The method's body does not fit the new return type: {SolutionEdits.Describe(inBody)}");

        throw new McpException($"Error: A caller would not compile with the new return type: {SolutionEdits.Describe(errors[0])}");
    }

    /// <summary>
    /// A result of a different type can still compile yet pick a different
    /// overload of whatever it is passed to, or infer different type
    /// arguments. Every call around a reference must bind as it did.
    /// </summary>
    private static async Task EnsureCallsBindAsBeforeAsync(
        Solution annotated,
        Solution changed,
        Edits edits,
        CancellationToken cancellationToken)
    {
        foreach (var documentId in edits.Marks.Keys)
        {
            var before = annotated.GetDocument(documentId)!;
            var after = changed.GetDocument(documentId)!;
            var beforeRoot = await before.GetSyntaxRootAsync(cancellationToken);
            var afterRoot = await after.GetSyntaxRootAsync(cancellationToken);
            var beforeModel = await before.GetSemanticModelAsync(cancellationToken);
            var afterModel = await after.GetSemanticModelAsync(cancellationToken);

            var beforeCalls = beforeRoot!.GetAnnotatedNodes(UseKind).SelectMany(Calls);
            var afterCalls = afterRoot!.GetAnnotatedNodes(UseKind).SelectMany(Calls);
            foreach (var (was, now) in beforeCalls.Zip(afterCalls))
            {
                var wasBound = beforeModel!.GetSymbolInfo(was, cancellationToken).Symbol?.ToDisplayString();
                var nowBound = afterModel!.GetSymbolInfo(now, cancellationToken).Symbol?.ToDisplayString();
                if (wasBound != nowBound)
                    throw new McpException(
                        $"Error: The call at {SolutionEdits.Describe(now.GetLocation())} would bind to '{nowBound}' instead of '{wasBound}'");
            }
        }
    }

    private static IEnumerable<SyntaxNode> Calls(SyntaxNode node) =>
        node.DescendantNodesAndSelf().Where(n => n is InvocationExpressionSyntax or BaseObjectCreationExpressionSyntax);

    /// <summary>A nullable return type must not leave callers dereferencing what may now be null.</summary>
    private static async Task EnsureNoNullableWarningsAsync(Solution solution, Solution changed, CancellationToken cancellationToken)
    {
        var warnings = await SolutionEdits.NewDiagnosticsAsync(
            solution,
            changed,
            d => d.Id.StartsWith("CS86", StringComparison.Ordinal),
            cancellationToken);
        if (warnings.Count > 0)
            throw new McpException(
                $"Error: The new return type would give callers nullable warnings: {SolutionEdits.Describe(warnings[0])}");
    }

    private static async Task<List<(string Path, TextSpan Span)>> MarkedSpansAsync(
        Solution solution,
        string kind,
        CancellationToken cancellationToken)
    {
        var spans = new List<(string, TextSpan)>();
        foreach (var document in solution.Projects.SelectMany(p => p.Documents))
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            spans.AddRange(root!.GetAnnotatedNodes(kind).Select(n => (document.FilePath ?? "", n.Span)));
        }

        return spans;
    }

    private static bool Inside(Diagnostic diagnostic, List<(string Path, TextSpan Span)> spans) =>
        diagnostic.Location.IsInSource
        && spans.Any(s => s.Path == diagnostic.Location.SourceTree!.FilePath && s.Span.Contains(diagnostic.Location.SourceSpan));
}
