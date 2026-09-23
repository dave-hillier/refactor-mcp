using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Simplification;
using System.Threading;

/// <summary>
/// Inlines every call of a method across the solution and deletes the method. Every
/// call is checked and rewritten before anything is written, so a refusal changes
/// nothing.
/// </summary>
internal sealed class MethodInliner
{
    private readonly MethodDeclarationSyntax _method;
    private readonly IMethodSymbol _symbol;
    private readonly SemanticModel _model;

    // A method that produces a value is inlined as its one expression; a void method
    // as its statements.
    private readonly ExpressionSyntax? _expression;
    private readonly IReadOnlyList<StatementSyntax> _statements;

    private MethodInliner(MethodDeclarationSyntax method, IMethodSymbol symbol, SemanticModel model)
    {
        _method = method;
        _symbol = symbol;
        _model = model;
        EnsureInlinable();
        (_expression, _statements) = Body();
    }

    public static async Task<int> InlineAsync(Document document, MethodDeclarationSyntax method, CancellationToken cancellationToken)
    {
        var model = (await document.GetSemanticModelAsync(cancellationToken))!;
        var symbol = model.GetDeclaredSymbol(method, cancellationToken)!;
        var inliner = new MethodInliner(method, symbol, model);

        var solution = document.Project.Solution;
        var references = await SymbolFinder.FindReferencesAsync(symbol, solution, cancellationToken);
        var locations = references
            .SelectMany(r => r.Locations)
            .Where(l => l.Location.IsInSource)
            .GroupBy(l => l.Document.Id);

        var editors = new Dictionary<DocumentId, DocumentEditor>();
        var calls = 0;
        foreach (var group in locations)
        {
            var callDocument = solution.GetDocument(group.Key)!;
            var root = (await callDocument.GetSyntaxRootAsync(cancellationToken))!;
            var callModel = (await callDocument.GetSemanticModelAsync(cancellationToken))!;
            var editor = await DocumentEditor.CreateAsync(callDocument, cancellationToken);

            // Innermost first, so a call nested in another's arguments is already
            // inlined when the outer call is rebuilt from its current arguments.
            var invocations = group
                .Select(l => inliner.InvocationAt(root.FindNode(l.Location.SourceSpan, getInnermostNodeForTie: true)))
                .OrderByDescending(i => i.SpanStart);
            foreach (var invocation in invocations)
            {
                inliner.InlineCall(invocation, callModel, editor);
                calls++;
            }

            editors[callDocument.Id] = editor;
        }

        if (!editors.TryGetValue(document.Id, out var declaringEditor))
        {
            declaringEditor = await DocumentEditor.CreateAsync(document, cancellationToken);
            editors[document.Id] = declaringEditor;
        }

        RemoveMethod(declaringEditor, method);

        foreach (var (id, editor) in editors)
        {
            var changed = solution.GetDocument(id)!.WithSyntaxRoot(editor.GetChangedRoot());
            changed = await Simplifier.ReduceAsync(changed, Simplifier.Annotation, cancellationToken: cancellationToken);
            changed = await Formatter.FormatAsync(changed, Formatter.Annotation, cancellationToken: cancellationToken);
            await RefactoringHelpers.WriteAndUpdateCachesAsync(solution.GetDocument(id)!, (await changed.GetSyntaxRootAsync(cancellationToken))!);
        }

        return calls;
    }

    private string Name => _symbol.Name;

    private (ExpressionSyntax? Expression, IReadOnlyList<StatementSyntax> Statements) Body()
    {
        if (_method.ExpressionBody is { } arrow)
        {
            return _symbol.ReturnsVoid
                ? (null, new StatementSyntax[] { SyntaxFactory.ExpressionStatement(arrow.Expression) })
                : (arrow.Expression, Array.Empty<StatementSyntax>());
        }

        if (_method.Body is not { } body)
            throw new McpException($"Error: '{Name}' has no body to inline");

        if (!_symbol.ReturnsVoid)
        {
            if (body.Statements is [ReturnStatementSyntax { Expression: { } returned }])
                return (returned, Array.Empty<StatementSyntax>());

            throw new McpException(
                $"Error: '{Name}' computes its result in several statements, so it cannot be inlined into an expression");
        }

        // A return at the very end only ends the method; one anywhere else would return
        // from the caller once inlined.
        var statements = body.Statements.ToList();
        if (statements.LastOrDefault() is ReturnStatementSyntax)
            statements.RemoveAt(statements.Count - 1);

        var earlyReturn = statements
            .SelectMany(s => s.DescendantNodesAndSelf(n => n is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax))
            .OfType<ReturnStatementSyntax>()
            .FirstOrDefault();
        if (earlyReturn != null)
            throw new McpException($"Error: '{Name}' returns before its last statement, which would return from the caller once inlined");

        return (null, statements);
    }

