using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;

[McpServerToolType]
public static class ConvertToAsyncTool
{
    [McpServerTool, Description("Make a method that blocks on tasks async, awaiting them instead, and make its callers await it in turn, up to callers that cannot be async, which go on blocking")]
    public static async Task<string> ConvertToAsync(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the method")] string filePath,
        [Description("Name of the method")] string methodName,
        [Description("A line of the method's declaration (1-based), to choose between overloads")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var method = (await SolutionEdits.FindMethodAsync(solution, filePath, methodName, line, cancellationToken)).OriginalDefinition;
            var (converted, blocking) = await MakeAsyncAsync(solution, method, convertCallers: true, cancellationToken);
            return $"Successfully made '{methodName}' async, with {converted - 1} caller(s) made async and {blocking} call(s) left blocking";
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error converting to async: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Makes the method async and writes the change. With <paramref name="convertCallers"/>,
    /// each caller that can be async awaits the call and becomes async in turn; without it,
    /// only calls in async methods are awaited and every other call blocks on the task.
    /// Returns how many methods became async and how many calls block.
    /// </summary>
    internal static async Task<(int Converted, int Blocking)> MakeAsyncAsync(
        Solution solution,
        IMethodSymbol method,
        bool convertCallers,
        CancellationToken cancellationToken)
    {
        var plan = await Plan.MakeAsync(solution, method, convertCallers, cancellationToken);
        var changed = await plan.ApplyAsync(solution, cancellationToken);
        await SolutionEdits.EnsureCompilesAsync(solution, changed, cancellationToken);
        await SolutionEdits.WriteAsync(solution, changed, cancellationToken);
        return (plan.Converted.Count, plan.Blocking.Count);
    }

    /// <summary>
    /// Which methods become async and how each call of them changes. The method
    /// awaits the tasks it blocked on; a call in an async method is awaited; when
    /// callers are converted, each caller that can be async awaits the call and
    /// becomes async itself; any other call blocks on the task.
    /// </summary>
    private sealed class Plan
    {
        private Plan(IMethodSymbol method) => Method = method;

        public IMethodSymbol Method { get; }

        public List<IMethodSymbol> Converted { get; } = new();

        public List<InvocationExpressionSyntax> Awaited { get; } = new();

        public List<InvocationExpressionSyntax> Blocking { get; } = new();

        public List<ExpressionSyntax> Waits { get; } = new();

        public static async Task<Plan> MakeAsync(Solution solution, IMethodSymbol method, bool convertCallers, CancellationToken cancellationToken)
        {
            var plan = new Plan(method);
            var reason = await WhyNotAsyncAsync(solution, method, cancellationToken);
            if (reason != null)
                throw new McpException($"Error: '{method.Name}' {reason}");

            var declaration = (MethodDeclarationSyntax)await method.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken);
            var model = (await solution.GetDocument(declaration.SyntaxTree)!.GetSemanticModelAsync(cancellationToken))!;
            plan.Waits.AddRange(BlockingWaits(declaration, model));
            if (plan.Waits.Count == 0)
                throw new McpException($"Error: '{method.Name}' does not block on a task, so it has nothing to await");

            var pending = new Queue<IMethodSymbol>();
            pending.Enqueue(method);
            plan.Converted.Add(method);
            while (pending.Count > 0)
            {
                var converted = pending.Dequeue();
                foreach (var (invocation, caller) in await CallsAsync(solution, converted, cancellationToken))
                {
                    if (caller is null || NeedsBlocking(invocation))
                    {
                        plan.Blocking.Add(invocation);
                        continue;
                    }

                    if (caller.IsAsync || plan.Converted.Contains(caller, SymbolEqualityComparer.Default))
                    {
                        plan.Awaited.Add(invocation);
                        continue;
                    }

                    if (!convertCallers || await WhyNotAsyncAsync(solution, caller, cancellationToken) != null)
                    {
                        plan.Blocking.Add(invocation);
                        continue;
                    }

                    plan.Awaited.Add(invocation);
                    plan.Converted.Add(caller);
                    pending.Enqueue(caller);
                }
            }

            return plan;
        }

        /// <summary>Why the method cannot become async, or null when it can.</summary>
        private static async Task<string?> WhyNotAsyncAsync(Solution solution, IMethodSymbol method, CancellationToken cancellationToken)
        {
            if (method.MethodKind != MethodKind.Ordinary)
                return "is not an ordinary method";
            if (method.IsAsync || IsTask(method.ReturnType))
                return "already returns a task";
            if (method.IsVirtual || method.IsAbstract || method.IsOverride || method.ExplicitInterfaceImplementations.Length > 0
                || method.ContainingType.AllInterfaces.SelectMany(i => i.GetMembers().OfType<IMethodSymbol>())
                    .Any(m => SymbolEqualityComparer.Default.Equals(method.ContainingType.FindImplementationForInterfaceMember(m), method)))
                return "is virtual, an override or an interface implementation, so its signature is shared";
            if (method.Parameters.Any(p => p.RefKind != RefKind.None))
                return "has ref, out or in parameters, which an async method cannot have";
            if (method.ReturnsByRef || method.ReturnsByRefReadonly)
                return "returns by reference";

            if (method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(cancellationToken) is not MethodDeclarationSyntax declaration
                || (declaration.Body == null && declaration.ExpressionBody == null))
                return "has no body";
            if (declaration.DescendantNodes().Any(n => n is YieldStatementSyntax))
                return "is an iterator";

            var references = await SymbolFinder.FindReferencesAsync(method, solution, cancellationToken);
            foreach (var location in references.SelectMany(r => r.Locations).Where(l => l.Location.IsInSource))
            {
                var root = await location.Document.GetSyntaxRootAsync(cancellationToken);
                if (InvocationAt(root!.FindNode(location.Location.SourceSpan)) == null)
                    return $"is used as a method group at {SolutionEdits.Describe(location.Location)}, where a task-returning method would not fit";
            }

            return null;
        }

        /// <summary>Each call of the method, with the method it is in, or null when it is in no method.</summary>
        private static async Task<List<(InvocationExpressionSyntax Invocation, IMethodSymbol? Caller)>> CallsAsync(
            Solution solution,
            IMethodSymbol method,
            CancellationToken cancellationToken)
        {
            var calls = new List<(InvocationExpressionSyntax, IMethodSymbol?)>();
            var references = await SymbolFinder.FindReferencesAsync(method, solution, cancellationToken);
            foreach (var location in references.SelectMany(r => r.Locations).Where(l => l.Location.IsInSource))
            {
                var root = await location.Document.GetSyntaxRootAsync(cancellationToken);
                var model = await location.Document.GetSemanticModelAsync(cancellationToken);
                var invocation = InvocationAt(root!.FindNode(location.Location.SourceSpan))!;
                var member = invocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                var owner = invocation.Ancestors().FirstOrDefault(a => a is MemberDeclarationSyntax or AccessorDeclarationSyntax);
                var caller = member != null && owner == member ? model!.GetDeclaredSymbol(member, cancellationToken) : null;
                calls.Add((invocation, caller));
            }

            return calls;
        }

        private static InvocationExpressionSyntax? InvocationAt(SyntaxNode reference)
        {
            var name = reference as SimpleNameSyntax ?? reference.DescendantNodesAndSelf().OfType<SimpleNameSyntax>().FirstOrDefault();
            if (name == null)
                return null;

            var callee = name.Parent is MemberAccessExpressionSyntax access && access.Name == name ? access : (ExpressionSyntax)name;
            return callee.Parent is InvocationExpressionSyntax invocation && invocation.Expression == callee ? invocation : null;
        }

        /// <summary>
        /// A call in a lambda or local function, or in a lock statement, cannot be
        /// awaited by its method, so it goes on blocking.
        /// </summary>
        private static bool NeedsBlocking(InvocationExpressionSyntax invocation) =>
            invocation.Ancestors()
                .TakeWhile(a => a is not MethodDeclarationSyntax)
                .Any(a => a is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax or LockStatementSyntax or QueryExpressionSyntax);

        /// <summary>
        /// Where the method blocks on a task outside any lambda: <c>task.Result</c>,
        /// <c>task.Wait()</c> or <c>task.GetAwaiter().GetResult()</c>.
        /// </summary>
        private static IEnumerable<ExpressionSyntax> BlockingWaits(MethodDeclarationSyntax declaration, SemanticModel model)
        {
            var body = (SyntaxNode?)declaration.Body ?? declaration.ExpressionBody!;
            foreach (var node in body.DescendantNodes(n => n is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax and not LockStatementSyntax))
            {
                if (TaskWaitedOn(node, model) != null)
                    yield return (ExpressionSyntax)node;
            }
        }

        public async Task<Solution> ApplyAsync(Solution solution, CancellationToken cancellationToken)
        {
            var editors = new Dictionary<DocumentId, DocumentEditor>();
            async Task<DocumentEditor> EditorFor(SyntaxTree tree)
            {
                var document = solution.GetDocument(tree)!;
                if (!editors.TryGetValue(document.Id, out var editor))
                    editors[document.Id] = editor = await DocumentEditor.CreateAsync(document, cancellationToken);
                return editor;
            }

            foreach (var wait in Waits)
            {
                var statement = wait.Parent is ExpressionStatementSyntax;
                (await EditorFor(wait.SyntaxTree)).ReplaceNode(wait, (current, _) => Awaited(TaskWaitedOn(current)!, statement).WithTriviaFrom(current));
            }

            foreach (var invocation in Awaited)
            {
                var statement = invocation.Parent is ExpressionStatementSyntax;
                (await EditorFor(invocation.SyntaxTree)).ReplaceNode(invocation, (current, _) => Awaited((ExpressionSyntax)current, statement).WithTriviaFrom(current));
            }

            foreach (var invocation in Blocking)
                (await EditorFor(invocation.SyntaxTree)).ReplaceNode(invocation, (current, _) => Blocked((ExpressionSyntax)current).WithTriviaFrom(current));

            foreach (var converted in Converted)
            {
                var declaration = (MethodDeclarationSyntax)await converted.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken);
                (await EditorFor(declaration.SyntaxTree)).ReplaceNode(declaration, (current, _) => MadeAsync((MethodDeclarationSyntax)current));
            }

            var changed = solution;
            foreach (var (id, editor) in editors)
            {
                var document = changed.GetDocument(id)!.WithSyntaxRoot(editor.GetChangedRoot());
                document = await ImportAdder.AddImportsAsync(document, Simplifier.AddImportsAnnotation, cancellationToken: cancellationToken);
                document = await Simplifier.ReduceAsync(document, Simplifier.Annotation, cancellationToken: cancellationToken);
                document = await Formatter.FormatAsync(document, Formatter.Annotation, cancellationToken: cancellationToken);
                changed = document.Project.Solution;
            }

            return changed;
        }
    }

