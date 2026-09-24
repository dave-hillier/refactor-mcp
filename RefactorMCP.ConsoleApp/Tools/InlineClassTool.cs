using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using System.Threading;

[McpServerToolType]
public static class InlineClassTool
{
    [McpServerTool, Description("Move every member of a class into the one class that holds an instance of it in a field or get-only property, make the uses through that holder direct, and delete the class")]
    public static async Task<string> InlineClass(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the class to inline")] string filePath,
        [Description("Name of the class to inline")] string className,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (solution, document) = await TypeRefactoringHelpers.LoadDocumentAsync(solutionPath, filePath, cancellationToken);
            var (type, declaration) = await TypeRefactoringHelpers.FindTypeAsync(document, className, cancellationToken);
            EnsureInlinable(type, declaration);

            var holder = await HolderAsync(solution, type, cancellationToken);
            EnsureNoClashes(type, holder);
            var uses = await DirectUsesAsync(solution, type, holder.Symbol, cancellationToken);

            var changed = await ApplyAsync(solution, document, declaration, holder, uses, cancellationToken);
            await SolutionEdits.EnsureCompilesAsync(solution, changed, cancellationToken);
            await SolutionEdits.WriteAsync(solution, changed, cancellationToken);
            return $"Successfully inlined {type.Name} into {holder.Symbol.ContainingType.Name}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error inlining class: {ex.Message}", ex);
        }
    }

    /// <summary>The field or property holding the only instance, and its declaration.</summary>
    private sealed record Holder(ISymbol Symbol, SyntaxNode Declaration, Document Document);

    /// <summary>
    /// The class must be plain enough that its members mean the same once
    /// they belong to another class: no base class, interfaces, constructors,
    /// finalizer, static members or nested types, and not generic, partial,
    /// static or abstract.
    /// </summary>
    private static void EnsureInlinable(INamedTypeSymbol type, TypeDeclarationSyntax declaration)
    {
        var reason =
            declaration is not ClassDeclarationSyntax ? "is not a class"
            : type.IsStatic || type.IsAbstract ? "is static or abstract"
            : type.IsGenericType ? "is generic"
            : type.DeclaringSyntaxReferences.Length > 1 ? "is declared in several parts"
            : type.BaseType is { SpecialType: not SpecialType.System_Object } ? "derives from another class"
            : type.Interfaces.Length > 0 ? "implements interfaces"
            : type.Constructors.Any(c => !c.IsImplicitlyDeclared) ? "declares a constructor"
            : type.GetMembers().Any(m => m.CanBeReferencedByName && m.IsStatic) ? "declares static members"
            : type.GetMembers().OfType<IMethodSymbol>().Any(m => m.MethodKind == MethodKind.Destructor) ? "declares a finalizer"
            : type.GetTypeMembers().Length > 0 ? "declares nested types"
            : null;
        if (reason is not null)
            throw new McpException($"Error: {type.Name} cannot be inlined because it {reason}");
    }

    /// <summary>
    /// The one instance field or get-only auto-property that creates an
    /// instance of the class in its initializer. Every other reference to the
    /// class, such as a parameter or local type, would be left naming a class
    /// that no longer exists.
    /// </summary>
    private static async Task<Holder> HolderAsync(Solution solution, INamedTypeSymbol type, CancellationToken cancellationToken)
    {
        var holders = new List<Holder>();
        var creations = new List<(BaseObjectCreationExpressionSyntax Creation, Location Location)>();
        foreach (var location in await ReferencesAsync(solution, type, cancellationToken))
        {
            var root = (await location.Document.GetSyntaxRootAsync(cancellationToken))!;
            var model = (await location.Document.GetSemanticModelAsync(cancellationToken))!;
            var name = root.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true);
            if (name.AncestorsAndSelf().OfType<ImplicitObjectCreationExpressionSyntax>().FirstOrDefault() is { } implicitCreation)
            {
                creations.Add((implicitCreation, location.Location));
                continue;
            }

            switch (name.Parent)
            {
                case VariableDeclarationSyntax { Parent: FieldDeclarationSyntax } variables when variables.Type == name:
                    holders.AddRange(variables.Variables.Select(v => new Holder(model.GetDeclaredSymbol(v, cancellationToken)!, v, location.Document)));
                    continue;
                case PropertyDeclarationSyntax property when property.Type == name:
                    holders.Add(new Holder(model.GetDeclaredSymbol(property, cancellationToken)!, property, location.Document));
                    continue;
                case ObjectCreationExpressionSyntax creation when creation.Type == name:
                    creations.Add((creation, location.Location));
                    continue;
            }

            throw new McpException($"Error: {type.Name} is referenced at {SolutionEdits.Describe(location.Location)}, which would be left naming a class that no longer exists");
        }

        holders = holders.Where(h => !SymbolEqualityComparer.Default.Equals(h.Symbol.ContainingType, type)).ToList();
        if (holders.Count == 0)
            throw new McpException($"Error: No field or property holds an instance of {type.Name}, so there is no class to inline it into");
        if (holders.Count > 1)
            throw new McpException($"Error: {type.Name} is held by several fields or properties ({string.Join(", ", holders.Select(h => h.Symbol.Name))}), each of which would need its own copy of its members");

        var holder = holders[0];
        var initializer = holder.Declaration switch
        {
            VariableDeclaratorSyntax variable => variable.Initializer,
            PropertyDeclarationSyntax { AccessorList.Accessors: [{ Keyword.RawKind: (int)SyntaxKind.GetKeyword, Body: null, ExpressionBody: null }] } property => property.Initializer,
            _ => null,
        };
        var creates = initializer?.Value is BaseObjectCreationExpressionSyntax { Initializer: null } created
            && (created.ArgumentList is null || created.ArgumentList.Arguments.Count == 0);
        if (holder.Symbol.IsStatic || !creates)
        {
            throw new McpException(
                $"Error: {holder.Symbol.Name} must be an instance field or get-only property that creates the object in its initializer, so each {holder.Symbol.ContainingType.Name} has exactly one from the start");
        }

        // The holder's initializer is the one place the class may be created.
        var elsewhere = creations.FirstOrDefault(c => c.Creation != initializer!.Value);
        if (elsewhere.Creation is not null)
            throw new McpException($"Error: {type.Name} is referenced at {SolutionEdits.Describe(elsewhere.Location)}, which would be left naming a class that no longer exists");

        return holder;
    }

    /// <summary>
    /// A member of the class takes its name into the holding class, where
    /// that name must be free, including in the classes it derives from.
    /// </summary>
    private static void EnsureNoClashes(INamedTypeSymbol type, Holder holder)
    {
        foreach (var member in type.GetMembers().Where(m => m.CanBeReferencedByName))
        {
            for (var current = holder.Symbol.ContainingType; current is not null; current = current.BaseType)
            {
                if (current.GetMembers(member.Name).Any(m => !SymbolEqualityComparer.Default.Equals(m, holder.Symbol)))
                    throw new McpException($"Error: {current.Name} already has a member named '{member.Name}', which the member of {type.Name} would clash with");
            }
        }
    }

    /// <summary>
    /// Each use of the holder must reach a member of the class, and becomes a
    /// direct use of that member: <c>_address.Street</c> becomes <c>Street</c>
    /// (or <c>this.Street</c> where a local hides it), <c>this._address.Street</c>
    /// becomes <c>this.Street</c>, and <c>customer.Address.Street</c> becomes
    /// <c>customer.Street</c>.
    /// </summary>
    private static async Task<List<(Document Document, SyntaxNode Replaced, SyntaxNode Replacement)>> DirectUsesAsync(
        Solution solution,
        INamedTypeSymbol type,
        ISymbol holder,
        CancellationToken cancellationToken)
    {
        var uses = new List<(Document, SyntaxNode, SyntaxNode)>();
        foreach (var location in await ReferencesAsync(solution, holder, cancellationToken))
        {
            var root = (await location.Document.GetSyntaxRootAsync(cancellationToken))!;
            var model = (await location.Document.GetSemanticModelAsync(cancellationToken))!;
            var name = (SimpleNameSyntax)root.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true);
            var use = name.Parent is MemberAccessExpressionSyntax qualified && qualified.Name == name ? (ExpressionSyntax)qualified : name;

            if (use.Parent is not MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access
                || access.Expression != use
                || !SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(access.Name).Symbol?.ContainingType, type))
            {
                throw new McpException(
                    $"Error: {SolutionEdits.Describe(location.Location)} uses {holder.Name} other than to reach a member of {type.Name}, which has nothing to become once the object is gone");
            }

            ExpressionSyntax replacement = use switch
            {
                MemberAccessExpressionSyntax receiver => receiver.WithName(access.Name),
                _ when Hidden(model, access) => SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.ThisExpression(), access.Name),
                _ => access.Name,
            };
            uses.Add((location.Document, access, replacement.WithTriviaFrom(access)));
        }

        return uses;
    }

    /// <summary>Whether a local, parameter or range variable of the member's name is in scope at the use.</summary>
    private static bool Hidden(SemanticModel model, MemberAccessExpressionSyntax access) =>
        model.LookupSymbols(access.SpanStart, name: access.Name.Identifier.ValueText)
            .Any(s => s is ILocalSymbol or IParameterSymbol or IRangeVariableSymbol);

    private static async Task<IReadOnlyList<ReferenceLocation>> ReferencesAsync(Solution solution, ISymbol symbol, CancellationToken cancellationToken) =>
        (await SymbolFinder.FindReferencesAsync(symbol, solution, cancellationToken))
            .SelectMany(r => r.Locations)
            .Where(l => l.Location.IsInSource)
            .ToList();

    /// <summary>
    /// Rewrites the uses, then moves the class's members into the holding
    /// class in place of the holder, adds the usings they need there, and
    /// deletes the class, with its file when nothing else is left in it.
    /// </summary>
    private static async Task<Solution> ApplyAsync(
        Solution solution,
        Document classDocument,
        TypeDeclarationSyntax declaration,
        Holder holder,
        IReadOnlyList<(Document Document, SyntaxNode Replaced, SyntaxNode Replacement)> uses,
        CancellationToken cancellationToken)
    {
        var classModel = (await classDocument.GetSemanticModelAsync(cancellationToken))!;
        var namespaces = declaration.Members.SelectMany(m => TypeRefactoringHelpers.NamespacesUsedBy(m, classModel)).Distinct().ToList();
        var holderType = holder.Declaration.Ancestors().OfType<TypeDeclarationSyntax>().First();
        var holderMember = holder.Declaration switch
        {
            VariableDeclaratorSyntax { Parent.Parent: FieldDeclarationSyntax { Declaration.Variables.Count: 1 } field } => field,
            VariableDeclaratorSyntax variable => variable,
            _ => holder.Declaration,
        };

        // Nodes are tracked so that the uses, the holder, and the two types can
        // be edited in turn, in whichever documents they share.
        var roots = new Dictionary<DocumentId, SyntaxNode>();
        async Task<SyntaxNode> RootAsync(Document document)
        {
            if (!roots.TryGetValue(document.Id, out var root))
            {
                var original = (await document.GetSyntaxRootAsync(cancellationToken))!;
                var nodes = uses.Where(u => u.Document.Id == document.Id).Select(u => u.Replaced)
                    .Concat(new[] { declaration, holderType, holderMember }.Where(n => n.SyntaxTree == original.SyntaxTree));
                root = original.TrackNodes(nodes);
            }

            return root;
        }

        foreach (var use in uses)
        {
            var root = await RootAsync(use.Document);
            roots[use.Document.Id] = root.ReplaceNode(root.GetCurrentNode(use.Replaced)!, use.Replacement);
        }

        // The members as the uses left them, so uses inside the class move rewritten.
        var classRoot = await RootAsync(classDocument);
        var members = classRoot.GetCurrentNode(declaration)!.Members;

        var holderRoot = await RootAsync(holder.Document);
        var currentType = holderRoot.GetCurrentNode(holderType)!;
        var currentHolder = currentType.GetCurrentNode(holderMember)!;
        currentType = currentHolder is MemberDeclarationSyntax holderDeclaration
            ? HierarchyMemberHelpers.RemoveMember(currentType, holderDeclaration)
            : currentType.RemoveNode(currentHolder, SyntaxRemoveOptions.KeepNoTrivia)!;

        var endOfLine = TypeDeclarations.NewLine(currentType);
        foreach (var member in members)
            currentType = HierarchyMemberHelpers.InsertMember(currentType, member.WithAdditionalAnnotations(Formatter.Annotation), endOfLine);
        holderRoot = holderRoot.ReplaceNode(holderRoot.GetCurrentNode(holderType)!, currentType);
        roots[holder.Document.Id] = TypeRefactoringHelpers.AddUsings((CompilationUnitSyntax)holderRoot, namespaces);

        classRoot = await RootAsync(classDocument);
        classRoot = DeclarationRemoval.RemoveMembers((CompilationUnitSyntax)classRoot, new[] { (MemberDeclarationSyntax)classRoot.GetCurrentNode(declaration)! });
        roots[classDocument.Id] = classRoot;

        var changed = solution;
        foreach (var (id, root) in roots)
        {
            changed = changed.WithDocumentSyntaxRoot(id, root);
            changed = (await Formatter.FormatAsync(changed.GetDocument(id)!, Formatter.Annotation, cancellationToken: cancellationToken)).Project.Solution;
        }

        if (DeclarationRemoval.IsEmpty((CompilationUnitSyntax)classRoot))
            changed = changed.RemoveDocument(classDocument.Id);

        return changed;
    }
}
