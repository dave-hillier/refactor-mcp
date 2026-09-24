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
using System.Threading;

[McpServerToolType]
public static class CollapseHierarchyTool
{
    [McpServerTool, Description("Merge a class into its base class, or a base class into its only subclass: move its members across, make every reference name the class that stays, and delete it")]
    public static async Task<string> CollapseHierarchy(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the class to remove")] string filePath,
        [Description("Name of the class to remove")] string className,
        [Description("Name of the class to merge it into: its base class (the default) or its only direct subclass")] string? into = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (solution, document) = await TypeRefactoringHelpers.LoadDocumentAsync(solutionPath, filePath, cancellationToken);
            var (removed, removedDeclaration) = await TypeRefactoringHelpers.FindTypeAsync(document, className, cancellationToken);
            var (kept, intoBase) = await KeptClassAsync(solution, removed, into, cancellationToken);
            var keptDeclaration = (TypeDeclarationSyntax)await kept.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken);
            EnsureSupported(removed, removedDeclaration);
            EnsureSupported(kept, keptDeclaration);

            if (removed.InstanceConstructors.Any(c => !c.IsImplicitlyDeclared) || removed.StaticConstructors.Length > 0)
                throw new McpException($"Error: {removed.Name} declares a constructor, which cannot be merged with how {kept.Name} is constructed");

            var references = await ReferencesAsync(solution, removed, cancellationToken);
            var collapse = intoBase
                ? await IntoBaseAsync(solution, removed, kept, references, cancellationToken)
                : await IntoSubclassAsync(solution, removed, kept, keptDeclaration, references, cancellationToken);

            var changed = await ApplyAsync(solution, document, removedDeclaration, keptDeclaration, kept, collapse, cancellationToken);
            await SolutionEdits.EnsureCompilesAsync(solution, changed, cancellationToken);
            await SolutionEdits.WriteAsync(solution, changed, cancellationToken);
            return $"Successfully collapsed {removed.Name} into {kept.Name}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error collapsing hierarchy: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// What the merge does besides moving members and renaming references:
    /// which references stay as they are, which members of the removed class
    /// are dropped rather than moved, and how members of the kept class change.
    /// </summary>
    private sealed record Collapse(
        bool IntoBase,
        IReadOnlyCollection<SyntaxNode> KeptReferences,
        IReadOnlyCollection<SyntaxNode> DroppedMembers,
        IReadOnlyDictionary<MemberDeclarationSyntax, Func<SyntaxTokenList, SyntaxTokenList>> KeptModifierChanges);

    /// <summary>The class that stays, and whether it is the base class of the one removed.</summary>
    private static async Task<(INamedTypeSymbol Kept, bool IntoBase)> KeptClassAsync(
        Solution solution,
        INamedTypeSymbol removed,
        string? into,
        CancellationToken cancellationToken)
    {
        var baseClass = removed.BaseType is { SpecialType: not SpecialType.System_Object } b && b.Locations.Any(l => l.IsInSource) ? b : null;
        if (into is null || into == baseClass?.Name)
        {
            return baseClass is not null
                ? (baseClass, true)
                : throw new McpException($"Error: {removed.Name} has no base class declared in the solution to merge into; name a subclass with into");
        }

        var subclasses = await SymbolFinder.FindDerivedClassesAsync(removed, solution, transitive: false, cancellationToken: cancellationToken);
        var subclass = subclasses.FirstOrDefault(s => s.Name == into)
            ?? throw new McpException($"Error: {into} is neither the base class nor a direct subclass of {removed.Name}");
        if (subclasses.Count() > 1)
            throw new McpException($"Error: {removed.Name} has other subclasses besides {into} ({string.Join(", ", subclasses.Where(s => s.Name != into).Select(s => s.Name))}), which would lose their base class");

        return (subclass, false);
    }

