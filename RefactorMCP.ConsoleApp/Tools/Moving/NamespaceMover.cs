using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Simplification;
using ModelContextProtocol;

namespace RefactorMCP.ConsoleApp.Tools.Moving;

/// <summary>
/// Changes the namespace of top-level types and keeps every reference to them
/// compiling: qualified names are rewritten, files that used a type by its
/// simple name import the new namespace, and the moved types import what
/// they used to find through their old namespace.
/// </summary>
internal static class NamespaceMover
{
    /// <summary>Marks the moved declarations in the declaring file for <c>restructure</c>.</summary>
    public static readonly SyntaxAnnotation MovedDeclaration = new(nameof(MovedDeclaration));

    /// <summary>
    /// Moves <paramref name="types"/>, all declared in <paramref name="declaringDocument"/>,
    /// to <paramref name="newNamespace"/>. <paramref name="restructure"/> changes
    /// the namespace declarations of the declaring file, whose moved type
    /// declarations carry <see cref="MovedDeclaration"/>.
    /// </summary>
    public static async Task<Solution> MoveAsync(
        Solution solution,
        Document declaringDocument,
        IReadOnlyList<INamedTypeSymbol> types,
        string newNamespace,
        Func<CompilationUnitSyntax, CompilationUnitSyntax> restructure,
        CancellationToken cancellationToken)
    {
        var edits = new Dictionary<DocumentId, Dictionary<SyntaxNode, SyntaxNode>>();
        var movedSpans = types
            .SelectMany(t => t.DeclaringSyntaxReferences)
            .Select(r => (r.SyntaxTree, r.Span))
            .ToList();

        foreach (var type in types)
            await RewriteReferencesAsync(solution, type, newNamespace, movedSpans, edits, cancellationToken);

        var importedNamespaces = await ImportImplicitNamespacesAsync(declaringDocument, types, newNamespace, edits, cancellationToken);

        var unnecessaryBefore = new Dictionary<DocumentId, HashSet<string>>();
        var updated = solution;
        foreach (var id in edits.Keys.Append(declaringDocument.Id).Distinct())
        {
            var document = solution.GetDocument(id)!;
            unnecessaryBefore[id] = (await MemberLayout.UnnecessaryUsingsAsync(document, cancellationToken))
                .Select(u => u.ToString())
                .ToHashSet(StringComparer.Ordinal);

            var root = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync(cancellationToken))!;
            var replacements = edits.GetValueOrDefault(id) ?? new Dictionary<SyntaxNode, SyntaxNode>();
            var declarations = id == declaringDocument.Id
                ? types.SelectMany(t => t.DeclaringSyntaxReferences)
                    .Where(r => r.SyntaxTree == root.SyntaxTree)
                    .Select(r => r.GetSyntax(cancellationToken))
                    .ToList()
                : new List<SyntaxNode>();

            root = root.ReplaceNodes(
                replacements.Keys.Concat(declarations),
                (original, rewritten) => replacements.TryGetValue(original, out var replacement)
                    ? replacement
                    : rewritten.WithAdditionalAnnotations(MovedDeclaration));

            if (id == declaringDocument.Id)
            {
                root = restructure(root);
                root = AddUsings(root, importedNamespaces);
            }

            updated = updated.WithDocumentSyntaxRoot(id, root);
        }

        updated = await MovingSupport.TidyChangedDocumentsAsync(solution, updated, cancellationToken);

