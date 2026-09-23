using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

/// <summary>A value a method dispatches on, and the method that takes over its branch.</summary>
public sealed class ExplicitMethod
{
    [Description("The value as a C# expression, such as \"height\" or Zone.Europe")]
    public string Value { get; set; } = "";

    [Description("Name of the method for that value")]
    public string Name { get; set; } = "";
}

[McpServerToolType]
public static class ReplaceParameterWithExplicitMethodsTool
{
    [McpServerTool, Description("Give each value a method dispatches on a method of its own, and make calls that pass that value as a constant call it directly")]
    public static async Task<string> ReplaceParameterWithExplicitMethods(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file declaring the method")] string filePath,
        [Description("Name of the method")] string methodName,
        [Description("The parameter the method dispatches on")] string parameterName,
        [Description("The values to give methods of their own, each with the method's name")] ExplicitMethod[] methods,
        [Description("A line of the method's declaration (1-based), to choose between overloads")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var method = (await SolutionEdits.FindMethodAsync(solution, filePath, methodName, line, cancellationToken)).OriginalDefinition;
            var declaration = (await method.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken)) as MethodDeclarationSyntax;
            if (declaration?.Body == null || declaration.Parent is not ClassDeclarationSyntax)
                throw new McpException($"Error: '{methodName}' is not a block-bodied method of a class");
            if (method.IsVirtual || method.IsAbstract || method.IsOverride || method.ExplicitInterfaceImplementations.Length > 0
                || method.ContainingType.AllInterfaces.SelectMany(i => i.GetMembers().OfType<IMethodSymbol>())
                    .Any(m => SymbolEqualityComparer.Default.Equals(method.ContainingType.FindImplementationForInterfaceMember(m), method)))
                throw new McpException($"Error: '{methodName}' is virtual, an override or an interface implementation, so callers may run other code");

            var parameter = SolutionEdits.FindParameter(method, parameterName);
            var document = solution.GetDocument(declaration.SyntaxTree)!;
            var model = (await document.GetSemanticModelAsync(cancellationToken))!;
            var requested = Requested(Dispatch.Of(declaration, parameter, model), methods);

            // Extract each branch, as Extract Method does, from the last so the new
            // methods follow the original in the order of their values.
            var mark = new SyntaxAnnotation();
            var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
            document = document.WithSyntaxRoot(root.ReplaceNode(declaration, declaration.WithAdditionalAnnotations(mark)));
            foreach (var (value, name) in Enumerable.Reverse(requested))
            {
                root = (await document.GetSyntaxRootAsync(cancellationToken))!;
                var annotated = (MethodDeclarationSyntax)root.GetAnnotatedNodes(mark).Single();
                var branch = Dispatch.Of(annotated, parameter, (await document.GetSemanticModelAsync(cancellationToken))!)
                    .First(b => Equals(b.Value, value));
                document = document.WithSyntaxRoot(await ExtractMethodTool.ExtractAsync(document, branch.Span, name));
            }

            root = (await document.GetSyntaxRootAsync(cancellationToken))!;
            document = document.WithSyntaxRoot(MemberAccessibility.CopyTo(
                root, (MethodDeclarationSyntax)root.GetAnnotatedNodes(mark).Single(), requested.Select(r => r.Name).ToList()));

            var extracted = document.Project.Solution;
            var original = await SolutionEdits.ResolveAsync(extracted, document.Project.Id, method, cancellationToken);
            var changed = await RedirectCallsAsync(extracted, original, mark, parameter.Ordinal, requested, cancellationToken);

            await SolutionEdits.EnsureCompilesAsync(solution, changed, cancellationToken);
            await SolutionEdits.WriteAsync(solution, changed, cancellationToken);
            return $"Successfully gave {string.Join(", ", requested.Select(r => r.Name))} the branches of '{methodName}'";
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error replacing parameter with explicit methods: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// A branch of the dispatch: the constant it runs for, the statements it runs, and
    /// why it cannot have a method of its own, if it cannot.
    /// </summary>
    private sealed record Branch(object? Value, string Text, TextSpan Span, string? Problem);

    /// <summary>The constant value of each requested value, with its method's name, in dispatch order.</summary>
    private static List<(object? Value, string Name)> Requested(IReadOnlyList<Branch> branches, ExplicitMethod[] methods)
    {
        if (methods.Length == 0)
            throw new McpException("Error: Name at least one value to give a method of its own");

        var requested = new List<(Branch Branch, string Name)>();
        foreach (var explicitMethod in methods)
        {
            var value = SyntaxFactory.ParseExpression(explicitMethod.Value);
            var branch = branches.FirstOrDefault(b => SyntaxFactory.AreEquivalent(SyntaxFactory.ParseExpression(b.Text), value))
                ?? throw new McpException($"Error: The method does not dispatch on the value {explicitMethod.Value}");
            if (branch.Problem != null)
                throw new McpException(branch.Problem);
            requested.Add((branch, explicitMethod.Name));
        }

        return requested
            .OrderBy(r => r.Branch.Span.Start)
            .Select(r => (r.Branch.Value, r.Name))
            .ToList();
    }

    /// <summary>
    /// How the method chooses what to run: from the start of its body, a switch on the
    /// parameter, or a run of if statements and else-if chains comparing it with a
    /// constant. A branch must end the method: it returns or throws, or nothing
    /// follows the dispatch.
    /// </summary>
    private static class Dispatch
    {
        public static IReadOnlyList<Branch> Of(MethodDeclarationSyntax method, IParameterSymbol dispatchedOn, SemanticModel model)
        {
            // The parameter as this model knows it, which may be a later version of the
            // solution than the one it came from.
            var parameter = model.GetDeclaredSymbol(method)!.Parameters[dispatchedOn.Ordinal];
            var statements = method.Body!.Statements;
            var branches = new List<(ExpressionSyntax Constant, IReadOnlyList<StatementSyntax> Statements, bool IsLast)>();

            if (statements.FirstOrDefault() is SwitchStatementSyntax @switch && IsParameter(@switch.Expression, parameter, model))
            {
                foreach (var section in @switch.Sections)
                {
                    if (section.Labels is [CaseSwitchLabelSyntax label] && model.GetConstantValue(label.Value).HasValue)
                        branches.Add((label.Value, section.Statements, statements.Count == 1));
                }
            }
            else
            {
                for (var i = 0; i < statements.Count && statements[i] is IfStatementSyntax; i++)
                {
                    for (var current = (IfStatementSyntax?)statements[i]; current != null; current = current.Else?.Statement as IfStatementSyntax)
                    {
                        var constant = ComparedConstant(current.Condition, parameter, model);
                        if (constant == null)
                            break;

                        var branch = current.Statement is BlockSyntax block ? (IReadOnlyList<StatementSyntax>)block.Statements : new[] { current.Statement };
                        branches.Add((constant, branch, i == statements.Count - 1));
                    }
                }
            }

            if (branches.Count == 0)
                throw new McpException($"Error: '{method.Identifier.ValueText}' does not start by choosing what to do from '{parameter.Name}' compared with constants");

            return branches.Select(b => ToBranch(b.Constant, b.Statements, b.IsLast, model)).ToList();
        }

        private static Branch ToBranch(ExpressionSyntax constant, IReadOnlyList<StatementSyntax> statements, bool isLast, SemanticModel model)
        {
            var value = model.GetConstantValue(constant).Value;
            var body = statements.Where(s => s is not BreakStatementSyntax).ToList();
            if (body.Count == 0 || body is [ReturnStatementSyntax { Expression: null }])
                return new Branch(value, constant.ToString(), default, $"Error: The branch for {constant} is empty, so there is nothing to give a method");

            var exits = statements[^1] is ReturnStatementSyntax or ThrowStatementSyntax;
            var problem = exits || isLast
                ? null
                : $"Error: The branch for {constant} runs on into the rest of the method, which its own method would not run";

            var span = TextSpan.FromBounds(statements[0].SpanStart, statements[^1].Span.End);
            return new Branch(value, constant.ToString(), span, problem);
        }

        private static ExpressionSyntax? ComparedConstant(ExpressionSyntax condition, IParameterSymbol parameter, SemanticModel model)
        {
            if (condition is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.EqualsExpression } comparison)
                return null;

            var constant = IsParameter(comparison.Left, parameter, model) ? comparison.Right
                : IsParameter(comparison.Right, parameter, model) ? comparison.Left
                : null;
            return constant != null && model.GetConstantValue(constant).HasValue ? constant : null;
        }

        private static bool IsParameter(ExpressionSyntax expression, IParameterSymbol parameter, SemanticModel model) =>
            expression is IdentifierNameSyntax name &&
            model.GetSymbolInfo(name).Symbol is IParameterSymbol symbol &&
            symbol.Ordinal == parameter.Ordinal &&
            SymbolEqualityComparer.Default.Equals(symbol.ContainingSymbol.OriginalDefinition, parameter.ContainingSymbol.OriginalDefinition);
    }