    private static void EnsureSupported(INamedTypeSymbol type, TypeDeclarationSyntax declaration)
    {
        var reason =
            declaration is not ClassDeclarationSyntax ? "is not a class"
            : type.IsStatic ? "is static"
            : type.IsGenericType ? "is generic"
            : type.DeclaringSyntaxReferences.Length > 1 ? "is declared in several parts"
            : null;
        if (reason is not null)
            throw new McpException($"Error: {type.Name} cannot be collapsed because it {reason}");
    }

    /// <summary>
    /// Merging a subclass into its base: the base class's own instances and
    /// its other subclasses gain the subclass's members, so none may override
    /// what the base class does or share a name with what is already there,
    /// and nothing may test for the subclass, which every instance would pass.
    /// </summary>
    private static async Task<Collapse> IntoBaseAsync(
        Solution solution,
        INamedTypeSymbol removed,
        INamedTypeSymbol kept,
        IReadOnlyList<(ReferenceLocation Location, SyntaxNode Name)> references,
        CancellationToken cancellationToken)
    {
        if (removed.IsAbstract)
            throw new McpException($"Error: {removed.Name} cannot be collapsed because it is abstract");

        var subclasses = await SymbolFinder.FindDerivedClassesAsync(removed, solution, transitive: false, cancellationToken: cancellationToken);
        if (subclasses.Any())
            throw new McpException($"Error: {removed.Name} has subclasses ({string.Join(", ", subclasses.Select(s => s.Name))}), which would lose their base class");

        var members = removed.GetMembers().Where(m => m.CanBeReferencedByName && !m.IsImplicitlyDeclared).ToList();
        if (members.FirstOrDefault(m => m.IsOverride) is { } overriding)
            throw new McpException($"Error: {removed.Name}.{overriding.Name} overrides a member, and moving it up would change what {kept.Name}'s own instances do");

        var others = (await SymbolFinder.FindDerivedClassesAsync(kept, solution, transitive: true, cancellationToken: cancellationToken))
            .Where(s => !SymbolEqualityComparer.Default.Equals(s, removed));
        var above = Enumerable.Empty<INamedTypeSymbol>();
        for (var current = kept; current is not null; current = current.BaseType)
            above = above.Append(current);
        foreach (var member in members)
        {
            var clash = above.Concat(others).FirstOrDefault(t => t.GetMembers(member.Name).Any());
            if (clash is not null)
                throw new McpException($"Error: {clash.Name} already has a member named '{member.Name}', which the member moving up would clash with");
        }

        foreach (var (location, name) in references)
        {
            if (IsTypeTest(TypeNode(name), includeConversions: true))
                throw new McpException($"Error: {SolutionEdits.Describe(location.Location)} tests for or names {removed.Name}, which would hold for every {kept.Name} once they are one class");
        }

        return new Collapse(true, Array.Empty<SyntaxNode>(), Array.Empty<SyntaxNode>(), new Dictionary<MemberDeclarationSyntax, Func<SyntaxTokenList, SyntaxTokenList>>());
    }

