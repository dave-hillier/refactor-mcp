using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

/// <summary>
/// One parameter of a changed signature: either a parameter the method
/// already has, found by its ordinal in the old signature, or a new one with
/// the argument each existing call passes for it.
/// </summary>
internal sealed class ParameterSlot
{
    private ParameterSlot(
        int? ordinal,
        ParameterSyntax? declaration,
        Func<SignatureCallSite, ExpressionSyntax?>? valueAt,
        Func<ParameterSyntax, ParameterSyntax>? rewrite,
        Func<SignatureCallSite, ArgumentSyntax, bool>? omitArgument)
    {
        Ordinal = ordinal;
        Declaration = declaration;
        ValueAt = valueAt;
        Rewrite = rewrite;
        OmitArgument = omitArgument;
    }

    /// <summary>The ordinal in the old signature, or null for a new parameter.</summary>
    public int? Ordinal { get; }

    public ParameterSyntax? Declaration { get; }

    /// <summary>The argument a call passes for a new parameter; null leaves it to the default.</summary>
    public Func<SignatureCallSite, ExpressionSyntax?>? ValueAt { get; }

    public Func<ParameterSyntax, ParameterSyntax>? Rewrite { get; }

    /// <summary>Whether a call may drop the argument it passes for an existing parameter.</summary>
    public Func<SignatureCallSite, ArgumentSyntax, bool>? OmitArgument { get; }

    public static ParameterSlot Existing(
        int ordinal,
        Func<ParameterSyntax, ParameterSyntax>? rewrite = null,
        Func<SignatureCallSite, ArgumentSyntax, bool>? omitArgument = null) =>
        new(ordinal, null, null, rewrite, omitArgument);

    public static ParameterSlot Added(ParameterSyntax declaration, Func<SignatureCallSite, ExpressionSyntax?> valueAt) =>
        new(null, declaration, valueAt, null, null);

    /// <summary>Parses <c>type name</c> or <c>type name = default</c> into a parameter.</summary>
    public static ParameterSyntax ParseDeclaration(string type, string name, string? defaultValue)
    {
        var text = defaultValue is null ? $"{type} {name}" : $"{type} {name} = {defaultValue}";
        var list = SyntaxFactory.ParseParameterList($"({text})");
        if (list.ContainsDiagnostics || list.Parameters.Count != 1)
            throw new McpException($"Error: '{text}' is not a valid parameter declaration");

        return list.Parameters[0];
    }
}

/// <summary>A call of a method whose signature is changing.</summary>
internal sealed class SignatureCallSite
{
    private readonly IReadOnlyDictionary<int, List<ArgumentSyntax>> _arguments;

    public SignatureCallSite(
        Document document,
        SemanticModel model,
        SyntaxNode call,
        IMethodSymbol method,
        IReadOnlyDictionary<int, List<ArgumentSyntax>> arguments)
    {
        Document = document;
        Model = model;
        Call = call;
        Method = method;
        _arguments = arguments;
    }

    public Document Document { get; }

    public SemanticModel Model { get; }

    /// <summary>An invocation, an object creation or a constructor initializer.</summary>
    public SyntaxNode Call { get; }

    /// <summary>The method the call binds to, which may be reduced from an extension method.</summary>
    public IMethodSymbol Method { get; }

    /// <summary>
    /// The arguments passed for a parameter of the old signature, by ordinal.
    /// The receiver of a call to an extension method in reduced form is not
    /// an argument, so it is never listed.
    /// </summary>
    public IReadOnlyList<ArgumentSyntax> ArgumentsFor(int ordinal) =>
        _arguments.TryGetValue(ordinal, out var arguments) ? arguments : Array.Empty<ArgumentSyntax>();

    /// <summary>The expression passed for a parameter, or null when the call leaves it to its default.</summary>
    public ExpressionSyntax? ArgumentFor(int ordinal) => ArgumentsFor(ordinal) is [var single] ? single.Expression : null;

    /// <summary>The expression a reduced extension call passes for the this parameter.</summary>
    public ExpressionSyntax? Receiver =>
        Method.ReducedFrom is not null && Call is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access }
            ? access.Expression
            : null;
}

