using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.Collections.Immutable;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using System.Threading;

[McpServerToolType]
public static class RemoveMiddleManTool
{
    [McpServerTool, Description("Remove the methods and properties of a class that only delegate to one of its fields or properties, making their callers use that delegate directly")]
    public static async Task<string> RemoveMiddleMan(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the class")] string filePath,
        [Description("Name of the class that delegates")] string className,
        [Description("Name of the field or property holding the delegate")] string via,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (solution, document) = await TypeRefactoringHelpers.LoadDocumentAsync(solutionPath, filePath, cancellationToken);
            var (type, _) = await TypeRefactoringHelpers.FindTypeAsync(document, className, cancellationToken);
            var viaSymbol = type.GetMembers(via).FirstOrDefault(m => m is IFieldSymbol or IPropertySymbol)
                ?? throw new McpException($"Error: {type.Name} has no field or property named '{via}' to delegate through");

            var delegating = await DelegatingMembersAsync(solution, type, viaSymbol, cancellationToken);
            if (delegating.Count == 0)
                throw new McpException($"Error: {type.Name} has no method or property that only delegates to '{via}'");

            foreach (var member in delegating)
                await EnsureCallersCanReachAsync(solution, member, viaSymbol, cancellationToken);

            var changed = await RedirectPropertiesAsync(solution, delegating.Where(d => d.Symbol is IPropertySymbol).ToList(), cancellationToken);
            foreach (var method in delegating.Where(d => d.Symbol is IMethodSymbol))
            {
                var current = await SolutionEdits.ResolveAsync(changed, SolutionEdits.ProjectOf(solution, method.Symbol), (IMethodSymbol)method.Symbol, cancellationToken);
                var reference = current.DeclaringSyntaxReferences.Single();
                var declaration = (MethodDeclarationSyntax)await reference.GetSyntaxAsync(cancellationToken);
                (changed, _) = await MethodInliner.InlineInSolutionAsync(changed.GetDocument(reference.SyntaxTree)!, declaration, cancellationToken);
            }

            await SolutionEdits.EnsureCompilesAsync(solution, changed, cancellationToken);
            await SolutionEdits.WriteAsync(solution, changed, cancellationToken);
            return $"Successfully removed {delegating.Count} delegating member(s) of {type.Name}: {string.Join(", ", delegating.Select(d => d.Symbol.Name))}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error removing middle man: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// A member that only forwards to the delegate, and the expression it
    /// forwards to: <c>Department.Manager</c> or <c>Department.Budget(year)</c>.
    /// </summary>
    private sealed record DelegatingMember(ISymbol Symbol, MemberDeclarationSyntax Declaration, ExpressionSyntax Forwarded, ISymbol Target);

    /// <summary>
    /// Methods whose body is one call of a member of the delegate, passing
    /// their parameters in order, or one read of a member of it; and get-only
    /// properties that read a member of the delegate. Members that callers
    /// reach through dispatch (virtual, overriding or implementing an
    /// interface) are kept.
    /// </summary>
    private static async Task<List<DelegatingMember>> DelegatingMembersAsync(
        Solution solution,
        INamedTypeSymbol type,
        ISymbol via,
        CancellationToken cancellationToken)
    {
        var members = new List<DelegatingMember>();
        foreach (var symbol in type.GetMembers().Where(m => m is IMethodSymbol { MethodKind: MethodKind.Ordinary } or IPropertySymbol { IsIndexer: false }))
        {
            if (symbol.IsStatic || symbol.IsVirtual || symbol.IsAbstract || symbol.IsOverride || ImplementsInterface(symbol))
                continue;

            if (symbol.DeclaringSyntaxReferences is not [var reference])
                continue;

            var declaration = (MemberDeclarationSyntax)await reference.GetSyntaxAsync(cancellationToken);
            var model = await solution.GetDocument(reference.SyntaxTree)!.GetSemanticModelAsync(cancellationToken);
            if (Forwarded(declaration) is not { } forwarded || model is null)
                continue;

            var parameters = symbol is IMethodSymbol method ? method.Parameters : ImmutableArray<IParameterSymbol>.Empty;
            if (ForwardedTarget(forwarded, parameters, via, model) is { } target)
                members.Add(new DelegatingMember(symbol, declaration, forwarded, target));
        }

        return members;
    }

    private static bool ImplementsInterface(ISymbol symbol) =>
        symbol.ContainingType.AllInterfaces
            .SelectMany(i => i.GetMembers())
            .Any(m => SymbolEqualityComparer.Default.Equals(symbol.ContainingType.FindImplementationForInterfaceMember(m), symbol));

    /// <summary>The single expression a method or get-only property returns or evaluates.</summary>
    private static ExpressionSyntax? Forwarded(MemberDeclarationSyntax declaration) => declaration switch
    {
        MethodDeclarationSyntax { ExpressionBody: { } arrow } => arrow.Expression,
        MethodDeclarationSyntax { Body.Statements: [ReturnStatementSyntax { Expression: { } returned }] } => returned,
        MethodDeclarationSyntax { Body.Statements: [ExpressionStatementSyntax { Expression: { } called }] } => called,
        PropertyDeclarationSyntax { ExpressionBody: { } arrow } => arrow.Expression,
        PropertyDeclarationSyntax { AccessorList.Accessors: [{ Keyword.RawKind: (int)SyntaxKind.GetKeyword } getter] } => getter switch
        {
            { ExpressionBody: { } arrow } => arrow.Expression,
            { Body.Statements: [ReturnStatementSyntax { Expression: { } returned }] } => returned,
            _ => null,
        },
        _ => null,
    };

    /// <summary>
    /// The member of the delegate that <paramref name="forwarded"/> reaches
    /// when it is <c>via.Member</c>, or <c>via.Method(p1, p2, ...)</c> passing
    /// the member's own parameters in order; otherwise null.
    /// </summary>
    private static ISymbol? ForwardedTarget(ExpressionSyntax forwarded, ImmutableArray<IParameterSymbol> parameters, ISymbol via, SemanticModel model)
    {
        var access = forwarded;
        if (forwarded is InvocationExpressionSyntax invocation)
        {
            var arguments = invocation.ArgumentList.Arguments;
            if (arguments.Count != parameters.Length)
                return null;
            for (var i = 0; i < arguments.Count; i++)
            {
                if (arguments[i].NameColon is not null
                    || !arguments[i].RefKindKeyword.IsKind(SyntaxKind.None)
                    || !SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(arguments[i].Expression).Symbol, parameters[i]))
                    return null;
            }

            access = invocation.Expression;
        }
        else if (parameters.Length > 0)
        {
            return null;
        }

        if (access is not MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } memberAccess
            || memberAccess.Expression is not (IdentifierNameSyntax or MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: IdentifierNameSyntax })
            || !SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(memberAccess.Expression).Symbol, via))
            return null;

        return model.GetSymbolInfo(forwarded).Symbol;
    }

    /// <summary>
    /// Once the member is gone its callers reach the delegate and its member
    /// themselves, so both must be accessible wherever the member is used.
    /// </summary>
    private static async Task EnsureCallersCanReachAsync(Solution solution, DelegatingMember member, ISymbol via, CancellationToken cancellationToken)
    {
        foreach (var location in await ReferencesAsync(solution, member.Symbol, cancellationToken))
        {
            var model = (await location.Document.GetSemanticModelAsync(cancellationToken))!;
            var position = location.Location.SourceSpan.Start;
            if (!model.IsAccessible(position, via) || !model.IsAccessible(position, member.Target))
            {
                throw new McpException(
                    $"Error: '{via.Name}' is not accessible where {SolutionEdits.Describe(location.Location)} uses '{member.Symbol.Name}'; make the delegate accessible first");
            }
        }
    }

    private static async Task<IReadOnlyList<ReferenceLocation>> ReferencesAsync(Solution solution, ISymbol symbol, CancellationToken cancellationToken) =>
        (await SymbolFinder.FindReferencesAsync(symbol, solution, cancellationToken))
            .SelectMany(r => r.Locations)
            .Where(l => l.Location.IsInSource)
            .ToList();

    /// <summary>
    /// Replaces each use of a delegating property with what it forwards to,
    /// reached through the same receiver: <c>person.Manager</c> becomes
    /// <c>person.Department.Manager</c>, <c>person?.Manager</c> becomes
    /// <c>person?.Department.Manager</c>, and <c>Manager</c> inside the class
    /// becomes <c>Department.Manager</c>. Then removes the property.
    /// </summary>
    private static async Task<Solution> RedirectPropertiesAsync(Solution solution, IReadOnlyList<DelegatingMember> properties, CancellationToken cancellationToken)
    {
        var replacements = new Dictionary<DocumentId, Dictionary<SyntaxNode, SyntaxNode>>();
        var removals = new Dictionary<DocumentId, List<MemberDeclarationSyntax>>();
        foreach (var property in properties)
        {
            foreach (var location in await ReferencesAsync(solution, property.Symbol, cancellationToken))
            {
                var root = (await location.Document.GetSyntaxRootAsync(cancellationToken))!;
                var name = (SimpleNameSyntax)root.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true);
                var (replaced, replacement) = Redirect(name, property.Forwarded);
                if (!replacements.TryGetValue(location.Document.Id, out var edits))
                    replacements[location.Document.Id] = edits = new();
                edits[replaced] = replacement.WithTriviaFrom(replaced);
            }

            var declaringDocument = solution.GetDocument(property.Declaration.SyntaxTree)!;
            if (!removals.TryGetValue(declaringDocument.Id, out var removed))
                removals[declaringDocument.Id] = removed = new();
            removed.Add(property.Declaration);
        }

        var changed = solution;
        foreach (var id in replacements.Keys.Union(removals.Keys))
        {
            var edits = replacements.GetValueOrDefault(id) ?? new();
            var declarations = removals.GetValueOrDefault(id) ?? new();
            var root = (await solution.GetDocument(id)!.GetSyntaxRootAsync(cancellationToken))!;
            root = root.TrackNodes(edits.Keys.Concat(declarations));
            foreach (var (replaced, replacement) in edits)
                root = root.ReplaceNode(root.GetCurrentNode(replaced)!, replacement);
            foreach (var declaration in declarations)
            {
                var current = root.GetCurrentNode(declaration)!;
                var type = (TypeDeclarationSyntax)current.Parent!;
                root = root.ReplaceNode(type, HierarchyMemberHelpers.RemoveMember(type, current));
            }

            changed = changed.WithDocumentSyntaxRoot(id, root);
        }

        return changed;
    }

    /// <summary>The node a use of the property is replaced at, and what replaces it.</summary>
    private static (SyntaxNode Replaced, SyntaxNode Replacement) Redirect(SimpleNameSyntax name, ExpressionSyntax forwarded)
    {
        var access = (MemberAccessExpressionSyntax)(forwarded is InvocationExpressionSyntax invocation ? invocation.Expression : forwarded);
        var viaName = access.Expression is MemberAccessExpressionSyntax thisAccess ? thisAccess.Name : (SimpleNameSyntax)access.Expression;
        viaName = viaName.WithoutTrivia();

        switch (name.Parent)
        {
            case MemberAccessExpressionSyntax receiverAccess when receiverAccess.Name == name:
                var throughReceiver = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, receiverAccess.Expression, viaName);
                return (receiverAccess, Rebase(forwarded, throughReceiver));
            case MemberBindingExpressionSyntax binding:
                return (binding, Rebase(forwarded, SyntaxFactory.MemberBindingExpression(viaName)));
            default:
                return (name, forwarded.WithoutTrivia());
        }
    }

    /// <summary>
    /// <paramref name="forwarded"/> with the delegate reached through
    /// <paramref name="via"/>: the outermost member access in it is the one on
    /// the delegate.
    /// </summary>
    private static ExpressionSyntax Rebase(ExpressionSyntax forwarded, ExpressionSyntax via)
    {
        var bare = forwarded.WithoutTrivia();
        var access = bare.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>().First();
        return bare.ReplaceNode(access.Expression, via);
    }
}