        foreach (var (id, before) in unnecessaryBefore)
        {
            var document = updated.GetDocument(id)!;
            var newlyUnnecessary = (await MemberLayout.UnnecessaryUsingsAsync(document, cancellationToken))
                .Where(u => !before.Contains(u.ToString()))
                .ToList();
            var root = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync(cancellationToken))!;
            updated = updated.WithDocumentSyntaxRoot(id, MemberLayout.RemoveUsings(root, newlyUnnecessary));
        }

        return updated;
    }

    /// <summary>A dotted name whose every part is an identifier.</summary>
    public static bool IsValidNamespace(string name) =>
        name.Split('.').All(part => SyntaxFacts.IsValidIdentifier(part) && SyntaxFacts.GetKeywordKind(part) == SyntaxKind.None);

    /// <summary>Whether the new namespace already has a type of the same name and arity.</summary>
    public static async Task<bool> NameTakenAsync(Solution solution, INamedTypeSymbol type, string newNamespace, CancellationToken cancellationToken)
    {
        foreach (var project in solution.Projects)
        {
            var compilation = await project.GetCompilationAsync(cancellationToken);
            var existing = compilation?.GetTypeByMetadataName($"{newNamespace}.{type.MetadataName}");
            if (existing is not null && !SymbolEqualityComparer.Default.Equals(existing, type))
                return true;
        }

        return false;
    }

    private static async Task RewriteReferencesAsync(
        Solution solution,
        INamedTypeSymbol type,
        string newNamespace,
        IReadOnlyList<(SyntaxTree Tree, Microsoft.CodeAnalysis.Text.TextSpan Span)> movedSpans,
        Dictionary<DocumentId, Dictionary<SyntaxNode, SyntaxNode>> edits,
        CancellationToken cancellationToken)
    {
        var references = await SymbolFinder.FindReferencesAsync(type, solution, cancellationToken);
        foreach (var location in references.SelectMany(r => r.Locations))
        {
            if (location.IsImplicit || !location.Location.IsInSource)
                continue;

            var document = location.Document;
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var model = await document.GetSemanticModelAsync(cancellationToken);
            if (root is null || model is null)
                continue;

            var span = location.Location.SourceSpan;
            if (root.FindNode(span, getInnermostNodeForTie: true) is not SimpleNameSyntax name || !NamesType(name, type))
                continue;

            var replacements = edits.TryGetValue(document.Id, out var existing) ? existing : edits[document.Id] = new();
            var qualifier = Qualifier(name);
            if (qualifier is not null)
            {
                if (model.GetSymbolInfo(qualifier, cancellationToken).Symbol is INamespaceSymbol)
                    replacements[qualifier] = NamespaceExpression(qualifier, newNamespace);
                continue;
            }

            var insideMovedType = movedSpans.Any(m => m.Tree == root.SyntaxTree && m.Span.Contains(span));
            if (insideMovedType || IsWithinNamespace(model, span.Start, newNamespace))
                continue;

            replacements[name] = Qualified(name, newNamespace);
        }
    }

    /// <summary>
    /// Qualifies the names in the moved types that bind to types found through
    /// the old namespace or its parents, when the new namespace does not reach
    /// them, so the file imports them. Extension methods found the same way
    /// are returned, as namespaces to import.
    /// </summary>
    private static async Task<IReadOnlyList<string>> ImportImplicitNamespacesAsync(
        Document document,
        IReadOnlyList<INamedTypeSymbol> types,
        string newNamespace,
        Dictionary<DocumentId, Dictionary<SyntaxNode, SyntaxNode>> edits,
        CancellationToken cancellationToken)
    {
        var model = (await document.GetSemanticModelAsync(cancellationToken))!;
        var replacements = edits.TryGetValue(document.Id, out var existing) ? existing : edits[document.Id] = new();
        var namespaces = new HashSet<string>(StringComparer.Ordinal);

        foreach (var type in types)
        {
            var oldNamespace = type.ContainingNamespace;
            foreach (var declaration in type.DeclaringSyntaxReferences.Select(r => r.GetSyntax(cancellationToken)))
            {
                foreach (var name in declaration.DescendantNodes().OfType<SimpleNameSyntax>())
                {
                    if (Qualifier(name) is not null || replacements.ContainsKey(name))
                        continue;

                    var symbol = model.GetSymbolInfo(name, cancellationToken).Symbol;
                    if (symbol is INamedTypeSymbol { ContainingType: null } used
                        && !types.Contains(used, SymbolEqualityComparer.Default)
                        && NeedsImport(used.ContainingNamespace, oldNamespace, newNamespace))
                    {
                        replacements[name] = Qualified(name, used.ContainingNamespace.ToDisplayString());
                    }
                    else if (symbol is IMethodSymbol { ReducedFrom: not null } extension
                        && NeedsImport(extension.ContainingType.ContainingNamespace, oldNamespace, newNamespace))
                    {
                        namespaces.Add(extension.ContainingType.ContainingNamespace.ToDisplayString());
                    }
                }
            }
        }

        return namespaces.ToList();
    }

    /// <summary>
    /// Code in <paramref name="oldNamespace"/> sees the types of that namespace
    /// and its parents without a using; code in the new namespace sees those
    /// of the new one and its parents.
    /// </summary>
    private static bool NeedsImport(INamespaceSymbol used, INamespaceSymbol oldNamespace, string newNamespace)
    {
        if (used.IsGlobalNamespace)
            return false;

        var usedName = used.ToDisplayString();
        var implicitBefore = Enclosing(oldNamespace).Any(n => n.ToDisplayString() == usedName);
        var implicitAfter = newNamespace == usedName || newNamespace.StartsWith(usedName + ".", StringComparison.Ordinal);
        return implicitBefore && !implicitAfter;
    }

    private static IEnumerable<INamespaceSymbol> Enclosing(INamespaceSymbol ns)
    {
        for (var current = ns; current is { IsGlobalNamespace: false }; current = current.ContainingNamespace)
            yield return current;
    }

    private static bool NamesType(SimpleNameSyntax name, INamedTypeSymbol type)
    {
        var text = name.Identifier.ValueText;
        return text == type.Name || text + "Attribute" == type.Name;
    }

    /// <summary>What a name is qualified by, when it is the right-hand side of a dotted name.</summary>
    private static SyntaxNode? Qualifier(SimpleNameSyntax name) => name.Parent switch
    {
        QualifiedNameSyntax q when q.Right == name => q.Left,
        MemberAccessExpressionSyntax m when m.Name == name => m.Expression,
        AliasQualifiedNameSyntax a when a.Name == name => a.Alias,
        _ => null,
    };

    private static bool IsWithinNamespace(SemanticModel model, int position, string ns)
    {
        for (var symbol = model.GetEnclosingSymbol(position); symbol is not null; symbol = symbol.ContainingSymbol)
        {
            if (symbol is INamespaceSymbol { IsGlobalNamespace: false } enclosing
                && Enclosing(enclosing).Any(n => n.ToDisplayString() == ns))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A namespace name in place of <paramref name="qualifier"/>, keeping a <c>global::</c> prefix.</summary>
    private static SyntaxNode NamespaceExpression(SyntaxNode qualifier, string newNamespace)
    {
        var global = qualifier.DescendantNodesAndSelf().OfType<AliasQualifiedNameSyntax>().Any(a => a.Alias.Identifier.IsKind(SyntaxKind.GlobalKeyword));
        var text = global ? $"global::{newNamespace}" : newNamespace;
        SyntaxNode replacement = qualifier is ExpressionSyntax and not NameSyntax
            ? SyntaxFactory.ParseExpression(text)
            : SyntaxFactory.ParseName(text);
        return replacement.WithTriviaFrom(qualifier);
    }

    /// <summary>
    /// <paramref name="name"/> qualified by <c>global::</c> and a namespace,
    /// marked so the simplifier reduces it and the file imports the namespace.
    /// </summary>
    private static SyntaxNode Qualified(SimpleNameSyntax name, string ns)
    {
        var text = $"global::{ns}.{name.WithoutTrivia()}";
        SyntaxNode qualified = SyntaxFacts.IsInTypeOnlyContext(name)
            ? SyntaxFactory.ParseTypeName(text)
            : SyntaxFactory.ParseExpression(text);
        return qualified
            .WithTriviaFrom(name)
            .WithAdditionalAnnotations(Simplifier.Annotation, Simplifier.AddImportsAnnotation);
    }

    /// <summary>Adds using directives the file lacks, after its existing ones.</summary>
    private static CompilationUnitSyntax AddUsings(CompilationUnitSyntax root, IEnumerable<string> namespaces)
    {
        foreach (var ns in namespaces)
        {
            if (root.Usings.Any(u => u.Alias is null && u.StaticKeyword.IsKind(SyntaxKind.None) && u.Name?.ToString() == ns))
                continue;

            root = root.AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(ns))
                .WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation));
        }

        return root;
    }
}
