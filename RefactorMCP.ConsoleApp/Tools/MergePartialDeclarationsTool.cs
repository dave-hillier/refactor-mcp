using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

[McpServerToolType]
public static class MergePartialDeclarationsTool
{
    /// <summary>Compiler errors that mean an added using directive clashes with the receiving file's names.</summary>
    private static readonly HashSet<string> ImportConflicts = new(StringComparer.Ordinal)
    {
        "CS0104", // ambiguous reference
        "CS0576", // alias conflicts with a declaration
        "CS1537", // alias defined twice
    };

    [McpServerTool, Description("Merge every part of a partial type into the declaration in the given file, deleting files left empty")]
    public static async Task<string> MergePartialDeclarations(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file whose declaration receives the members")] string filePath,
        [Description("Name of the type")] string typeName,
        [Description("A line of the receiving declaration (1-based), when the file has several")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var (target, type) = await FindPartAsync(solution, filePath, typeName, line, cancellationToken);

            var parts = (await TypeDeclarations.DeclarationsAsync(type, cancellationToken)).Cast<TypeDeclarationSyntax>().ToList();
            if (parts.Count < 2)
                throw new McpException($"Error: '{typeName}' has only one declaration, so there is nothing to merge");

            var others = parts
                .Where(p => p != target)
                .OrderBy(p => p.SyntaxTree == target.SyntaxTree ? 0 : 1)
                .ThenBy(p => p.SyntaxTree.FilePath, StringComparer.Ordinal)
                .ThenBy(p => p.SpanStart)
                .ToList();

            var models = new Dictionary<SyntaxTree, SemanticModel>();
            foreach (var tree in parts.Select(p => p.SyntaxTree).Distinct())
                models[tree] = (await solution.GetDocument(tree)!.GetSemanticModelAsync(cancellationToken))!;

            var merged = Merge(target, others, parts, models);
            var usings = others
                .Where(p => p.SyntaxTree != target.SyntaxTree)
                .SelectMany(p => NeededUsings(p, models[p.SyntaxTree]))
                .ToList();

            var changed = solution;
            foreach (var tree in others.Select(p => p.SyntaxTree).Append(target.SyntaxTree).Distinct())
            {
                var document = solution.GetDocument(tree)!;
                var root = (CompilationUnitSyntax)await tree.GetRootAsync(cancellationToken);
                var removed = others.Where(p => p.SyntaxTree == tree).ToList();
                root = root.TrackNodes(tree == target.SyntaxTree ? removed.Append<SyntaxNode>(target) : removed);

                if (tree == target.SyntaxTree)
                    root = root.ReplaceNode(root.GetCurrentNode(target)!, merged);

                foreach (var part in removed)
                    root = RemoveMember(root, root.GetCurrentNode(part)!);

                if (tree == target.SyntaxTree)
                {
                    root = TypeDeclarations.WithUsings(root, usings);
                    root = (CompilationUnitSyntax)Formatter.Format(root, MovedAnnotation, RefactoringHelpers.SharedWorkspace);
                    changed = changed.WithDocumentSyntaxRoot(document.Id, root);
                }
                else if (DeclaresNothing(root))
                {
                    changed = changed.RemoveDocument(document.Id);
                }
                else
                {
                    changed = changed.WithDocumentSyntaxRoot(document.Id, RemoveEmptyNamespaces(root));
                }
            }

            await EnsureCompilesAsync(solution, changed, cancellationToken);
            await SolutionEdits.WriteAsync(solution, changed, cancellationToken);

            return $"Successfully merged {parts.Count} declarations of '{typeName}' into {filePath}";
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error merging partial declarations: {ex.Message}", ex);
        }
    }

    private static readonly SyntaxAnnotation MovedAnnotation = new("MergedPartialMember");