/// <summary>
/// Rewrites a method's parameter list, the matching lists of every override
/// and interface member related to it, and the arguments of every call in the
/// solution. The primitive the signature refactorings build on.
/// </summary>
internal static class SignatureChange
{
    public static async Task<Solution> ApplyAsync(
        Solution solution,
        IMethodSymbol method,
        IReadOnlyList<ParameterSlot> slots,
        CancellationToken cancellationToken = default)
    {
        method = method.OriginalDefinition;
        var family = await MethodFamily.FindAsync(solution, method, cancellationToken);
        CheckExtensionReceiverStaysFirst(method, slots);
        var primary = await method.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken);
        if (ParameterListOf(primary) is { } primaryList)
            CheckNewOrder(RewriteParameters(primaryList, slots));
        await CheckRemovedParametersAreUnused(solution, family, slots, cancellationToken);

        var edits = new Dictionary<DocumentId, Dictionary<SyntaxNode, Func<SyntaxNode, SyntaxNode>>>();

        foreach (var member in family)
        {
            foreach (var reference in member.DeclaringSyntaxReferences)
            {
                var declaration = await reference.GetSyntaxAsync(cancellationToken);
                var parameterList = ParameterListOf(declaration);
                if (parameterList is null)
                    continue;

                var document = solution.GetDocument(reference.SyntaxTree)!;
                Edit(edits, document.Id, parameterList, current => RewriteParameters((ParameterListSyntax)current, slots));
            }
        }

        foreach (var site in await CallSitesAsync(solution, family, cancellationToken))
        {
            var original = ArgumentListOf(site.Call);
            Edit(edits, site.Document.Id, site.Call, current => RewriteCall(current, original, site, slots));
        }

        var updated = solution;
        foreach (var (documentId, replacements) in edits)
        {
            var document = updated.GetDocument(documentId)!;
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = root!.ReplaceNodes(replacements.Keys, (original, current) => replacements[original](current));
            updated = updated.WithDocumentSyntaxRoot(documentId, newRoot);
        }