    /// <summary>
    /// Points each call that passes one of the values as a constant at that value's
    /// method, passing the arguments for the parameters it takes. A call whose
    /// arguments have side effects keeps calling the method, since some of them would
    /// no longer be evaluated. The method is deleted once nothing refers to it.
    /// </summary>
    private static async Task<Solution> RedirectCallsAsync(
        Solution solution,
        IMethodSymbol original,
        SyntaxAnnotation mark,
        int ordinal,
        IReadOnlyList<(object? Value, string Name)> requested,
        CancellationToken cancellationToken)
    {
        var declaration = (MethodDeclarationSyntax)await original.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken);
        var explicitMethods = ((ClassDeclarationSyntax)declaration.Parent!).Members.OfType<MethodDeclarationSyntax>()
            .Where(m => requested.Any(r => r.Name == m.Identifier.ValueText))
            .ToDictionary(m => m.Identifier.ValueText);

        var edits = new Dictionary<DocumentId, Dictionary<SyntaxNode, SyntaxNode>>();
        var references = 0;
        var redirected = 0;
        var locations = (await SymbolFinder.FindReferencesAsync(original, solution, cancellationToken))
            .SelectMany(r => r.Locations)
            .Where(l => l.Location.IsInSource);
        foreach (var location in locations)
        {
            references++;
            var document = location.Document;
            var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
            var model = (await document.GetSemanticModelAsync(cancellationToken))!;
            var invocation = root.FindNode(location.Location.SourceSpan).AncestorsAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault();
            if (invocation == null || model.GetOperation(invocation, cancellationToken) is not IInvocationOperation operation
                || !SymbolEqualityComparer.Default.Equals(operation.TargetMethod.OriginalDefinition, original))
                continue;

            var passed = operation.Arguments.FirstOrDefault(a => a.Parameter?.Ordinal == ordinal);
            if (passed == null || passed.ArgumentKind != ArgumentKind.Explicit || !passed.Value.ConstantValue.HasValue)
                continue;
            var target = requested.FirstOrDefault(r => Equals(r.Value, passed.Value.ConstantValue.Value));
            if (target.Name == null || invocation.ArgumentList.Arguments.Any(a => ExpressionFacts.HasSideEffects(a.Expression)))
                continue;

            var call = ExplicitCall(invocation, explicitMethods[target.Name], operation, original);
            if (!edits.TryGetValue(document.Id, out var nodes))
                edits[document.Id] = nodes = new Dictionary<SyntaxNode, SyntaxNode>();
            nodes[invocation] = call.WithTriviaFrom(invocation);
            redirected++;
        }