    /// <summary>The part in the file that receives the members: the one on <paramref name="line"/>, or the first.</summary>
    private static async Task<(TypeDeclarationSyntax Part, INamedTypeSymbol Type)> FindPartAsync(
        Solution solution,
        string filePath,
        string typeName,
        int? line,
        CancellationToken cancellationToken)
    {
        var document = RefactoringHelpers.GetDocumentByPath(solution, filePath)
            ?? throw new McpException($"Error: File {filePath} not found in solution");
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var model = await document.GetSemanticModelAsync(cancellationToken);

        var part = root!.DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .Where(t => t.Identifier.ValueText == typeName)
            .Where(t => line is null || SpansLine(t, line.Value))
            .OrderBy(t => t.SpanStart)
            .FirstOrDefault()
            ?? throw new McpException($"Error: No class, struct, record or interface named '{typeName}' found in {filePath}");

        return (part, model!.GetDeclaredSymbol(part, cancellationToken)!);
    }

    private static bool SpansLine(SyntaxNode node, int line)
    {
        var span = node.GetLocation().GetLineSpan();
        return span.StartLinePosition.Line + 1 <= line && line <= span.EndLinePosition.Line + 1;
    }

    /// <summary>The receiving declaration with every other part's members, modifiers, base types and constraints.</summary>
    private static TypeDeclarationSyntax Merge(
        TypeDeclarationSyntax target,
        IReadOnlyList<TypeDeclarationSyntax> others,
        IReadOnlyList<TypeDeclarationSyntax> parts,
        IReadOnlyDictionary<SyntaxTree, SemanticModel> models)
    {
        var newLine = TypeDeclarations.NewLine(target);
        var members = target.Members.ToList();
        var closeTrivia = target.CloseBraceToken.LeadingTrivia;
        var moved = new List<MemberDeclarationSyntax>();

        // Directives before the receiving closing brace, such as #endregion, close over its own members.
        var carried = Significant(closeTrivia) ? closeTrivia.ToList() : new List<SyntaxTrivia>();
        if (carried.Count > 0)
            closeTrivia = SyntaxFactory.TriviaList(closeTrivia.Where(t => t.IsKind(SyntaxKind.WhitespaceTrivia)).TakeLast(1));

        foreach (var part in others)
        {
            var above = part.GetLeadingTrivia()
                .Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) || t.IsKind(SyntaxKind.MultiLineCommentTrivia) || t.IsDirective)
                .SelectMany(t => t.IsDirective ? new[] { t } : new[] { t, newLine });
            var header = new List<SyntaxTrivia> { newLine };
            header.AddRange(carried);
            header.AddRange(above);
            carried = Significant(part.CloseBraceToken.LeadingTrivia)
                ? part.CloseBraceToken.LeadingTrivia.ToList()
                : new List<SyntaxTrivia>();

            for (var i = 0; i < part.Members.Count; i++)
            {
                var member = part.Members[i];
                if (i == 0)
                    member = member.WithLeadingTrivia(header.Concat(member.GetLeadingTrivia()));
                moved.Add(member.WithAdditionalAnnotations(MovedAnnotation));
            }