        return updated;
    }

    /// <summary>Every call of a member of the family, refusing references that are not calls.</summary>
    public static async Task<IReadOnlyList<SignatureCallSite>> CallSitesAsync(
        Solution solution,
        IReadOnlyList<IMethodSymbol> family,
        CancellationToken cancellationToken = default)
    {
        var keys = family.Select(MethodFamily.Key).ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<(DocumentId, int)>();
        var sites = new List<SignatureCallSite>();

        foreach (var member in family)
        {
            var references = await SymbolFinder.FindReferencesAsync(member, solution, cancellationToken);
            foreach (var location in references.SelectMany(r => r.Locations))
            {
                if (!location.Location.IsInSource)
                    continue;

                var document = location.Document;
                var span = location.Location.SourceSpan;
                if (!seen.Add((document.Id, span.Start)))
                    continue;

                var root = await document.GetSyntaxRootAsync(cancellationToken);
                var token = root!.FindToken(span.Start, findInsideTrivia: true);
                if (token.Parent is null || IsNameOnly(token.Parent))
                    continue;

                // Target-typed new is reported as implicit; other implicit
                // references, such as a base constructor called without an
                // initializer, have no arguments to rewrite.
                if (location.IsImplicit && token.Parent is not ImplicitObjectCreationExpressionSyntax)
                    continue;

                var call = CallOf(token.Parent);
                var model = await document.GetSemanticModelAsync(cancellationToken);
                var bound = call is null ? null : model!.GetSymbolInfo(call, cancellationToken).Symbol as IMethodSymbol;
                if (call is null || bound is null || !keys.Contains(MethodFamily.Key(bound)))
                {
                    var position = location.Location.GetLineSpan();
                    throw new McpException(
                        $"Error: '{member.Name}' is used as a method group at {Path.GetFileName(position.Path)}({position.StartLinePosition.Line + 1},{position.StartLinePosition.Character + 1}), which a new signature would break");
                }

                sites.Add(new SignatureCallSite(document, model!, call, bound, MapArguments(call, bound)));
            }
        }

        return sites;
    }

    public static ParameterListSyntax? ParameterListOf(SyntaxNode declaration) => declaration switch
    {
        BaseMethodDeclarationSyntax method => method.ParameterList,
        LocalFunctionStatementSyntax local => local.ParameterList,
        _ => null,
    };

    public static ArgumentListSyntax? ArgumentListOf(SyntaxNode call) => call switch
    {
        InvocationExpressionSyntax invocation => invocation.ArgumentList,
        BaseObjectCreationExpressionSyntax creation => creation.ArgumentList,
        ConstructorInitializerSyntax initializer => initializer.ArgumentList,
        _ => null,
    };

    private static void Edit(
        Dictionary<DocumentId, Dictionary<SyntaxNode, Func<SyntaxNode, SyntaxNode>>> edits,
        DocumentId document,
        SyntaxNode node,
        Func<SyntaxNode, SyntaxNode> rewrite)
    {
        if (!edits.TryGetValue(document, out var replacements))
            edits[document] = replacements = new Dictionary<SyntaxNode, Func<SyntaxNode, SyntaxNode>>();

        replacements[node] = rewrite;
    }

    /// <summary>nameof and documentation references name the method without calling it.</summary>
    private static bool IsNameOnly(SyntaxNode node) =>
        node.AncestorsAndSelf().Any(n => n is CrefSyntax
            || n is InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" } });

    /// <summary>The call a reference to a method's name belongs to, or null when it is not called.</summary>
    private static SyntaxNode? CallOf(SyntaxNode node)
    {
        if (node is ImplicitObjectCreationExpressionSyntax or ConstructorInitializerSyntax)
            return node;

        var current = node;
        while (current.Parent is MemberAccessExpressionSyntax access && access.Name == current
            || current.Parent is MemberBindingExpressionSyntax
            || current.Parent is QualifiedNameSyntax qualified && qualified.Right == current
            || current.Parent is AliasQualifiedNameSyntax alias && alias.Name == current)
        {
            current = current.Parent;
        }

        return current.Parent switch
        {
            InvocationExpressionSyntax invocation when invocation.Expression == current => invocation,
            ObjectCreationExpressionSyntax creation when creation.Type == current => creation,
            _ => null,
        };
    }

    /// <summary>Which old parameter each argument is passed for, by ordinal.</summary>
    private static Dictionary<int, List<ArgumentSyntax>> MapArguments(SyntaxNode call, IMethodSymbol bound)
    {
        var map = new Dictionary<int, List<ArgumentSyntax>>();
        var arguments = ArgumentListOf(call)?.Arguments ?? default;
        var offset = bound.ReducedFrom is null ? 0 : 1;
        var paramsOrdinal = bound.Parameters.LastOrDefault() is { IsParams: true } last ? last.Ordinal : -1;

        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];
            int ordinal;
            if (argument.NameColon is { } nameColon)
            {
                var parameter = bound.Parameters.FirstOrDefault(p => p.Name == nameColon.Name.Identifier.ValueText);
                if (parameter is null)
                    continue;
                ordinal = parameter.Ordinal;
            }
            else
            {
                ordinal = paramsOrdinal >= 0 && i >= paramsOrdinal ? paramsOrdinal : i;
            }

            if (!map.TryGetValue(ordinal + offset, out var list))
                map[ordinal + offset] = list = new List<ArgumentSyntax>();
            list.Add(argument);
        }

        return map;
    }

    private static ParameterListSyntax RewriteParameters(ParameterListSyntax list, IReadOnlyList<ParameterSlot> slots)
    {
        var items = new List<(ParameterSyntax Node, int? From)>();
        foreach (var slot in slots)
        {
            if (slot.Ordinal is { } ordinal)
            {
                var parameter = list.Parameters[ordinal];
                items.Add((slot.Rewrite?.Invoke(parameter) ?? parameter, ordinal));
            }
            else
            {
                items.Add((slot.Declaration!, null));
            }
        }

        return list.WithParameters(ListLayout.Rearrange(list.Parameters, items));
    }

    private static SyntaxNode RewriteCall(
        SyntaxNode current,
        ArgumentListSyntax? original,
        SignatureCallSite site,
        IReadOnlyList<ParameterSlot> slots)
    {
        var arguments = ArgumentListOf(current) ?? SyntaxFactory.ArgumentList();
        var reduced = site.Method.ReducedFrom is not null;
        var items = new List<(ArgumentSyntax Node, int? From)>();
        var mustName = false;

        foreach (var slot in slots)
        {
            if (slot.Ordinal is { } ordinal)
            {
                if (reduced && ordinal == 0)
                    continue;

                var passed = site.ArgumentsFor(ordinal);
                if (passed.Count == 0 || passed.Count == 1 && slot.OmitArgument?.Invoke(site, passed[0]) == true)
                {
                    mustName = true;
                    continue;
                }

                if (mustName && passed.Count > 1)
                    throw new McpException($"Error: A call passes several values for the params parameter '{ParameterName(site, ordinal)}' after an omitted argument, so they cannot be named");

                foreach (var argument in passed)
                {
                    var index = original!.Arguments.IndexOf(argument);
                    var node = arguments.Arguments[index];
                    if (mustName && node.NameColon is null)
                        node = Named(node, ParameterName(site, ordinal));
                    items.Add((node, index));
                }
            }
            else
            {
                var value = slot.ValueAt!(site);
                if (value is null)
                {
                    mustName = true;
                    continue;
                }

                var node = SyntaxFactory.Argument(value.WithoutTrivia());
                if (mustName)
                    node = Named(node, slot.Declaration!.Identifier.ValueText);
                items.Add((node, null));
            }
        }

        var rewritten = arguments.WithArguments(ListLayout.Rearrange(arguments.Arguments, items));
        return current switch
        {
            InvocationExpressionSyntax invocation => invocation.WithArgumentList(rewritten),
            ObjectCreationExpressionSyntax creation => creation.WithArgumentList(rewritten),
            ImplicitObjectCreationExpressionSyntax creation => creation.WithArgumentList(rewritten),
            ConstructorInitializerSyntax initializer => initializer.WithArgumentList(rewritten),
            _ => current,
        };
    }

    private static string ParameterName(SignatureCallSite site, int ordinal)
    {
        var method = site.Method.ReducedFrom ?? site.Method;
        return method.Parameters[ordinal].Name;
    }

    private static ArgumentSyntax Named(ArgumentSyntax argument, string name)
    {
        var leading = argument.GetLeadingTrivia();
        var nameColon = SyntaxFactory.NameColon(
            SyntaxFactory.IdentifierName(name),
            SyntaxFactory.Token(SyntaxKind.ColonToken).WithTrailingTrivia(SyntaxFactory.Space));
        return argument.WithoutLeadingTrivia().WithNameColon(nameColon).WithLeadingTrivia(leading);
    }

    private static void CheckExtensionReceiverStaysFirst(IMethodSymbol method, IReadOnlyList<ParameterSlot> slots)
    {
        if (method.IsExtensionMethod && (slots.Count == 0 || slots[0].Ordinal != 0))
            throw new McpException(
                $"Error: The this parameter '{method.Parameters[0].Name}' of an extension method must stay first");
    }

    /// <summary>A params array must be last, and no required parameter may follow an optional one.</summary>
    private static void CheckNewOrder(ParameterListSyntax parameters)
    {
        string? optional = null;
        for (var i = 0; i < parameters.Parameters.Count; i++)
        {
            var parameter = parameters.Parameters[i];
            var name = parameter.Identifier.ValueText;
            var isParams = parameter.Modifiers.Any(SyntaxKind.ParamsKeyword);

            if (isParams && i != parameters.Parameters.Count - 1)
                throw new McpException($"Error: The params parameter '{name}' must stay last");

            if (parameter.Default is not null)
                optional ??= name;
            else if (optional is not null && !isParams)
                throw new McpException($"Error: Required parameter '{name}' would follow optional parameter '{optional}'");
        }
    }

    private static async Task CheckRemovedParametersAreUnused(
        Solution solution,
        IReadOnlyList<IMethodSymbol> family,
        IReadOnlyList<ParameterSlot> slots,
        CancellationToken cancellationToken)
    {
        var kept = slots.Where(s => s.Ordinal is not null).Select(s => s.Ordinal!.Value).ToHashSet();
        foreach (var member in family)
        {
            foreach (var parameter in member.Parameters.Where(p => !kept.Contains(p.Ordinal)))
            {
                if (await ParameterUsage.IsUsedAsync(solution, member, parameter, cancellationToken))
                    throw new McpException(
                        $"Error: Parameter '{parameter.Name}' is used in the body of '{member.ContainingType.Name}.{member.Name}', so it cannot be removed");
            }
        }
    }
}