    private void EnsureInlinable()
    {
        if (_symbol.IsVirtual || _symbol.IsAbstract || _symbol.IsOverride || ImplementsInterfaceMember())
        {
            throw new McpException(
                $"Error: '{Name}' is virtual, an override or an interface implementation, so a call to it may run other code");
        }

        if (_symbol.IsAsync || _method.DescendantNodes().Any(n => n is YieldStatementSyntax))
            throw new McpException($"Error: '{Name}' is async or an iterator, which cannot be inlined");

        var recursive = _method.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(i => SymbolEqualityComparer.Default.Equals(_model.GetSymbolInfo(i).Symbol?.OriginalDefinition, _symbol));
        if (recursive)
            throw new McpException($"Error: '{Name}' calls itself, so inlining it would never end");
    }

    private bool ImplementsInterfaceMember() =>
        _symbol.ContainingType.AllInterfaces
            .SelectMany(i => i.GetMembers().OfType<IMethodSymbol>())
            .Any(m => SymbolEqualityComparer.Default.Equals(_symbol.ContainingType.FindImplementationForInterfaceMember(m), _symbol));

    /// <summary>The call a reference to the method belongs to, or a refusal when it is not a call.</summary>
    private InvocationExpressionSyntax InvocationAt(SyntaxNode reference)
    {
        var name = reference as SimpleNameSyntax ?? reference.DescendantNodesAndSelf().OfType<SimpleNameSyntax>().First();
        var callee = name.Parent is MemberAccessExpressionSyntax access && access.Name == name ? access : (ExpressionSyntax)name;
        if (callee.Parent is InvocationExpressionSyntax invocation && invocation.Expression == callee)
            return invocation;

        throw new McpException(
            $"Error: '{Name}' is used without being called at {Where(reference)}, as a method group, so there is no call to inline");
    }

