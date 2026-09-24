using ModelContextProtocol.Server;
using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using System.ComponentModel;

[McpServerToolType]
public static class IntroduceGenericTypeParameterTool
{
    [McpServerTool, Description("Replace a concrete type used in a class or method with a new type parameter, constrained as its uses need, and pass the old type at every use")]
    public static async Task<string> IntroduceGenericTypeParameter(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the class")] string filePath,
        [Description("Name of the class to make generic, or that declares the method")] string className,
        [Description("The concrete type to replace, as written in the file")] string typeToReplace,
        [Description("Name of the new type parameter")] string typeParameterName,
        [Description("Name of the method to make generic instead of the class (optional)")] string? methodName = null,
        [Description("Line of the method's declaration, to choose between overloads (optional)")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (solution, document) = await TypeRefactoringHelpers.LoadDocumentAsync(solutionPath, filePath, cancellationToken);
            var (type, typeDeclaration) = await TypeRefactoringHelpers.FindTypeAsync(document, className, cancellationToken);
            var model = (await document.GetSemanticModelAsync(cancellationToken))!;

            MemberDeclarationSyntax scope = typeDeclaration;
            ISymbol scopeSymbol = type;
            if (methodName is not null)
            {
                var candidates = typeDeclaration.Members.OfType<MethodDeclarationSyntax>().Where(m => m.Identifier.ValueText == methodName).ToList();
                var method = TypeRefactoringHelpers.Choose(candidates, m => m.Identifier, $"Method {methodName} in {className}", line);
                scope = method;
                scopeSymbol = model.GetDeclaredSymbol(method, cancellationToken)!;
            }

            var concrete = ResolveType(model, scope.SpanStart, typeToReplace);
            EnsureNameIsFree(model, scope, scopeSymbol, typeParameterName);

            var occurrences = Occurrences(scope, model, concrete);
            if (occurrences.Count == 0)
                throw new McpException($"Error: {scopeSymbol.Name} does not use {concrete.ToDisplayString()}");

            var creates = occurrences.Where(o => o.Parent is ObjectCreationExpressionSyntax).ToList();
            if (creates.Any(o => ((ObjectCreationExpressionSyntax)o.Parent!).ArgumentList?.Arguments.Count > 0))
                throw new McpException($"Error: There is no constraint that lets {typeParameterName} be created with arguments, as {scopeSymbol.Name} creates {concrete.Name}");

            // The scope's own members change signature along with it, so only
            // members declared elsewhere must keep binding as they did.
            var consumers = Consumers(scope, model, concrete, document.Id)
                .Where(c => !DeclaredBy(TypeRefactoringHelpers.Definition(c.Bound), scopeSymbol))
                .ToList();
            var callers = await ReferencesAsync(solution, scopeSymbol, scope, cancellationToken);

            // Try the loosest constraint first, and settle on the first that
            // compiles and leaves every use reaching the same members.
            var reason = "";
            foreach (var constraint in Constraints(concrete))
            {
                var changed = await RewriteAsync(
                    solution, document, scope, scopeSymbol, model, concrete, typeParameterName,
                    constraint, creates.Count > 0, occurrences, consumers, callers, cancellationToken);

                var errors = await TypeRefactoringHelpers.NewErrorsAsync(solution, changed, cancellationToken);
                if (errors.Count > 0)
                {
                    reason = TypeRefactoringHelpers.Describe(errors);
                    continue;
                }

                if (await TypeRefactoringHelpers.FirstChangedBindingAsync(changed, consumers, cancellationToken) is { } moved)
                {
                    reason = $"{moved.Node} would reach {moved.Now?.ToDisplayString() ?? "nothing"} instead of {moved.Binding.Bound.ToDisplayString()}";
                    continue;
                }

                await TypeRefactoringHelpers.ApplyAsync(solution, changed, cancellationToken);
                return $"Successfully made {scopeSymbol.Name} generic over {typeParameterName}"
                    + (constraint is null ? "" : $" where {typeParameterName} : {constraint.ToDisplayString()}");
            }

            throw new McpException(
                $"Error: There is no constraint under which {typeParameterName} can stand for {concrete.ToDisplayString()} everywhere {scopeSymbol.Name} uses it: {reason}");
        }
        catch (Exception ex)
        {
            throw new McpException($"Error introducing generic type parameter: {ex.Message}", ex);
        }
    }