/// <summary>A method and every method that must keep the same signature as it.</summary>
internal static class MethodFamily
{
    /// <summary>
    /// The method, the members it overrides or implements, and the members
    /// that override or implement those, transitively. Refuses when any of
    /// them is declared outside the solution, since that one cannot change.
    /// </summary>
    public static async Task<IReadOnlyList<IMethodSymbol>> FindAsync(
        Solution solution,
        IMethodSymbol method,
        CancellationToken cancellationToken = default)
    {
        var family = new List<IMethodSymbol>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<IMethodSymbol>();
        pending.Enqueue(method);

        while (pending.Count > 0)
        {
            var member = pending.Dequeue();
            var definition = member.OriginalDefinition;
            if (!seen.Add(Key(definition)))
                continue;

            if (!definition.Locations.Any(l => l.IsInSource))
                throw new McpException(
                    $"Error: '{method.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)}' must keep the signature of '{member.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)}', which is declared outside the solution");

            family.Add(definition);

            if (definition.OverriddenMethod is { } overridden)
                pending.Enqueue(overridden);

            foreach (var implemented in definition.ExplicitInterfaceImplementations)
                pending.Enqueue(implemented);

            foreach (var implemented in ImplicitlyImplemented(definition))
                pending.Enqueue(implemented);

            foreach (var overriding in await SymbolFinder.FindOverridesAsync(definition, solution, cancellationToken: cancellationToken))
                pending.Enqueue((IMethodSymbol)overriding);

            if (definition.ContainingType.TypeKind == TypeKind.Interface)
            {
                foreach (var implementation in await SymbolFinder.FindImplementationsAsync(definition, solution, cancellationToken: cancellationToken))
                    pending.Enqueue((IMethodSymbol)implementation);
            }
        }

        return family;
    }