    /// <summary>The task a blocking wait waits on, or null when the node is not one.</summary>
    private static ExpressionSyntax? TaskWaitedOn(SyntaxNode node, SemanticModel model)
    {
        var task = TaskWaitedOn(node);
        return task != null && IsTask(model.GetTypeInfo(task).Type) ? task : null;
    }

    private static ExpressionSyntax? TaskWaitedOn(SyntaxNode node) => node switch
    {
        MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Result" } access
            when node.Parent is not InvocationExpressionSyntax => access.Expression,
        InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Wait" } wait,
            ArgumentList.Arguments.Count: 0,
        } => wait.Expression,
        InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax
            {
                Name.Identifier.ValueText: "GetResult",
                Expression: InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "GetAwaiter" } awaiter,
                    ArgumentList.Arguments.Count: 0,
                },
            },
            ArgumentList.Arguments.Count: 0,
        } => awaiter.Expression,
        _ => null,
    };

    private static bool IsTask(ITypeSymbol? type) =>
        type is INamedTypeSymbol { Name: "Task" or "ValueTask" } named &&
        named.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks";

    /// <summary>
    /// <c>await</c> on the expression. Inside another expression it is parenthesised for
    /// the simplifier to unwrap where precedence allows; a statement of its own needs none.
    /// </summary>
    private static ExpressionSyntax Awaited(ExpressionSyntax task, bool isStatement)
    {
        var awaited = SyntaxFactory.AwaitExpression(
            SyntaxFactory.Token(SyntaxKind.AwaitKeyword).WithTrailingTrivia(SyntaxFactory.Space),
            task.WithoutTrivia());
        return isStatement
            ? awaited
            : SyntaxFactory.ParenthesizedExpression(awaited).WithAdditionalAnnotations(Simplifier.Annotation);
    }

    private static ExpressionSyntax Blocked(ExpressionSyntax task) =>
        SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    task.WithoutTrivia(),
                    SyntaxFactory.IdentifierName("GetAwaiter"))),
                SyntaxFactory.IdentifierName("GetResult")));

    /// <summary>The method marked async and returning a task of what it returned.</summary>
    private static MethodDeclarationSyntax MadeAsync(MethodDeclarationSyntax method)
    {
        var tasks = SyntaxFactory.ParseName("global::System.Threading.Tasks");
        TypeSyntax returnType = method.ReturnType is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.VoidKeyword }
            ? SyntaxFactory.QualifiedName(tasks, SyntaxFactory.IdentifierName("Task"))
            : SyntaxFactory.QualifiedName(tasks, SyntaxFactory.GenericName(SyntaxFactory.Identifier("Task"),
                SyntaxFactory.TypeArgumentList(SyntaxFactory.SingletonSeparatedList(method.ReturnType.WithoutTrivia()))));
        returnType = returnType
            .WithTriviaFrom(method.ReturnType)
            .WithAdditionalAnnotations(Simplifier.Annotation, Simplifier.AddImportsAnnotation);

        var asyncKeyword = SyntaxFactory.Token(SyntaxKind.AsyncKeyword).WithTrailingTrivia(SyntaxFactory.Space);
        if (method.Modifiers.Count == 0)
        {
            return method
                .WithModifiers(SyntaxFactory.TokenList(asyncKeyword.WithLeadingTrivia(method.ReturnType.GetLeadingTrivia())))
                .WithReturnType(returnType.WithLeadingTrivia());
        }

        return method.WithModifiers(method.Modifiers.Add(asyncKeyword)).WithReturnType(returnType);
    }
}
