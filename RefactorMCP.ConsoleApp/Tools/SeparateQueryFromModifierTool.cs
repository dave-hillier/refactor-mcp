using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

[McpServerToolType]
public static class SeparateQueryFromModifierTool
{
    [McpServerTool, Description("Split a method that changes state and returns a value into a modifier that makes the change and a query that returns the value, and make every caller call both")]
    public static async Task<string> SeparateQueryFromModifier(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file declaring the method")] string filePath,
        [Description("Name of the method")] string methodName,
        [Description("Name of the query that returns the value")] string queryName,
        [Description("Name of the modifier that changes state")] string modifierName,
        [Description("A line of the method's declaration (1-based), to choose between overloads")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var method = await SolutionEdits.FindMethodAsync(solution, filePath, methodName, line, cancellationToken);
            var declaration = (await method.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken)) as MethodDeclarationSyntax
                ?? throw new McpException($"Error: '{methodName}' is not a method");
            EnsureSeparable(method, declaration);
            var shape = Shape.Of(declaration);

            var family = new[] { method.OriginalDefinition };
            foreach (var site in await SignatureChange.CallSitesAsync(solution, family, cancellationToken))
                EnsureCallCanBeSplit(site, shape);

            // Extract the modifier, then the query, as Extract Method does. Each new method
            // follows the original, so the query ends up first.
            var mark = new SyntaxAnnotation();
            var document = solution.GetDocument(declaration.SyntaxTree)!;
            var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
            document = document.WithSyntaxRoot(root.ReplaceNode(declaration, declaration.WithAdditionalAnnotations(mark)));
            document = await ExtractAsync(document, mark, d => Shape.Of(d).ModifierSpan, modifierName);
            document = await ExtractAsync(document, mark, d => Shape.Of(d).Query.Span, queryName);
            document = document.WithSyntaxRoot(GiveAccessibility((await document.GetSyntaxRootAsync(cancellationToken))!, mark, queryName, modifierName));

            var extracted = document.Project.Solution;
            var original = await SolutionEdits.ResolveAsync(extracted, document.Project.Id, method.OriginalDefinition, cancellationToken);
            var changed = await RedirectCallersAsync(extracted, original, mark, queryName, modifierName, shape.QueryFirst, cancellationToken);

            await SolutionEdits.EnsureCompilesAsync(solution, changed, cancellationToken);
            await SolutionEdits.WriteAsync(solution, changed, cancellationToken);
            return $"Successfully separated '{methodName}' into the query '{queryName}' and the modifier '{modifierName}'";
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error separating query from modifier: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// The two parts of the method's body. Either statements change state and the
    /// method then returns an expression computed afterwards, or it first stores the
    /// value in a local, changes state, and returns the local.
    /// </summary>
    private sealed record Shape(ExpressionSyntax Query, IReadOnlyList<StatementSyntax> Modifier, bool QueryFirst)
    {
        public TextSpan ModifierSpan => TextSpan.FromBounds(Modifier[0].SpanStart, Modifier[^1].Span.End);

        public static Shape Of(MethodDeclarationSyntax method)
        {
            var statements = method.Body!.Statements;
            if (statements.LastOrDefault() is not ReturnStatementSyntax { Expression: { } returned })
                throw new McpException($"Error: '{method.Identifier.ValueText}' does not end by returning its value");

            var early = statements.Take(statements.Count - 1)
                .SelectMany(s => s.DescendantNodesAndSelf(n => n is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax))
                .OfType<ReturnStatementSyntax>()
                .Any();
            if (early)
                throw new McpException($"Error: '{method.Identifier.ValueText}' returns before its last statement, so its query and modifier cannot be told apart");

            if (statements.Count > 2 &&
                statements[0] is LocalDeclarationStatementSyntax { Declaration.Variables: [{ Initializer: { } initializer } local] } &&
                returned is IdentifierNameSyntax name && name.Identifier.ValueText == local.Identifier.ValueText)
            {
                var modifier = statements.Skip(1).Take(statements.Count - 2).ToList();
                var readsResult = modifier.SelectMany(s => s.DescendantNodes()).OfType<IdentifierNameSyntax>()
                    .Any(i => i.Identifier.ValueText == local.Identifier.ValueText);
                if (readsResult)
                    throw new McpException($"Error: The statements that change state read '{local.Identifier.ValueText}', the value the query returns");

                return Checked(new Shape(initializer.Value, modifier, QueryFirst: true));
            }

            if (statements.Count < 2)
                throw new McpException($"Error: '{method.Identifier.ValueText}' has no statements that change state before it returns");

            return Checked(new Shape(returned, statements.Take(statements.Count - 1).ToList(), QueryFirst: false));
        }

        private static Shape Checked(Shape shape) =>
            ExpressionFacts.HasSideEffects(shape.Query)
                ? throw new McpException("Error: The value returned has side effects of its own, so it cannot become a query")
                : shape;
    }

    private static void EnsureSeparable(IMethodSymbol method, MethodDeclarationSyntax declaration)
    {
        if (method.ReturnsVoid)
            throw new McpException($"Error: '{method.Name}' returns nothing, so it has no query to separate");
        if (method.IsVirtual || method.IsAbstract || method.IsOverride || method.ExplicitInterfaceImplementations.Length > 0
            || method.ContainingType.AllInterfaces.SelectMany(i => i.GetMembers().OfType<IMethodSymbol>())
                .Any(m => SymbolEqualityComparer.Default.Equals(method.ContainingType.FindImplementationForInterfaceMember(m), method)))
            throw new McpException($"Error: '{method.Name}' is virtual, an override or an interface implementation, so callers may run other code");
        if (method.IsAsync || declaration.DescendantNodes().Any(n => n is YieldStatementSyntax))
            throw new McpException($"Error: '{method.Name}' is async or an iterator");
        if (declaration.Body == null)
            throw new McpException($"Error: '{method.Name}' is expression-bodied, so it has no statements that change state");
        if (declaration.Parent is not ClassDeclarationSyntax)
            throw new McpException($"Error: '{method.Name}' is not declared in a class");
    }

    /// <summary>
    /// A call is replaced by a call of each part, so it must start its statement, and
    /// its receiver and arguments are evaluated twice, so they must have no side effects.
    /// </summary>
    private static void EnsureCallCanBeSplit(SignatureCallSite site, Shape shape)
    {
        var where = SolutionEdits.Describe(site.Call.GetLocation());
        var host = Host(site.Call)
            ?? throw new McpException($"Error: The call at {where} is part of a larger expression, so it cannot become two calls");
        if (shape.QueryFirst && host is ReturnStatementSyntax)
            throw new McpException($"Error: The call at {where} returns the value, which the query must compute before the modifier runs");

        var invocation = (InvocationExpressionSyntax)site.Call;
        if (invocation.Expression is MemberAccessExpressionSyntax access && !ExpressionFacts.IsSimple(access.Expression))
            throw new McpException($"Error: The call at {where} is made on an expression that would be evaluated twice");
        if (invocation.ArgumentList.Arguments.Any(a => ExpressionFacts.HasSideEffects(a.Expression)))
            throw new McpException($"Error: The call at {where} passes an argument with side effects, which would be evaluated twice");
    }

    /// <summary>The statement a call starts, or null when anything else runs before it.</summary>
    private static StatementSyntax? Host(SyntaxNode call) => call.Parent switch
    {
        ExpressionStatementSyntax statement => statement,
        EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax { Variables.Count: 1, Parent: LocalDeclarationStatementSyntax declaration } } } => declaration,
        AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.SimpleAssignmentExpression, Parent: ExpressionStatementSyntax statement } assignment
            when assignment.Right == call && ExpressionFacts.IsSimple(assignment.Left) => statement,
        ReturnStatementSyntax statement => statement,
        _ => null,
    };

    private static async Task<Document> ExtractAsync(
        Document document,
        SyntaxAnnotation mark,
        Func<MethodDeclarationSyntax, TextSpan> part,
        string name)
    {
        var root = (await document.GetSyntaxRootAsync())!;
        var method = (MethodDeclarationSyntax)root.GetAnnotatedNodes(mark).Single();
        return document.WithSyntaxRoot(await ExtractMethodTool.ExtractAsync(document, part(method), name));
    }

    /// <summary>The query and modifier are called wherever the method was, so they take its accessibility.</summary>
    private static SyntaxNode GiveAccessibility(SyntaxNode root, SyntaxAnnotation mark, params string[] names) =>
        MemberAccessibility.CopyTo(root, (MethodDeclarationSyntax)root.GetAnnotatedNodes(mark).Single(), names);

    /// <summary>
    /// Replaces each call of the original method with a call of the modifier and a call
    /// of the query, in the order the method ran them, then deletes the method.
    /// </summary>
    private static async Task<Solution> RedirectCallersAsync(
        Solution solution,
        IMethodSymbol original,
        SyntaxAnnotation mark,
        string queryName,
        string modifierName,
        bool queryFirst,
        CancellationToken cancellationToken)
    {
        var declaration = (MethodDeclarationSyntax)await original.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken);
        var members = ((ClassDeclarationSyntax)declaration.Parent!).Members.OfType<MethodDeclarationSyntax>().ToList();
        var query = members.Single(m => m.Identifier.ValueText == queryName);
        var modifier = members.Single(m => m.Identifier.ValueText == modifierName);

        var editors = new Dictionary<DocumentId, DocumentEditor>();
        async Task<DocumentEditor> EditorFor(DocumentId id)
        {
            if (!editors.TryGetValue(id, out var editor))
                editors[id] = editor = await DocumentEditor.CreateAsync(solution.GetDocument(id)!, cancellationToken);
            return editor;
        }

        foreach (var site in await SignatureChange.CallSitesAsync(solution, new[] { original }, cancellationToken))
        {
            var host = Host(site.Call)!;
            var modifierCall = SyntaxFactory.ExpressionStatement(CallOf(modifier, original, site));
            var replacement = new List<StatementSyntax>();
            if (host is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax })
            {
                replacement.Add(modifierCall);
            }
            else
            {
                var withQuery = host.ReplaceNode(site.Call, CallOf(query, original, site).WithTriviaFrom(site.Call));
                replacement.AddRange(queryFirst
                    ? new[] { withQuery, modifierCall }
                    : new StatementSyntax[] { modifierCall, withQuery });
            }

            var editor = await EditorFor(site.Document.Id);
            Replace(editor, host, replacement);
        }

        var declaringEditor = await EditorFor(solution.GetDocument(declaration.SyntaxTree)!.Id);
        declaringEditor.RemoveNode(declaration, SyntaxRemoveOptions.KeepNoTrivia);

        var changed = solution;
        foreach (var (id, editor) in editors)
        {
            var document = changed.GetDocument(id)!.WithSyntaxRoot(editor.GetChangedRoot());
            document = await Formatter.FormatAsync(document, Formatter.Annotation, cancellationToken: cancellationToken);
            changed = document.Project.Solution;
        }

        return changed;
    }

    /// <summary>A call of one part, on the original call's receiver, passing what it passed for the part's parameters.</summary>
    private static InvocationExpressionSyntax CallOf(MethodDeclarationSyntax part, IMethodSymbol original, SignatureCallSite site)
    {
        var invocation = (InvocationExpressionSyntax)site.Call;
        var name = SyntaxFactory.IdentifierName(part.Identifier.ValueText);
        ExpressionSyntax target = invocation.Expression is MemberAccessExpressionSyntax access
            ? access.WithName(name)
            : name;

        var arguments = part.ParameterList.Parameters.Select(p =>
        {
            var parameter = original.Parameters.First(o => o.Name == p.Identifier.ValueText);
            var passed = site.ArgumentFor(parameter.Ordinal)
                ?? SyntaxFactory.ParseExpression(((ParameterSyntax)parameter.DeclaringSyntaxReferences[0].GetSyntax()).Default!.Value.ToString());
            return SyntaxFactory.Argument(passed.WithoutTrivia());
        });

        return SyntaxFactory.InvocationExpression(
            target.WithoutTrivia(),
            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments)));
    }

    /// <summary>
    /// Puts the statements where the call's statement was, keeping its comments; a
    /// statement that was the body of an if or loop without braces gets a block.
    /// </summary>
    private static void Replace(SyntaxEditor editor, StatementSyntax host, List<StatementSyntax> statements)
    {
        statements = statements
            .Select(s => s
                .WithLeadingTrivia(SyntaxFactory.ElasticMarker)
                .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed)
                .WithAdditionalAnnotations(Formatter.Annotation))
            .ToList();
        statements[0] = statements[0].WithLeadingTrivia(host.GetLeadingTrivia());
        statements[^1] = statements[^1].WithTrailingTrivia(host.GetTrailingTrivia());

        if (host.Parent is BlockSyntax or SwitchSectionSyntax)
        {
            editor.InsertBefore(host, statements);
            editor.RemoveNode(host, SyntaxRemoveOptions.KeepNoTrivia);
            return;
        }

        editor.ReplaceNode(host, SyntaxFactory.Block(statements).WithTriviaFrom(host).WithAdditionalAnnotations(Formatter.Annotation));
    }
}