            if (part.Members.Count == 0 && carried.Count == 0)
                carried = header.Skip(1).ToList();
        }

        members.AddRange(moved);
        if (carried.Count > 0)
            closeTrivia = SyntaxFactory.TriviaList(carried.Concat(closeTrivia));

        var merged = target
            .WithMembers(SyntaxFactory.List(members))
            .WithCloseBraceToken(target.CloseBraceToken.WithLeadingTrivia(closeTrivia));

        merged = WithModifiers(merged, others, keepPartial: parts.SelectMany(p => p.Members).Any(m => m.Modifiers.Any(SyntaxKind.PartialKeyword)));
        merged = WithHeader(merged, target, others, models);
        merged = WithAttributes(merged, others, newLine);
        return merged;
    }

    private static bool Significant(SyntaxTriviaList trivia) =>
        trivia.Any(t => !t.IsKind(SyntaxKind.WhitespaceTrivia) && !t.IsKind(SyntaxKind.EndOfLineTrivia));

    /// <summary>
    /// The modifiers of every part: an accessibility the receiving part lacks
    /// goes first and others before <c>partial</c>, which is then dropped
    /// unless a partial member needs it.
    /// </summary>
    private static TypeDeclarationSyntax WithModifiers(
        TypeDeclarationSyntax merged,
        IReadOnlyList<TypeDeclarationSyntax> others,
        bool keepPartial)
    {
        var modifiers = merged.Modifiers.ToList();
        var hasAccessibility = modifiers.Any(IsAccessibility);

        foreach (var modifier in others.SelectMany(p => p.Modifiers))
        {
            if (modifiers.Any(m => m.IsKind(modifier.Kind())) || (IsAccessibility(modifier) && hasAccessibility))
                continue;

            var token = SyntaxFactory.Token(modifier.Kind()).WithTrailingTrivia(SyntaxFactory.Space);
            if (IsAccessibility(modifier))
            {
                var position = modifiers.TakeWhile(IsAccessibility).Count();
                if (position == 0)
                {
                    token = token.WithLeadingTrivia(modifiers[0].LeadingTrivia);
                    modifiers[0] = modifiers[0].WithLeadingTrivia();
                }
                modifiers.Insert(position, token);
            }
            else
            {
                modifiers.Insert(modifiers.FindIndex(m => m.IsKind(SyntaxKind.PartialKeyword)), token);
            }
        }

        if (!keepPartial)
        {
            var index = modifiers.FindIndex(m => m.IsKind(SyntaxKind.PartialKeyword));
            var partial = modifiers[index];
            modifiers.RemoveAt(index);
            if (index == 0)
            {
                if (modifiers.Count > 0)
                    modifiers[0] = modifiers[0].WithLeadingTrivia(partial.LeadingTrivia);
                else
                    return merged
                        .WithModifiers(default)
                        .WithKeyword(merged.Keyword.WithLeadingTrivia(partial.LeadingTrivia));
            }
        }

        return merged.WithModifiers(SyntaxFactory.TokenList(modifiers));
    }

    private static bool IsAccessibility(SyntaxToken token) =>
        token.IsKind(SyntaxKind.PublicKeyword) || token.IsKind(SyntaxKind.InternalKeyword)
        || token.IsKind(SyntaxKind.ProtectedKeyword) || token.IsKind(SyntaxKind.PrivateKeyword);

    /// <summary>
    /// Adds the base types and constraint clauses other parts state, keeping
    /// whatever ended the receiving header, usually a line break, before its
    /// opening brace.
    /// </summary>
    private static TypeDeclarationSyntax WithHeader(
        TypeDeclarationSyntax merged,
        TypeDeclarationSyntax target,
        IReadOnlyList<TypeDeclarationSyntax> others,
        IReadOnlyDictionary<SyntaxTree, SemanticModel> models)
    {
        var targetModel = models[target.SyntaxTree];
        var known = (target.BaseList?.Types ?? default)
            .Select(t => targetModel.GetTypeInfo(t.Type).Type)
            .ToList();

        var baseTypes = (merged.BaseList?.Types ?? default).ToList();
        var added = false;
        foreach (var part in others)
        {
            foreach (var baseType in part.BaseList?.Types ?? default)
            {
                var symbol = models[part.SyntaxTree].GetTypeInfo(baseType.Type).Type;
                if (known.Any(k => SymbolEqualityComparer.Default.Equals(k, symbol)))
                    continue;

                known.Add(symbol);
                var clean = baseType.WithoutTrivia();
                if (symbol?.TypeKind == TypeKind.Class)
                    baseTypes.Insert(0, clean);
                else
                    baseTypes.Add(clean);
                added = true;
            }
        }

        var constraints = merged.ConstraintClauses.Count == 0
            ? others.SelectMany(p => p.ConstraintClauses).Select(c => c.WithoutTrivia()).ToList()
            : new List<TypeParameterConstraintClauseSyntax>();

        if (!added && constraints.Count == 0)
            return merged;

        var headerEnd = merged.OpenBraceToken.GetPreviousToken();
        var ending = headerEnd.TrailingTrivia;
        merged = merged.ReplaceToken(headerEnd, headerEnd.WithTrailingTrivia(SyntaxFactory.Space));

        if (added)
        {
            var list = SyntaxFactory.BaseList(
                SyntaxFactory.Token(SyntaxKind.ColonToken).WithTrailingTrivia(SyntaxFactory.Space),
                SyntaxFactory.SeparatedList(
                    baseTypes.Select(t => t.WithoutTrivia()),
                    Enumerable.Repeat(SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space), baseTypes.Count - 1)));
            merged = merged.WithBaseList(list.WithTrailingTrivia(SyntaxFactory.Space));
        }

        if (constraints.Count > 0)
            merged = merged.WithConstraintClauses(SyntaxFactory.List(constraints.Select(c => c.WithTrailingTrivia(SyntaxFactory.Space))));

        var newEnd = merged.OpenBraceToken.GetPreviousToken();
        return merged.ReplaceToken(newEnd, newEnd.WithTrailingTrivia(ending));
    }

    /// <summary>Attribute lists on other parts, each on its own line after the receiving part's.</summary>
    private static TypeDeclarationSyntax WithAttributes(
        TypeDeclarationSyntax merged,
        IReadOnlyList<TypeDeclarationSyntax> others,
        SyntaxTrivia newLine)
    {
        var added = others.SelectMany(p => p.AttributeLists).ToList();
        if (added.Count == 0)
            return merged;

        var first = merged.GetFirstToken();
        var leading = first.LeadingTrivia;
        var indentation = SyntaxFactory.TriviaList(leading.Where(t => t.IsKind(SyntaxKind.WhitespaceTrivia)).TakeLast(1));
        var lists = added.Select(l => l.WithoutTrivia().WithLeadingTrivia(indentation).WithTrailingTrivia(newLine)).ToList();

        if (merged.AttributeLists.Count > 0)
            return merged.WithAttributeLists(merged.AttributeLists.AddRange(lists));

        lists[0] = lists[0].WithLeadingTrivia(leading);
        return merged
            .ReplaceToken(first, first.WithLeadingTrivia(indentation))
            .WithAttributeLists(SyntaxFactory.List(lists));
    }

    /// <summary>
    /// The using directives of a part's file that its members, base types,
    /// constraints and attributes rely on: namespaces of the types and
    /// extension methods they name, aliases they use and static imports.
    /// </summary>
    private static IEnumerable<UsingDirectiveSyntax> NeededUsings(TypeDeclarationSyntax part, SemanticModel model)
    {
        var namespaces = new HashSet<string>(StringComparer.Ordinal);
        var staticTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var aliases = new HashSet<string>(StringComparer.Ordinal);

        var nodes = part.Members.Cast<SyntaxNode>()
            .Concat(part.AttributeLists)
            .Concat(part.ConstraintClauses)
            .Concat(part.BaseList is null ? Array.Empty<SyntaxNode>() : new SyntaxNode[] { part.BaseList });

        foreach (var node in nodes.SelectMany(n => n.DescendantNodesAndSelf()))
        {
            if (node is QueryClauseSyntax or OrderingSyntax or SelectOrGroupClauseSyntax)
            {
                AddQueryMethods(node, model, namespaces);
                continue;
            }

            if (node is not SimpleNameSyntax name)
                continue;

            if (model.GetAliasInfo(name) is { } alias)
            {
                aliases.Add(alias.Name);
                continue;
            }

            var symbol = model.GetSymbolInfo(name).Symbol;
            switch (symbol)
            {
                case IMethodSymbol { ReducedFrom: { } extension }:
                    namespaces.Add(extension.ContainingNamespace.ToDisplayString());
                    break;
                case INamedTypeSymbol namedType:
                    var outer = namedType;
                    while (outer.ContainingType is not null)
                        outer = outer.ContainingType;
                    if (outer.ContainingNamespace is { } ns)
                        namespaces.Add(ns.ToDisplayString());
                    if (namedType.ContainingType is not null && !IsQualified(name))
                        staticTypes.Add(namedType.ContainingType);
                    break;
                case { IsStatic: true, ContainingType: { } containing } when !IsQualified(name):
                    staticTypes.Add(containing);
                    break;
            }
        }

        var unit = (CompilationUnitSyntax)part.SyntaxTree.GetRoot();
        foreach (var directive in unit.Usings)
        {
            if (directive.Alias is not null)
            {
                if (aliases.Contains(directive.Alias.Name.Identifier.ValueText))
                    yield return directive;
                continue;
            }

            var imported = model.GetSymbolInfo(directive.Name!).Symbol;
            var needed = directive.StaticKeyword.IsKind(SyntaxKind.None)
                ? imported is INamespaceSymbol ns && namespaces.Contains(ns.ToDisplayString())
                : imported is INamedTypeSymbol type && staticTypes.Contains(type);
            if (needed)
                yield return directive;
        }
    }

    private static bool IsQualified(SimpleNameSyntax name) =>
        name.Parent is MemberAccessExpressionSyntax access && access.Name == name
        || name.Parent is QualifiedNameSyntax qualified && qualified.Right == name
        || name.Parent is MemberBindingExpressionSyntax;

    private static void AddQueryMethods(SyntaxNode node, SemanticModel model, HashSet<string> namespaces)
    {
        var methods = node switch
        {
            QueryClauseSyntax clause => new[] { model.GetQueryClauseInfo(clause).OperationInfo.Symbol, model.GetQueryClauseInfo(clause).CastInfo.Symbol },
            OrderingSyntax or SelectOrGroupClauseSyntax => new[] { model.GetSymbolInfo(node).Symbol },
            _ => Array.Empty<ISymbol?>(),
        };

        foreach (var method in methods.OfType<IMethodSymbol>())
            namespaces.Add((method.ReducedFrom ?? method).ContainingNamespace.ToDisplayString());
    }

    /// <summary>
    /// Removes a member, giving the blank lines above it to the member that
    /// follows so the spacing between the remaining members stays as it was.
    /// </summary>
    private static CompilationUnitSyntax RemoveMember(CompilationUnitSyntax root, MemberDeclarationSyntax member)
    {
        var siblings = member.Parent switch
        {
            CompilationUnitSyntax unit => unit.Members,
            BaseNamespaceDeclarationSyntax ns => ns.Members,
            TypeDeclarationSyntax type => type.Members,
            _ => default,
        };

        var index = siblings.IndexOf(member);
        if (index >= 0 && index + 1 < siblings.Count)
        {
            var next = siblings[index + 1];
            var above = member.GetLeadingTrivia()
                .TakeWhile(t => t.IsKind(SyntaxKind.EndOfLineTrivia) || t.IsKind(SyntaxKind.WhitespaceTrivia))
                .Where(t => t.IsKind(SyntaxKind.EndOfLineTrivia));
            var own = next.GetLeadingTrivia().SkipWhile(t => t.IsKind(SyntaxKind.EndOfLineTrivia));

            root = root.TrackNodes(member, next);
            next = root.GetCurrentNode(next)!;
            root = root.ReplaceNode(next, next.WithLeadingTrivia(above.Concat(own)));
            member = root.GetCurrentNode(member)!;
        }

        return root.RemoveNode(member, SyntaxRemoveOptions.KeepNoTrivia)!;
    }

    /// <summary>A file with no type, delegate, statement or assembly attribute left.</summary>
    private static bool DeclaresNothing(CompilationUnitSyntax root) =>
        root.AttributeLists.Count == 0
        && !root.DescendantNodes().Any(n => n is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax or GlobalStatementSyntax);

    private static CompilationUnitSyntax RemoveEmptyNamespaces(CompilationUnitSyntax root)
    {
        var empty = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault(n => n.Members.Count == 0);
        return empty is null ? root : RemoveEmptyNamespaces(RemoveMember(root, empty));
    }

    private static async Task EnsureCompilesAsync(Solution solution, Solution changed, CancellationToken cancellationToken)
    {
        var errors = await SolutionEdits.NewDiagnosticsAsync(
            solution,
            changed,
            d => d.Severity == DiagnosticSeverity.Error,
            cancellationToken);
        if (errors.Count == 0)
            return;

        var conflict = errors.FirstOrDefault(e => ImportConflicts.Contains(e.Id));
        if (conflict is not null)
            throw new McpException(
                $"Error: The parts cannot be merged because their files import conflicting names: {SolutionEdits.Describe(conflict)}");

        throw new McpException($"Error: The change would not compile: {SolutionEdits.Describe(errors[0])}");
    }
}