    /// <summary>
    /// Merging a base class into its only subclass: every instance of the base
    /// is already one of the subclass, as long as the base is never created
    /// itself. Its abstract members, and the virtual ones the subclass
    /// overrides, are dropped, and the subclass's overrides of them become
    /// ordinary members, or virtual ones when a class further down overrides
    /// them in turn.
    /// </summary>
    private static async Task<Collapse> IntoSubclassAsync(
        Solution solution,
        INamedTypeSymbol removed,
        INamedTypeSymbol kept,
        TypeDeclarationSyntax keptDeclaration,
        IReadOnlyList<(ReferenceLocation Location, SyntaxNode Name)> references,
        CancellationToken cancellationToken)
    {
        var keptReferences = new List<SyntaxNode>();
        foreach (var (location, name) in references)
        {
            if (name.Ancestors().Contains(keptDeclaration.BaseList))
            {
                keptReferences.Add(name);
                continue;
            }

            var typeNode = TypeNode(name);
            if (typeNode.Parent is ObjectCreationExpressionSyntax creation && creation.Type == typeNode || name is ImplicitObjectCreationExpressionSyntax)
                throw new McpException($"Error: {SolutionEdits.Describe(location.Location)} creates a {removed.Name} itself, which would gain the behaviour of {kept.Name}");
            if (IsTypeTest(typeNode, includeConversions: false))
                throw new McpException($"Error: {SolutionEdits.Describe(location.Location)} tests for or names {removed.Name}, whose name and type would change");
        }

        var keptModel = (await solution.GetDocument(keptDeclaration.SyntaxTree)!.GetSemanticModelAsync(cancellationToken))!;
        var baseCall = keptDeclaration.DescendantNodes().OfType<BaseExpressionSyntax>()
            .FirstOrDefault(b => SymbolEqualityComparer.Default.Equals(keptModel.GetSymbolInfo(b.Parent!).Symbol?.ContainingType, removed));
        if (baseCall is not null)
            throw new McpException($"Error: {kept.Name} calls {removed.Name}'s own version of a member through base ('{baseCall.Parent}'), which merging would leave without one");

        var dropped = new List<SyntaxNode>();
        var modifierChanges = new Dictionary<MemberDeclarationSyntax, Func<SyntaxTokenList, SyntaxTokenList>>();
        foreach (var member in removed.GetMembers().Where(m => m.CanBeReferencedByName && !m.IsImplicitlyDeclared))
        {
            var declaration = member.DeclaringSyntaxReferences.Single().GetSyntax(cancellationToken);
            var overriding = kept.GetMembers(member.Name).FirstOrDefault(m => SymbolEqualityComparer.Default.Equals(OverriddenMember(m), member));
            if (member.IsAbstract || overriding is not null)
                dropped.Add(declaration is VariableDeclaratorSyntax ? declaration.Parent!.Parent! : declaration);

            if (overriding is not null)
            {
                var overridingDeclaration = (MemberDeclarationSyntax)overriding.DeclaringSyntaxReferences.Single().GetSyntax(cancellationToken);
                var overriddenBelow = (await SymbolFinder.FindOverridesAsync(overriding, solution, cancellationToken: cancellationToken)).Any();
                modifierChanges[overridingDeclaration] = member.IsOverride
                    ? modifiers => modifiers
                    : modifiers => overriddenBelow
                        ? HierarchyMemberHelpers.WithModifier(HierarchyMemberHelpers.WithoutModifiers(modifiers, SyntaxKind.OverrideKeyword, SyntaxKind.SealedKeyword), SyntaxKind.VirtualKeyword)
                        : HierarchyMemberHelpers.WithoutModifiers(modifiers, SyntaxKind.OverrideKeyword, SyntaxKind.SealedKeyword);
            }
            else if (kept.GetMembers(member.Name).Any())
            {
                throw new McpException($"Error: {kept.Name} already has a member named '{member.Name}' that does not override {removed.Name}'s, which the member moving down would clash with");
            }
        }

        return new Collapse(false, keptReferences, dropped, modifierChanges);
    }

    private static ISymbol? OverriddenMember(ISymbol member) => member switch
    {
        IMethodSymbol method => method.OverriddenMethod,
        IPropertySymbol property => property.OverriddenProperty,
        IEventSymbol @event => @event.OverriddenEvent,
        _ => null,
    };

    /// <summary>The whole type a reference names: <c>Staff.Salesman</c> rather than <c>Salesman</c>.</summary>
    private static SyntaxNode TypeNode(SyntaxNode name) => name.Parent switch
    {
        QualifiedNameSyntax qualified when qualified.Right == name => qualified,
        AliasQualifiedNameSyntax aliased when aliased.Name == name => aliased,
        _ => name,
    };