    /// <summary>Identifies a method across the compilations of a solution.</summary>
    public static string Key(IMethodSymbol method)
    {
        var definition = (method.ReducedFrom ?? method).OriginalDefinition;
        return $"{definition.ContainingAssembly?.Name}|{definition.GetDocumentationCommentId()}";
    }

    private static IEnumerable<IMethodSymbol> ImplicitlyImplemented(IMethodSymbol method)
    {
        if (method.ContainingType.TypeKind == TypeKind.Interface)
            yield break;

        foreach (var @interface in method.ContainingType.AllInterfaces)
        {
            foreach (var member in @interface.GetMembers(method.Name).OfType<IMethodSymbol>())
            {
                var implementation = method.ContainingType.FindImplementationForInterfaceMember(member);
                if (SymbolEqualityComparer.Default.Equals(implementation?.OriginalDefinition, method))
                    yield return member;
            }
        }
    }
}

/// <summary>Where a parameter is read or written in the body of its method.</summary>
internal static class ParameterUsage
{
    public static async Task<bool> IsUsedAsync(
        Solution solution,
        IMethodSymbol method,
        IParameterSymbol parameter,
        CancellationToken cancellationToken = default) =>
        (await UsesAsync(solution, method, parameter, cancellationToken)).Count > 0;

    /// <summary>The identifiers in the method's declarations that refer to the parameter.</summary>
    public static async Task<IReadOnlyList<IdentifierNameSyntax>> UsesAsync(
        Solution solution,
        IMethodSymbol method,
        IParameterSymbol parameter,
        CancellationToken cancellationToken = default)
    {
        var uses = new List<IdentifierNameSyntax>();
        foreach (var reference in method.DeclaringSyntaxReferences)
        {
            var declaration = await reference.GetSyntaxAsync(cancellationToken);
            var document = solution.GetDocument(reference.SyntaxTree);
            if (document is null)
                continue;

            var model = await document.GetSemanticModelAsync(cancellationToken);
            foreach (var identifier in declaration.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (identifier.Identifier.ValueText != parameter.Name)
                    continue;

                var symbol = model!.GetSymbolInfo(identifier, cancellationToken).Symbol;
                if (symbol is IParameterSymbol used && used.Ordinal == parameter.Ordinal
                    && SymbolEqualityComparer.Default.Equals(used.ContainingSymbol.OriginalDefinition, method.OriginalDefinition))
                {
                    uses.Add(identifier);
                }
            }
        }

        return uses;
    }
}