    private void InlineCall(InvocationExpressionSyntax invocation, SemanticModel callModel, DocumentEditor editor)
    {
        var operation = callModel.GetOperation(invocation) as IInvocationOperation
            ?? throw new McpException($"Error: The call at {Where(invocation)} could not be analysed");
        EnsureAccessible(invocation, callModel);

        var receiver = Receiver(invocation);
        var typeArguments = _symbol.TypeParameters
            .Zip(operation.TargetMethod.TypeArguments, (parameter, argument) => (parameter, argument))
            .ToDictionary(
                p => (ITypeParameterSymbol)p.parameter,
                p => SyntaxFactory.ParseTypeName(p.argument.ToMinimalDisplayString(callModel, invocation.SpanStart)),
                (IEqualityComparer<ITypeParameterSymbol>)SymbolEqualityComparer.Default);

        var statementForm = _expression == null;
        var arguments = Arguments(invocation, operation, statementForm);
        var locals = arguments.Where(a => a.NeedsLocal).ToList();

        var callStatement = invocation.Parent as ExpressionStatementSyntax;
        if (statementForm && callStatement == null)
            throw new McpException($"Error: The call at {Where(invocation)} is not a statement of its own, so '{Name}' cannot be inlined there");

        var containing = invocation.Ancestors().OfType<StatementSyntax>().FirstOrDefault();
        EnsureNamesAreFree(invocation, callModel, locals.Select(l => l.Parameter.Name)
            .Concat(statementForm ? DeclaredNames(_statements) : Enumerable.Empty<string>()));

        var declarations = locals.Select(l => (StatementSyntax)SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(
                    SyntaxFactory.ParseTypeName(l.Type.ToMinimalDisplayString(callModel, invocation.SpanStart)),
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.VariableDeclarator(l.Parameter.Name)
                            .WithInitializer(SyntaxFactory.EqualsValueClause(l.Expression.WithoutTrivia())))))
            .WithAdditionalAnnotations(Formatter.Annotation))
            .ToList();

        if (statementForm)
        {
            var rewriter = new BodyRewriter(this, arguments, typeArguments, receiver);
            var inlined = declarations
                .Concat(_statements.Select(s => ((StatementSyntax)rewriter.Visit(s)!).WithAdditionalAnnotations(Formatter.Annotation)))
                .ToList();
            ReplaceStatement(editor, callStatement!, inlined);
            return;
        }

        if (callStatement != null)
            throw new McpException($"Error: The call at {Where(invocation)} discards the result of '{Name}', so there is no expression to inline");

        if (declarations.Count > 0)
        {
            if (containing == null ||
                containing.Parent is not (BlockSyntax or SwitchSectionSyntax) ||
                ExpressionFacts.IsConditionallyEvaluated(invocation, containing) ||
                ExpressionFacts.IsInLoopCondition(invocation, containing))
            {
                throw new McpException(
                    $"Error: The call at {Where(invocation)} needs a local for '{locals[0].Parameter.Name}', but has no statement to declare it before");
            }

            // Comments above the statement stay above the declarations before it.
            declarations[0] = declarations[0].WithLeadingTrivia(containing.GetLeadingTrivia());
            editor.InsertBefore(containing, declarations);
            editor.ReplaceNode(containing, (current, _) => current.WithLeadingTrivia(SyntaxFactory.ElasticMarker).WithAdditionalAnnotations(Formatter.Annotation));
        }

        editor.ReplaceNode(invocation, (current, _) =>
        {
            // The arguments are taken from the current call, which already has any call
            // nested in them inlined.
            var currentArguments = ((InvocationExpressionSyntax)current).ArgumentList.Arguments;
            var rewriter = new BodyRewriter(this, arguments.Select(a => a.WithCurrent(currentArguments)).ToList(), typeArguments, receiver);
            return SyntaxFactory.ParenthesizedExpression((ExpressionSyntax)rewriter.Visit(_expression!)!)
                .WithTriviaFrom(current)
                .WithAdditionalAnnotations(Simplifier.Annotation, Formatter.Annotation);
        });
    }

    /// <summary>
    /// The object the call is made on, when it is written and is not <c>this</c> or a
    /// type. Members the method reaches through <c>this</c> are reached through it.
    /// </summary>
    private ExpressionSyntax? Receiver(InvocationExpressionSyntax invocation)
    {
        if (_symbol.IsStatic || invocation.Expression is not MemberAccessExpressionSyntax access)
            return null;
        if (access.Expression is ThisExpressionSyntax or BaseExpressionSyntax)
            return null;

        var uses = _method.DescendantNodes().Count(n => n is ThisExpressionSyntax || (n is SimpleNameSyntax name && IsImplicitInstanceMember(name)));
        if (uses > 1 && !ExpressionFacts.IsSimple(access.Expression))
            throw new McpException($"Error: The call at {Where(invocation)} is made on an expression that would be evaluated more than once");

        return access.Expression.WithoutTrivia();
    }

    private List<InlinedArgument> Arguments(InvocationExpressionSyntax invocation, IInvocationOperation operation, bool statementForm)
    {
        var arguments = new List<InlinedArgument>();
        foreach (var argument in operation.Arguments)
        {
            var parameter = argument.Parameter!;
            if (argument.ArgumentKind == ArgumentKind.ParamArray)
                throw new McpException($"Error: The call at {Where(invocation)} passes a params array, which is not supported");
            if (parameter.RefKind != RefKind.None)
                throw new McpException($"Error: The call at {Where(invocation)} passes '{parameter.Name}' by reference, which is not supported");

            var ordinal = parameter.Ordinal;
            var declared = _symbol.Parameters[ordinal];
            ExpressionSyntax expression;
            var index = -1;
            if (argument.ArgumentKind == ArgumentKind.DefaultValue)
            {
                expression = _method.ParameterList.Parameters[ordinal].Default!.Value;
            }
            else
            {
                var syntax = (ArgumentSyntax)argument.Syntax;
                index = invocation.ArgumentList.Arguments.IndexOf(syntax);
                expression = syntax.Expression;
            }

            var uses = ReferencesTo(declared).ToList();
            var written = uses.Any(LocalVariableTarget.IsWrite);
            var sideEffects = ExpressionFacts.HasSideEffects(expression);
            arguments.Add(new InlinedArgument(declared, argument.Parameter!.Type, expression, index, written || (sideEffects && (statementForm || uses.Count != 1))));
        }

        // Arguments with side effects run in the order they were written, so when more
        // than one has them, each is evaluated into a local first.
        if (arguments.Count(a => ExpressionFacts.HasSideEffects(a.Expression)) > 1)
            arguments = arguments.Select(a => ExpressionFacts.HasSideEffects(a.Expression) ? a with { NeedsLocal = true } : a).ToList();

        return arguments;
    }

    private IEnumerable<IdentifierNameSyntax> ReferencesTo(IParameterSymbol parameter) =>
        _method.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(n => n.Identifier.ValueText == parameter.Name &&
                        SymbolEqualityComparer.Default.Equals(_model.GetSymbolInfo(n).Symbol, parameter));

    /// <summary>Every member and type the method names must be accessible where it is called.</summary>
    private void EnsureAccessible(InvocationExpressionSyntax invocation, SemanticModel callModel)
    {
        var body = (SyntaxNode?)_method.Body ?? _method.ExpressionBody!;
        foreach (var name in body.DescendantNodes().OfType<SimpleNameSyntax>())
        {
            var symbol = _model.GetSymbolInfo(name).Symbol;
            if (symbol is null or ILocalSymbol or IParameterSymbol or ITypeParameterSymbol or INamespaceSymbol or IRangeVariableSymbol or ILabelSymbol)
                continue;
            if (symbol is IMethodSymbol { MethodKind: MethodKind.LocalFunction or MethodKind.AnonymousFunction })
                continue;

            if (!callModel.IsAccessible(invocation.SpanStart, symbol))
                throw new McpException($"Error: '{Name}' uses '{symbol.Name}', which is not accessible at {Where(invocation)}");
        }
    }

    private static IEnumerable<string> DeclaredNames(IEnumerable<StatementSyntax> statements) =>
        statements.SelectMany(s => s.DescendantNodesAndSelf()).Select(n => n switch
        {
            VariableDeclaratorSyntax v => v.Identifier.ValueText,
            SingleVariableDesignationSyntax d => d.Identifier.ValueText,
            ForEachStatementSyntax f => f.Identifier.ValueText,
            LocalFunctionStatementSyntax l => l.Identifier.ValueText,
            _ => null,
        }).OfType<string>();

    /// <summary>
    /// A name the inlined code declares must not be one the caller can already see, or
    /// declares anywhere in the same member.
    /// </summary>
    private void EnsureNamesAreFree(InvocationExpressionSyntax invocation, SemanticModel callModel, IEnumerable<string> names)
    {
        var member = invocation.Ancestors().FirstOrDefault(a => a is MemberDeclarationSyntax or AccessorDeclarationSyntax);
        var used = member == null
            ? new HashSet<string>()
            : DeclaredNames(member.DescendantNodes().OfType<StatementSyntax>())
                .Concat(member.DescendantNodes().OfType<ParameterSyntax>().Select(p => p.Identifier.ValueText))
                .ToHashSet();

        foreach (var name in names.Distinct())
        {
            var visible = callModel.LookupSymbols(invocation.SpanStart, name: name)
                .Any(s => s is ILocalSymbol or IParameterSymbol or IRangeVariableSymbol);
            if (visible || used.Contains(name))
                throw new McpException($"Error: Inlining '{Name}' at {Where(invocation)} would declare '{name}', which is already used there");
        }
    }

    /// <summary>
    /// Puts the inlined statements where the call statement was. The comments above the
    /// call stay above them; a call that is the body of an if or loop gets a block.
    /// </summary>
    private static void ReplaceStatement(SyntaxEditor editor, ExpressionStatementSyntax call, List<StatementSyntax> inlined)
    {
        if (inlined.Count == 0)
        {
            editor.RemoveNode(call, SyntaxRemoveOptions.KeepNoTrivia);
            return;
        }

        var firstLeading = inlined[0].GetLeadingTrivia()
            .SkipWhile(t => t.IsKind(SyntaxKind.WhitespaceTrivia) || t.IsKind(SyntaxKind.EndOfLineTrivia));
        inlined[0] = inlined[0].WithLeadingTrivia(call.GetLeadingTrivia().AddRange(firstLeading));

        if (call.Parent is BlockSyntax or SwitchSectionSyntax)
        {
            editor.InsertBefore(call, inlined);
            editor.RemoveNode(call, SyntaxRemoveOptions.KeepNoTrivia);
            return;
        }

        editor.ReplaceNode(call, inlined.Count == 1
            ? inlined[0]
            : SyntaxFactory.Block(inlined).WithTriviaFrom(call).WithAdditionalAnnotations(Formatter.Annotation));
    }

    /// <summary>
    /// Deletes the method with its comments. The blank line that separated it from the
    /// member before goes with it; when it was the first member, the blank line after it
    /// goes instead.
    /// </summary>
    private static void RemoveMethod(SyntaxEditor editor, MethodDeclarationSyntax method)
    {
        if (method.Parent is TypeDeclarationSyntax type && type.Members.IndexOf(method) == 0 && type.Members.Count > 1)
        {
            var next = type.Members[1];
            var leading = next.GetLeadingTrivia();
            if (leading.FirstOrDefault().IsKind(SyntaxKind.EndOfLineTrivia))
                editor.ReplaceNode(next, (current, _) => current.WithLeadingTrivia(current.GetLeadingTrivia().RemoveAt(0)));
        }

        editor.RemoveNode(method, SyntaxRemoveOptions.KeepNoTrivia);
    }

    private bool IsImplicitInstanceMember(SimpleNameSyntax name)
    {
        if (IsQualified(name))
            return false;

        var symbol = _model.GetSymbolInfo(name).Symbol;
        return symbol is IFieldSymbol or IPropertySymbol or IMethodSymbol or IEventSymbol &&
               !symbol.IsStatic &&
               symbol is not IMethodSymbol { MethodKind: MethodKind.LocalFunction or MethodKind.AnonymousFunction } &&
               symbol.ContainingType != null &&
               InheritsFromOrEquals(_symbol.ContainingType, symbol.ContainingType);
    }

    private static bool InheritsFromOrEquals(INamedTypeSymbol type, INamedTypeSymbol candidate)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, candidate.OriginalDefinition))
                return true;
        }

        return false;
    }

    /// <summary>Whether the name is qualified by something before it, rather than standing alone.</summary>
    private static bool IsQualified(SimpleNameSyntax name) => name.Parent switch
    {
        MemberAccessExpressionSyntax access => access.Name == name,
        QualifiedNameSyntax qualified => qualified.Right == name,
        AliasQualifiedNameSyntax alias => alias.Name == name,
        MemberBindingExpressionSyntax => true,
        NameColonSyntax or NameEqualsSyntax => true,
        AssignmentExpressionSyntax assignment => assignment.Left == name && assignment.Parent is InitializerExpressionSyntax,
        _ => false,
    };

    private static string Where(SyntaxNode node)
    {
        var span = node.GetLocation().GetLineSpan();
        return $"{Path.GetFileName(span.Path)}:{span.StartLinePosition.Line + 1}";
    }

    private sealed record InlinedArgument(IParameterSymbol Parameter, ITypeSymbol Type, ExpressionSyntax Expression, int Index, bool NeedsLocal)
    {
        public InlinedArgument WithCurrent(SeparatedSyntaxList<ArgumentSyntax> current) =>
            Index < 0 ? this : this with { Expression = current[Index].Expression };
    }

    /// <summary>
    /// Rewrites the method's body for one call: parameters become their arguments or
    /// locals, type parameters become the call's type arguments, members reached through
    /// <c>this</c> are reached through the call's receiver, and types and static members
    /// are fully qualified for the simplifier to shorten as the call site allows.
    /// </summary>
    private sealed class BodyRewriter : CSharpSyntaxRewriter
    {
        private readonly MethodInliner _inliner;
        private readonly Dictionary<IParameterSymbol, InlinedArgument> _arguments;
        private readonly Dictionary<ITypeParameterSymbol, TypeSyntax> _typeArguments;
        private readonly ExpressionSyntax? _receiver;

        public BodyRewriter(
            MethodInliner inliner,
            IEnumerable<InlinedArgument> arguments,
            Dictionary<ITypeParameterSymbol, TypeSyntax> typeArguments,
            ExpressionSyntax? receiver)
        {
            _inliner = inliner;
            _arguments = arguments.ToDictionary(a => a.Parameter, (IEqualityComparer<IParameterSymbol>)SymbolEqualityComparer.Default);
            _typeArguments = typeArguments;
            _receiver = receiver;
        }

        private SemanticModel Model => _inliner._model;

        public override SyntaxNode? VisitThisExpression(ThisExpressionSyntax node) =>
            _receiver == null ? node : Parenthesize(_receiver).WithTriviaFrom(node);

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            var symbol = Model.GetSymbolInfo(node).Symbol;
            switch (symbol)
            {
                case IParameterSymbol parameter when _arguments.TryGetValue(parameter, out var argument):
                    return argument.NeedsLocal
                        ? SyntaxFactory.IdentifierName(parameter.Name).WithTriviaFrom(node)
                        : Parenthesize(argument.Expression).WithTriviaFrom(node);
                case ITypeParameterSymbol typeParameter when _typeArguments.TryGetValue(typeParameter, out var typeArgument):
                    return typeArgument.WithTriviaFrom(node);
            }

            return Qualify(node, symbol) ?? base.VisitIdentifierName(node);
        }

        public override SyntaxNode? VisitGenericName(GenericNameSyntax node)
        {
            var symbol = Model.GetSymbolInfo(node).Symbol;
            var visited = (GenericNameSyntax)base.VisitGenericName(node)!;
            return Qualify(visited, symbol, original: node) ?? visited;
        }

        /// <summary>
        /// A name standing alone for a type or member is qualified: an instance member
        /// with the receiver, a type or static member with its fully qualified container.
        /// </summary>
        private ExpressionSyntax? Qualify(SimpleNameSyntax node, ISymbol? symbol, SimpleNameSyntax? original = null)
        {
            original ??= node;
            if (symbol == null || IsQualified(original))
                return null;

            if (_inliner.IsImplicitInstanceMember(original))
            {
                return _receiver == null
                    ? null
                    : SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, Parenthesize(_receiver), node.WithoutTrivia())
                        .WithTriviaFrom(node);
            }

            var isStaticMember = symbol is IFieldSymbol or IPropertySymbol or IMethodSymbol or IEventSymbol &&
                                 symbol.IsStatic &&
                                 symbol is not IMethodSymbol { MethodKind: MethodKind.LocalFunction };
            if (symbol is not INamedTypeSymbol && !isStaticMember)
                return null;

            var container = (ISymbol?)symbol.ContainingType ?? symbol.ContainingNamespace;
            ExpressionSyntax qualified = container is null or INamespaceSymbol { IsGlobalNamespace: true }
                ? SyntaxFactory.AliasQualifiedName(SyntaxFactory.IdentifierName(SyntaxFactory.Token(SyntaxKind.GlobalKeyword)), node.WithoutTrivia())
                : SyntaxFactory.QualifiedName(
                    SyntaxFactory.ParseName(container.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)),
                    node.WithoutTrivia());

            // A member reached through a qualified name reads as member access.
            if (isStaticMember && qualified is QualifiedNameSyntax name)
                qualified = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, name.Left, name.Right);

            return qualified.WithTriviaFrom(node).WithAdditionalAnnotations(Simplifier.Annotation);
        }

        private static ExpressionSyntax Parenthesize(ExpressionSyntax expression) =>
            ExpressionFacts.IsSimple(expression)
                ? expression.WithoutTrivia()
                : SyntaxFactory.ParenthesizedExpression(expression.WithoutTrivia()).WithAdditionalAnnotations(Simplifier.Annotation);
    }
}