    /// <summary>
    /// Whether the type is named by <c>typeof</c> or <c>nameof</c>, whose
    /// results would change, or, with <paramref name="includeConversions"/>,
    /// by a type test, pattern or cast, which would hold for more instances.
    /// </summary>
    private static bool IsTypeTest(SyntaxNode typeNode, bool includeConversions)
    {
        if (typeNode.Parent is TypeOfExpressionSyntax
            || typeNode.Ancestors().OfType<InvocationExpressionSyntax>().Any(i => i.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" }))
            return true;

        return includeConversions && typeNode.Parent switch
        {
            BinaryExpressionSyntax binary => binary.Right == typeNode,
            CastExpressionSyntax => true,
            DeclarationPatternSyntax or TypePatternSyntax or RecursivePatternSyntax => true,
            _ => false,
        };
    }

    /// <summary>Every reference to the class in source, and the name node each one is.</summary>
    private static async Task<IReadOnlyList<(ReferenceLocation Location, SyntaxNode Name)>> ReferencesAsync(
        Solution solution,
        INamedTypeSymbol type,
        CancellationToken cancellationToken)
    {
        var references = new List<(ReferenceLocation, SyntaxNode)>();
        foreach (var location in (await SymbolFinder.FindReferencesAsync(type, solution, cancellationToken)).SelectMany(r => r.Locations))
        {
            if (!location.Location.IsInSource)
                continue;

            var root = (await location.Document.GetSyntaxRootAsync(cancellationToken))!;
            var node = root.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true);
            references.Add((location, node.AncestorsAndSelf().OfType<ImplicitObjectCreationExpressionSyntax>().FirstOrDefault() ?? node));
        }

        return references;
    }

    /// <summary>
    /// Renames every reference to the kept class, moves the removed class's
    /// members and interfaces into the kept one, and deletes the removed
    /// class, with its file when nothing else is left in it.
    /// </summary>
    private static async Task<Solution> ApplyAsync(
        Solution solution,
        Document removedDocument,
        TypeDeclarationSyntax removedDeclaration,
        TypeDeclarationSyntax keptDeclaration,
        INamedTypeSymbol kept,
        Collapse collapse,
        CancellationToken cancellationToken)
    {
        var removedModel = (await removedDocument.GetSemanticModelAsync(cancellationToken))!;
        var removedSymbol = removedModel.GetDeclaredSymbol(removedDeclaration, cancellationToken)!;
        var keptDocument = solution.GetDocument(keptDeclaration.SyntaxTree)!;
        var namespaces = removedDeclaration.Members.Cast<SyntaxNode>()
            .Concat(removedDeclaration.BaseList?.Types.Cast<SyntaxNode>() ?? Enumerable.Empty<SyntaxNode>())
            .SelectMany(n => TypeRefactoringHelpers.NamespacesUsedBy(n, removedModel))
            .Distinct()
            .ToList();

        var renames = new List<(DocumentId Document, SyntaxNode Replaced)>();
        foreach (var (location, name) in await ReferencesAsync(solution, removedSymbol, cancellationToken))
        {
            if (name is SimpleNameSyntax && !collapse.KeptReferences.Contains(name))
                renames.Add((location.Document.Id, TypeNode(name)));
        }

        var interfaces = removedDeclaration.BaseList?.Types
            .Where(t => removedModel.GetTypeInfo(t.Type, cancellationToken).Type is { TypeKind: TypeKind.Interface })
            .ToList() ?? new List<BaseTypeSyntax>();
        var baseClassEntry = removedDeclaration.BaseList?.Types.FirstOrDefault(t => !interfaces.Contains(t));
        var keptBaseEntry = keptDeclaration.BaseList?.Types.FirstOrDefault(t => collapse.KeptReferences.Any(r => t.Span.Contains(r.Span)));

        // Every node an edit names is tracked, so the edits can be made in turn
        // in whichever documents they share.
        var tracked = renames.Select(r => r.Replaced)
            .Concat(new SyntaxNode[] { removedDeclaration, keptDeclaration })
            .Concat(removedDeclaration.Members)
            .Concat(interfaces)
            .Concat(baseClassEntry is null ? Array.Empty<SyntaxNode>() : new SyntaxNode[] { baseClassEntry })
            .Concat(keptBaseEntry is null ? Array.Empty<SyntaxNode>() : new SyntaxNode[] { keptBaseEntry })
            .Concat(collapse.KeptModifierChanges.Keys)
            .ToList();
        var roots = new Dictionary<DocumentId, SyntaxNode>();
        async Task<SyntaxNode> RootAsync(DocumentId id)
        {
            if (!roots.TryGetValue(id, out var root))
            {
                root = (await solution.GetDocument(id)!.GetSyntaxRootAsync(cancellationToken))!;
                root = root.TrackNodes(tracked.Where(n => n.SyntaxTree == root.SyntaxTree));
            }

            return root;
        }

        var keptName = SyntaxFactory.ParseTypeName(kept.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        foreach (var (id, replaced) in renames)
        {
            var root = await RootAsync(id);
            var current = root.GetCurrentNode(replaced)!;
            roots[id] = root.ReplaceNode(current, keptName.WithTriviaFrom(current).WithAdditionalAnnotations(Simplifier.Annotation, Simplifier.AddImportsAnnotation));
        }

        // The removed class as the renames left it, so its members move renamed.
        var removedRoot = await RootAsync(removedDocument.Id);
        var moved = removedDeclaration.Members
            .Where(m => !collapse.DroppedMembers.Contains(m))
            .Select(m => removedRoot.GetCurrentNode(m)!)
            .ToList();
        var movedInterfaces = interfaces.Select(i => removedRoot.GetCurrentNode(i)!).ToList();
        var newBase = baseClassEntry is null ? null : removedRoot.GetCurrentNode(baseClassEntry);

        var keptRoot = await RootAsync(keptDocument.Id);
        var type = keptRoot.GetCurrentNode(keptDeclaration)!;
        foreach (var (member, change) in collapse.KeptModifierChanges)
            type = type.ReplaceNode(type.GetCurrentNode(member)!, HierarchyMemberHelpers.WithModifiers(type.GetCurrentNode(member)!, change));

        if (!collapse.IntoBase && keptBaseEntry is not null)
        {
            var entry = type.GetCurrentNode(keptBaseEntry)!;
            type = newBase is not null
                ? type.ReplaceNode(entry, entry.WithType(newBase.Type.WithTriviaFrom(entry.Type)))
                : ChangeBaseTypeTool.WithoutBase(type, entry);
        }

        foreach (var entry in movedInterfaces.Where(i => type.BaseList?.Types.Any(t => t.IsEquivalentTo(i)) != true))
            type = TypeRefactoringHelpers.AddBaseType(type, entry.Type.WithoutTrivia(), first: false);

        var endOfLine = TypeDeclarations.NewLine(type);
        foreach (var member in moved)
            type = HierarchyMemberHelpers.InsertMember(type, member, endOfLine);

        keptRoot = keptRoot.ReplaceNode(keptRoot.GetCurrentNode(keptDeclaration)!, type);
        roots[keptDocument.Id] = TypeRefactoringHelpers.AddUsings((CompilationUnitSyntax)keptRoot, namespaces);

        removedRoot = await RootAsync(removedDocument.Id);
        removedRoot = DeclarationRemoval.RemoveMembers((CompilationUnitSyntax)removedRoot, new[] { (MemberDeclarationSyntax)removedRoot.GetCurrentNode(removedDeclaration)! });
        roots[removedDocument.Id] = removedRoot;

        var changed = solution;
        foreach (var (id, root) in roots)
        {
            var document = changed.WithDocumentSyntaxRoot(id, root).GetDocument(id)!;
            document = await ImportAdder.AddImportsAsync(document, Simplifier.AddImportsAnnotation, cancellationToken: cancellationToken);
            document = await Simplifier.ReduceAsync(document, Simplifier.Annotation, cancellationToken: cancellationToken);
            document = await Formatter.FormatAsync(document, Formatter.Annotation, cancellationToken: cancellationToken);
            changed = document.Project.Solution;
        }

        if (DeclarationRemoval.IsEmpty((CompilationUnitSyntax)(await changed.GetDocument(removedDocument.Id)!.GetSyntaxRootAsync(cancellationToken))!))
            changed = changed.RemoveDocument(removedDocument.Id);

        return changed;
    }
}