    private static bool DeclaredBy(ISymbol member, ISymbol scope)
    {
        if (SymbolEqualityComparer.Default.Equals(member, scope.OriginalDefinition))
            return true;

        for (var type = member.ContainingType; type is not null; type = type.ContainingType)
        {
            if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, scope.OriginalDefinition))
                return true;
        }

        return false;
    }

    private static ITypeSymbol ResolveType(SemanticModel model, int position, string typeName)
    {
        var parsed = SyntaxFactory.ParseTypeName(typeName);
        var type = model.GetSpeculativeTypeInfo(position, parsed, SpeculativeBindingOption.BindAsTypeOrNamespace).Type
            ?? model.Compilation.GetTypeByMetadataName(typeName);
        return type is null or { TypeKind: TypeKind.Error }
            ? throw new McpException($"Error: No type named '{typeName}' found")
            : type;
    }

    private static void EnsureNameIsFree(SemanticModel model, MemberDeclarationSyntax scope, ISymbol scopeSymbol, string name)
    {
        if (!SyntaxFacts.IsValidIdentifier(name) || SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None)
            throw new McpException($"Error: '{name}' is not a valid type parameter name");

        var position = scope is TypeDeclarationSyntax type ? type.OpenBraceToken.SpanStart + 1 : scope.SpanStart;
        if (model.LookupSymbols(position, name: name).Any() || scopeSymbol.Name == name)
            throw new McpException($"Error: {name} already names something {scopeSymbol.Name} can see");
    }

    /// <summary>
    /// Where the scope names the concrete type: each outermost name that binds
    /// to it, leaving <c>nameof</c> alone because its text is its value.
    /// Reaching a static member through the type cannot be rewritten.
    /// </summary>
    private static IReadOnlyList<TypeSyntax> Occurrences(SyntaxNode scope, SemanticModel model, ITypeSymbol concrete)
    {
        var found = new List<TypeSyntax>();
        foreach (var name in scope.DescendantNodes().OfType<NameSyntax>())
        {
            if (name.Parent is QualifiedNameSyntax or AliasQualifiedNameSyntax || name.IsVar)
                continue;
            if (!SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(name).Symbol, concrete))
                continue;
            if (name.Ancestors().OfType<InvocationExpressionSyntax>().Any(i => i.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" }))
                continue;

            if (name.Parent is MemberAccessExpressionSyntax access && access.Expression == name)
                throw new McpException($"Error: {concrete.Name}.{access.Name.Identifier.ValueText} is a static member, which a type parameter cannot reach");

            found.Add(name);
        }

        return found;
    }

    /// <summary>
    /// The uses whose meaning depends on the old type: members reached through
    /// a value of it, and calls it is passed to.
    /// </summary>
    private static IReadOnlyList<TypeRefactoringHelpers.Binding> Consumers(
        SyntaxNode scope,
        SemanticModel model,
        ITypeSymbol concrete,
        DocumentId document)
    {
        var consumers = new List<TypeRefactoringHelpers.Binding>();
        foreach (var expression in scope.DescendantNodes().OfType<ExpressionSyntax>())
        {
            if (!SymbolEqualityComparer.Default.Equals(model.GetTypeInfo(expression).Type, concrete) || model.GetSymbolInfo(expression).Symbol is ITypeSymbol)
                continue;

            SyntaxNode? consumer = expression.Parent switch
            {
                MemberAccessExpressionSyntax access when access.Expression == expression => access,
                ConditionalAccessExpressionSyntax conditional when conditional.Expression == expression =>
                    conditional.WhenNotNull.DescendantNodesAndSelf().OfType<MemberBindingExpressionSyntax>().FirstOrDefault(),
                ArgumentSyntax { Parent.Parent: { } call } => call,
                _ => null,
            };

            if (consumer is not null && model.GetSymbolInfo(consumer).Symbol is { } bound)
                consumers.Add(new TypeRefactoringHelpers.Binding(document, consumer, new SyntaxAnnotation(), bound));
        }

        return consumers;
    }

    /// <summary>References to the class or method from outside it, which gain a type argument.</summary>
    private static async Task<IReadOnlyList<(Document Document, SyntaxNode Node)>> ReferencesAsync(
        Solution solution,
        ISymbol scopeSymbol,
        SyntaxNode scope,
        CancellationToken cancellationToken)
    {
        var found = new List<(Document, SyntaxNode)>();
        foreach (var location in (await SymbolFinder.FindReferencesAsync(scopeSymbol, solution, cancellationToken))
            .Where(r => SymbolEqualityComparer.Default.Equals(r.Definition.OriginalDefinition, scopeSymbol.OriginalDefinition)
                || r.Definition is IMethodSymbol { MethodKind: MethodKind.Constructor } constructor
                    && SymbolEqualityComparer.Default.Equals(constructor.ContainingType.OriginalDefinition, scopeSymbol.OriginalDefinition))
            .SelectMany(r => r.Locations))
        {
            var root = await location.Document.GetSyntaxRootAsync(cancellationToken);
            var node = root!.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true);
            if (node is SimpleNameSyntax && !node.Ancestors().Any(a => a is DocumentationCommentTriviaSyntax or CrefSyntax))
                found.Add((location.Document, node));
        }

        return found;
    }

    /// <summary>The constraints to try, loosest first: none, each interface of the type, then the type itself.</summary>
    private static IEnumerable<ITypeSymbol?> Constraints(ITypeSymbol concrete)
    {
        yield return null;
        foreach (var @interface in concrete.AllInterfaces)
            yield return @interface;
        if (concrete.TypeKind == TypeKind.Interface || concrete is { TypeKind: TypeKind.Class, IsSealed: false, IsStatic: false })
            yield return concrete;
    }

    /// <summary>
    /// The solution with the type parameter in place: the scope's uses of the
    /// old type become the parameter, the class's references to itself take
    /// the parameter, and every other use of the class, or call of the method
    /// that cannot infer it, passes the old type.
    /// </summary>
    private static async Task<Solution> RewriteAsync(
        Solution solution,
        Document document,
        MemberDeclarationSyntax scope,
        ISymbol scopeSymbol,
        SemanticModel model,
        ITypeSymbol concrete,
        string name,
        ITypeSymbol? constraint,
        bool creates,
        IReadOnlyList<TypeSyntax> occurrences,
        IReadOnlyList<TypeRefactoringHelpers.Binding> consumers,
        IReadOnlyList<(Document Document, SyntaxNode Node)> references,
        CancellationToken cancellationToken)
    {
        var parameter = SyntaxFactory.IdentifierName(name);
        var edits = new Dictionary<DocumentId, Dictionary<SyntaxNode, Func<SyntaxNode, SyntaxNode>>>();
        void Edit(DocumentId id, SyntaxNode node, Func<SyntaxNode, SyntaxNode> edit)
        {
            if (!edits.TryGetValue(id, out var byNode))
                edits[id] = byNode = new();
            byNode[node] = byNode.TryGetValue(node, out var earlier) ? current => edit(earlier(current)) : edit;
        }

        foreach (var occurrence in occurrences)
            Edit(document.Id, occurrence, current => parameter.WithTriviaFrom(current));
        foreach (var consumer in consumers)
            Edit(consumer.Document, consumer.Node, current => current.WithAdditionalAnnotations(consumer.Mark));

        foreach (var (referenceDocument, node) in references)
        {
            var within = referenceDocument.Id == document.Id && scope.Span.Contains(node.Span);
            if (within)
            {
                if (scopeSymbol is INamedTypeSymbol)
                    Edit(document.Id, node, current => WithTypeArguments((SimpleNameSyntax)current, new[] { parameter }));
                continue;
            }

            if (scopeSymbol is IMethodSymbol method && !NeedsExplicitArgument(method, concrete))
                continue;

            var referenceModel = (await referenceDocument.GetSemanticModelAsync(cancellationToken))!;
            var arguments = new List<TypeSyntax>();

            // A call that inferred the method's other type arguments must now spell them out.
            if (scopeSymbol is IMethodSymbol && node is IdentifierNameSyntax && referenceModel.GetSymbolInfo(node).Symbol is IMethodSymbol { IsGenericMethod: true } bound)
                arguments.AddRange(bound.TypeArguments.Select(t => SyntaxFactory.ParseTypeName(t.ToMinimalDisplayString(referenceModel, node.SpanStart))));
            arguments.Add(SyntaxFactory.ParseTypeName(concrete.ToMinimalDisplayString(referenceModel, node.SpanStart)));
            Edit(referenceDocument.Id, node, current => WithTypeArguments((SimpleNameSyntax)current, arguments));
        }

        Edit(document.Id, scope, current => WithTypeParameter((MemberDeclarationSyntax)current, name, constraint, creates, model, scope.SpanStart));

        foreach (var (id, byNode) in edits)
        {
            var root = (await solution.GetDocument(id)!.GetSyntaxRootAsync(cancellationToken))!;
            solution = solution.WithDocumentSyntaxRoot(id, root.ReplaceNodes(byNode.Keys, (original, current) => byNode[original](current)));
        }

        return solution;
    }

    /// <summary>A call can only infer the new type parameter from a parameter whose type uses the old type.</summary>
    private static bool NeedsExplicitArgument(IMethodSymbol method, ITypeSymbol concrete) =>
        !method.Parameters.Any(p => Mentions(p.Type, concrete));

    private static bool Mentions(ITypeSymbol type, ITypeSymbol concrete) =>
        SymbolEqualityComparer.Default.Equals(type, concrete)
        || type is INamedTypeSymbol named && named.TypeArguments.Any(a => Mentions(a, concrete))
        || type is IArrayTypeSymbol array && Mentions(array.ElementType, concrete);

    private static SimpleNameSyntax WithTypeArguments(SimpleNameSyntax name, IEnumerable<TypeSyntax> arguments) => name switch
    {
        GenericNameSyntax generic => generic.WithTypeArgumentList(
            generic.TypeArgumentList.WithArguments(Appended(generic.TypeArgumentList.Arguments, arguments))),
        _ => SyntaxFactory.GenericName(
                name.Identifier.WithoutTrivia(),
                SyntaxFactory.TypeArgumentList(Appended(default(SeparatedSyntaxList<TypeSyntax>), arguments)))
            .WithTriviaFrom(name),
    };

    /// <summary>A comma separated list with more items, each new one after a comma and a space.</summary>
    private static SeparatedSyntaxList<T> Appended<T>(SeparatedSyntaxList<T> list, IEnumerable<T> items)
        where T : SyntaxNode
    {
        var all = list.Concat(items).ToList();
        var separators = list.GetSeparators()
            .Concat(Enumerable.Repeat(SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space), all.Count - 1 - list.SeparatorCount));
        return SyntaxFactory.SeparatedList(all, separators);
    }

    /// <summary>
    /// Adds the type parameter to the class or method, and its constraint
    /// clause after the rest of the header, taking over whatever separated the
    /// header from the body.
    /// </summary>
    private static MemberDeclarationSyntax WithTypeParameter(
        MemberDeclarationSyntax scope,
        string name,
        ITypeSymbol? constraint,
        bool creates,
        SemanticModel model,
        int position)
    {
        var typeParameter = SyntaxFactory.TypeParameter(name);
        TypeParameterListSyntax Extend(TypeParameterListSyntax? list, SyntaxToken identifier) =>
            list?.WithParameters(Appended(list.Parameters, new[] { typeParameter }))
            ?? SyntaxFactory.TypeParameterList(SyntaxFactory.SingletonSeparatedList(typeParameter)).WithTrailingTrivia(identifier.TrailingTrivia);

        MemberDeclarationSyntax withParameter = scope switch
        {
            TypeDeclarationSyntax type => type
                .WithIdentifier(type.TypeParameterList is null ? type.Identifier.WithoutTrivia() : type.Identifier)
                .WithTypeParameterList(Extend(type.TypeParameterList, type.Identifier)),
            MethodDeclarationSyntax method => method
                .WithIdentifier(method.TypeParameterList is null ? method.Identifier.WithoutTrivia() : method.Identifier)
                .WithTypeParameterList(Extend(method.TypeParameterList, method.Identifier)),
            _ => throw new McpException("Error: Only a class or method can be made generic"),
        };

        var constraints = new List<TypeParameterConstraintSyntax>();
        if (constraint is not null)
            constraints.Add(SyntaxFactory.TypeConstraint(SyntaxFactory.ParseTypeName(constraint.ToMinimalDisplayString(model, position))));
        if (creates)
            constraints.Add(SyntaxFactory.ConstructorConstraint());
        if (constraints.Count == 0)
            return withParameter;

        var bodyStart = withParameter switch
        {
            TypeDeclarationSyntax type => type.OpenBraceToken,
            MethodDeclarationSyntax { Body: { } body } => body.OpenBraceToken,
            MethodDeclarationSyntax method => method.ExpressionBody!.ArrowToken,
            _ => default,
        };
        var headerEnd = bodyStart.GetPreviousToken();
        var clause = SyntaxFactory.TypeParameterConstraintClause(
                SyntaxFactory.Token(SyntaxKind.WhereKeyword).WithLeadingTrivia(SyntaxFactory.Space).WithTrailingTrivia(SyntaxFactory.Space),
                SyntaxFactory.IdentifierName(name).WithTrailingTrivia(SyntaxFactory.Space),
                SyntaxFactory.Token(SyntaxKind.ColonToken).WithTrailingTrivia(SyntaxFactory.Space),
                SyntaxFactory.SeparatedList(constraints, constraints.Skip(1).Select(_ => SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space))))
            .WithTrailingTrivia(headerEnd.TrailingTrivia);

        return withParameter.ReplaceToken(headerEnd, headerEnd.WithTrailingTrivia()) switch
        {
            TypeDeclarationSyntax type => type.AddConstraintClauses(clause),
            MethodDeclarationSyntax method => method.AddConstraintClauses(clause),
            var other => other,
        };
    }
}