        var changed = solution;
        foreach (var (documentId, nodes) in edits)
        {
            var root = (await changed.GetDocument(documentId)!.GetSyntaxRootAsync(cancellationToken))!;
            changed = changed.WithDocumentSyntaxRoot(documentId, root.ReplaceNodes(nodes.Keys, (o, _) => nodes[o]));
        }

        if (redirected == references)
        {
            var documentId = solution.GetDocumentId(declaration.SyntaxTree)!;
            var root = (await changed.GetDocument(documentId)!.GetSyntaxRootAsync(cancellationToken))!;
            changed = changed.WithDocumentSyntaxRoot(documentId, RemoveMethod(root, mark));
        }

        return changed;
    }

    /// <summary>
    /// Deletes the marked method with its comments. When it was the first member, the
    /// blank line that set the next member apart goes too.
    /// </summary>
    private static SyntaxNode RemoveMethod(SyntaxNode root, SyntaxAnnotation mark)
    {
        var method = (MemberDeclarationSyntax)root.GetAnnotatedNodes(mark).Single();
        var type = (TypeDeclarationSyntax)method.Parent!;
        if (type.Members.IndexOf(method) == 0 && type.Members.Count > 1 &&
            type.Members[1].GetLeadingTrivia().FirstOrDefault().IsKind(SyntaxKind.EndOfLineTrivia))
        {
            var next = type.Members[1];
            root = root.ReplaceNode(next, next.WithLeadingTrivia(next.GetLeadingTrivia().RemoveAt(0)));
            method = (MemberDeclarationSyntax)root.GetAnnotatedNodes(mark).Single();
        }

        return root.RemoveNode(method, SyntaxRemoveOptions.KeepNoTrivia)!;
    }

    /// <summary>A call of the explicit method on the call's receiver, passing what the call passed for its parameters.</summary>
    private static InvocationExpressionSyntax ExplicitCall(
        InvocationExpressionSyntax invocation,
        MethodDeclarationSyntax explicitMethod,
        IInvocationOperation operation,
        IMethodSymbol original)
    {
        var name = SyntaxFactory.IdentifierName(explicitMethod.Identifier.ValueText);
        ExpressionSyntax target = invocation.Expression is MemberAccessExpressionSyntax access ? access.WithName(name) : name;
        var arguments = explicitMethod.ParameterList.Parameters.Select(p =>
        {
            var parameter = original.Parameters.First(o => o.Name == p.Identifier.ValueText);
            var argument = operation.Arguments.First(a => a.Parameter?.Ordinal == parameter.Ordinal);
            var value = argument.ArgumentKind == ArgumentKind.Explicit
                ? ((ArgumentSyntax)argument.Syntax).Expression
                : SyntaxFactory.ParseExpression(((ParameterSyntax)parameter.DeclaringSyntaxReferences[0].GetSyntax()).Default!.Value.ToString());
            return SyntaxFactory.Argument(value.WithoutTrivia());
        }).ToList();

        return SyntaxFactory.InvocationExpression(
            target.WithoutTrivia(),
            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(
                arguments,
                Enumerable.Repeat(SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space), Math.Max(0, arguments.Count - 1)))));
    }
}