/// <summary>
/// Rebuilds a separated list in a new order. Layout belongs to positions and
/// comments belong to elements: each position keeps its indentation and line
/// breaks, while a comment before an element or after its comma moves with it.
/// </summary>
internal static class ListLayout
{
    public static SeparatedSyntaxList<T> Rearrange<T>(
        SeparatedSyntaxList<T> original,
        IReadOnlyList<(T Node, int? From)> items)
        where T : SyntaxNode
    {
        var count = original.Count;
        var leadingLayout = new SyntaxTriviaList[count];
        var leadingContent = new SyntaxTriviaList[count];
        var trailingLayout = new SyntaxTriviaList[count];
        for (var i = 0; i < count; i++)
        {
            (leadingLayout[i], leadingContent[i]) = SplitLeading(original[i].GetLeadingTrivia());
            trailingLayout[i] = IsLayout(original[i].GetTrailingTrivia()) ? original[i].GetTrailingTrivia() : default;
        }

        var separators = original.SeparatorCount;
        var commentAfter = new SyntaxTriviaList[separators];
        var separatorLayout = new SyntaxTriviaList[separators];
        for (var i = 0; i < separators; i++)
            (commentAfter[i], separatorLayout[i]) = SplitTrailing(original.GetSeparator(i).TrailingTrivia);

        var nodes = new List<T>();
        var newSeparators = new List<SyntaxToken>();
        for (var j = 0; j < items.Count; j++)
        {
            var (node, from) = items[j];
            var isLast = j == items.Count - 1;

            var leading = count == 0 ? default : leadingLayout[Math.Min(j, count - 1)];
            var content = from is { } f ? leadingContent[f] : default;
            node = node.WithLeadingTrivia(leading.AddRange(content));

            var ownTrailing = from is { } t && IsLayout(original[t].GetTrailingTrivia()) ? default : node.GetTrailingTrivia();
            var positionTrailing = count == 0
                ? default
                : isLast ? trailingLayout[count - 1] : j < count - 1 ? trailingLayout[j] : default;
            node = node.WithTrailingTrivia(ownTrailing.AddRange(positionTrailing));

            var after = from is { } a && a < separators ? commentAfter[a] : default;
            if (!isLast)
            {
                var layout = separators == 0
                    ? SyntaxFactory.TriviaList(SyntaxFactory.Space)
                    : separatorLayout[Math.Min(j, separators - 1)];
                newSeparators.Add(SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(after.AddRange(layout)));
            }
            else if (after.Count > 0)
            {
                // A line comment cannot end a list, or it would swallow the parenthesis.
                node = node.WithTrailingTrivia(node.GetTrailingTrivia().AddRange(after).AddRange(separatorLayout[from!.Value]));
            }

            nodes.Add(node);
        }

        return SyntaxFactory.SeparatedList(nodes, newSeparators);
    }

    private static bool IsLayout(SyntaxTriviaList trivia) =>
        trivia.All(t => t.IsKind(SyntaxKind.WhitespaceTrivia) || t.IsKind(SyntaxKind.EndOfLineTrivia));

    /// <summary>Indentation belongs to the position; anything after it belongs to the element.</summary>
    private static (SyntaxTriviaList Layout, SyntaxTriviaList Content) SplitLeading(SyntaxTriviaList trivia)
    {
        var layout = trivia.TakeWhile(t => t.IsKind(SyntaxKind.WhitespaceTrivia) || t.IsKind(SyntaxKind.EndOfLineTrivia)).Count();
        return (SyntaxFactory.TriviaList(trivia.Take(layout)), SyntaxFactory.TriviaList(trivia.Skip(layout)));
    }

    /// <summary>A comment after a comma belongs to the element before it; the rest is layout.</summary>
    private static (SyntaxTriviaList Comment, SyntaxTriviaList Layout) SplitTrailing(SyntaxTriviaList trivia)
    {
        var lastComment = -1;
        for (var i = 0; i < trivia.Count; i++)
        {
            if (trivia[i].IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia[i].IsKind(SyntaxKind.MultiLineCommentTrivia))
                lastComment = i;
        }

        return (SyntaxFactory.TriviaList(trivia.Take(lastComment + 1)), SyntaxFactory.TriviaList(trivia.Skip(lastComment + 1)));
    }
}
