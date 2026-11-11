using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelContextProtocol;

namespace RefactorMCP.ConsoleApp.Tools.Moving;

/// <summary>Semantic selection, project dependencies and declaration extraction for cross-project type moves.</summary>
internal static class TypeMovePlanner
{
    internal sealed record Part(DocumentId DocumentId, string FilePath, int OriginalSpanStart);

    internal static List<Part> Parts(Solution solution, INamedTypeSymbol type, ProjectId sourceProject)
    {
        var parts = new List<Part>();
        foreach (var syntax in type.DeclaringSyntaxReferences)
        {
            var doc = solution.GetDocument(syntax.SyntaxTree)
                ?? throw new McpException($"The declaration of {type.Name} is generated or absent from the loaded solution");
            if (doc.Project.Id != sourceProject || doc.FilePath is null || !File.Exists(doc.FilePath))
                throw new McpException($"The declaration of {type.Name} is outside its source project or not on disk: {doc.FilePath}");
            var node = syntax.GetSyntax();
            if (node is not BaseTypeDeclarationSyntax and not DelegateDeclarationSyntax ||
                node.Parent is not (CompilationUnitSyntax or BaseNamespaceDeclarationSyntax))
                throw new McpException($"Unsupported declaration for {type.Name} in {doc.FilePath}: {node.Kind()}");
            if (node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Count() > 1)
                throw new McpException($"Nested namespace blocks containing {type.Name} must be flattened first: {doc.FilePath}");
            parts.Add(new Part(doc.Id, doc.FilePath, syntax.Span.Start));
        }
        if (parts.Count == 0)
            throw new McpException($"No source declarations found for {type.Name}");
        if (parts.Count > 1 && parts.Any(p =>
            type.DeclaringSyntaxReferences.First(r => r.Span.Start == p.OriginalSpanStart &&
                solution.GetDocument(r.SyntaxTree)?.Id == p.DocumentId).GetSyntax() is TypeDeclarationSyntax syntax &&
                !syntax.Modifiers.Any(SyntaxKind.PartialKeyword)))
            throw new McpException($"Every declaration of multi-part type {type.Name} must be partial");
        return parts;
    }

    internal static IReadOnlyList<string> TargetPaths(string firstPath, int count)
    {
        var extension = Path.GetExtension(firstPath);
        var name = Path.GetFileNameWithoutExtension(firstPath);
        var folder = Path.GetDirectoryName(firstPath)!;
        return Enumerable.Range(0, count)
            .Select(i => i == 0 ? firstPath : Path.Combine(folder, $"{name}.Part{i + 1:D2}{extension}"))
            .ToList();
    }

    internal static async Task<HashSet<ProjectId>> ReferencedProjectIdsAsync(
        Solution solution, INamedTypeSymbol type, CancellationToken cancellationToken)
    {
        var result = new HashSet<ProjectId>();
        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            var doc = solution.GetDocument(reference.SyntaxTree)!;
            var model = await doc.GetSemanticModelAsync(cancellationToken)
                ?? throw new McpException($"Missing semantic model for {doc.FilePath}");
            var declaration = reference.GetSyntax(cancellationToken);
            foreach (var node in declaration.DescendantNodesAndSelf().OfType<SimpleNameSyntax>())
            {
                var symbolInfo = model.GetSymbolInfo(node, cancellationToken);
                var symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
                var declaringType = symbol switch
                {
                    INamedTypeSymbol t => t,
                    IMethodSymbol m => m.ContainingType,
                    IPropertySymbol p => p.ContainingType,
                    IFieldSymbol f => f.ContainingType,
                    IEventSymbol e => e.ContainingType,
                    _ => null
                };
                if (declaringType is null ||
                    SymbolEqualityComparer.Default.Equals(declaringType.OriginalDefinition, type.OriginalDefinition))
                    continue;
                foreach (var location in declaringType.OriginalDefinition.Locations.Where(l => l.IsInSource))
                {
                    var dependencyDoc = solution.GetDocument(location.SourceTree);
                    if (dependencyDoc is not null)
                        result.Add(dependencyDoc.Project.Id);
                }
            }
        }
        return result;
    }

    internal static async Task<bool> NeedsOldNamespaceImportAsync(
        Solution solution, INamedTypeSymbol type, string oldNamespace, CancellationToken cancellationToken)
    {
        if (oldNamespace.Length == 0)
            return false;
        foreach (var syntax in type.DeclaringSyntaxReferences)
        {
            var model = await solution.GetDocument(syntax.SyntaxTree)!.GetSemanticModelAsync(cancellationToken)
                ?? throw new McpException($"Missing semantic model for {syntax.SyntaxTree.FilePath}");
            foreach (var node in syntax.GetSyntax(cancellationToken).DescendantNodes().OfType<SimpleNameSyntax>())
            {
                var info = model.GetSymbolInfo(node, cancellationToken);
                var symbol = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
                var declaringType = symbol switch
                {
                    INamedTypeSymbol t => t,
                    IMethodSymbol m => m.ContainingType,
                    IPropertySymbol p => p.ContainingType,
                    IFieldSymbol f => f.ContainingType,
                    IEventSymbol e => e.ContainingType,
                    _ => null
                };
                if (declaringType is not null &&
                    !SymbolEqualityComparer.Default.Equals(declaringType.OriginalDefinition, type.OriginalDefinition) &&
                    declaringType.ContainingNamespace.ToDisplayString() == oldNamespace)
                    return true;
            }
        }
        return false;
    }

    internal static async Task<Solution> ExtractAsync(
        Solution changed, IReadOnlyList<Part> parts, string typeName, string originalNamespace,
        string newNamespace, ProjectId targetProjectId,
        IReadOnlyList<string> destinationPaths, bool importOldNamespace, CancellationToken cancellationToken)
    {
        var byDocument = parts.GroupBy(p => p.DocumentId).ToList();
        var destinationIndex = 0;
        foreach (var group in byDocument)
        {
            var doc = changed.GetDocument(group.Key)!;
            var root = (CompilationUnitSyntax)(await doc.GetSyntaxRootAsync(cancellationToken))!;
            var count = group.Count();
            for (var i = 0; i < count; i++)
            {
                var declaration = root.DescendantNodes()
                    .OfType<MemberDeclarationSyntax>()
                    .Where(node => IsMatch(node, typeName, originalNamespace))
                    .FirstOrDefault()
                    ?? throw new McpException($"Cannot find original {typeName} partial declaration in {doc.FilePath}");

                var isolated = TypeFileMover.OnlyType(declaration);
                if (!string.Equals(originalNamespace, newNamespace, StringComparison.Ordinal))
                {
                    isolated = ChangeNamespace(isolated, newNamespace);
                    if (originalNamespace.Length > 0 && importOldNamespace)
                        isolated = MovingSupport.AddUsings(isolated, new[] { originalNamespace });
                }

                var destinationPath = destinationPaths[destinationIndex++];
                var folder = Path.GetDirectoryName(changed.GetProject(targetProjectId)!.FilePath!)!;
                var relative = Path.GetRelativePath(folder, Path.GetDirectoryName(destinationPath)!);
                var folders = relative.Split(Path.DirectorySeparatorChar,
                    StringSplitOptions.RemoveEmptyEntries).Where(f => f != ".").ToList();
                changed = changed.AddDocument(DocumentId.CreateNewId(targetProjectId),
                    Path.GetFileName(destinationPath), isolated, folders, destinationPath);

                var enclosing = declaration.Parent!;
                root = enclosing switch
                {
                    CompilationUnitSyntax unit =>
                        unit.WithMembers(MemberLayout.Remove(unit.Members, declaration)),
                    BaseNamespaceDeclarationSyntax ns =>
                        root.ReplaceNode(ns, ns.WithMembers(MemberLayout.Remove(ns.Members, declaration))),
                    _ => throw new McpException($"Unexpected declaration container for {typeName}")
                };
            }
            changed = changed.WithDocumentSyntaxRoot(doc.Id, root);
        }
        return changed;
    }

    internal static string? ForbiddenContractsCoreEdge(Solution solution)
    {
        var graph = solution.GetProjectDependencyGraph();
        foreach (var project in solution.Projects.Where(p => IsSuffix(p.Name, "Contracts")))
        {
            foreach (var dependencyId in graph.GetProjectsThatThisProjectTransitivelyDependsOn(project.Id))
            {
                var dependency = solution.GetProject(dependencyId);
                if (dependency is not null && IsSuffix(dependency.Name, "Core"))
                    return $"{project.Name} -> {dependency.Name} (transitive or direct)";
            }
        }
        return null;
    }

    private static bool IsSuffix(string project, string suffix) =>
        project.Equals(suffix, StringComparison.OrdinalIgnoreCase) ||
        project.EndsWith("." + suffix, StringComparison.OrdinalIgnoreCase);

    private static bool IsMatch(MemberDeclarationSyntax node, string typeName, string ns)
    {
        if (node.Parent is not CompilationUnitSyntax and not BaseNamespaceDeclarationSyntax)
            return false;
        var name = node switch
        {
            BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
            DelegateDeclarationSyntax del => del.Identifier.ValueText,
            _ => null
        };
        if (name != typeName) return false;
        var namespaceParts = node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>()
            .Reverse().Select(n => n.Name.ToString());
        return string.Join(".", namespaceParts) == ns;
    }

    private static CompilationUnitSyntax ChangeNamespace(CompilationUnitSyntax root, string targetNamespace)
    {
        var ns = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        if (ns is not null)
        {
            if (targetNamespace.Length > 0)
                return root.ReplaceNode(ns, ns.WithName(SyntaxFactory.ParseName(targetNamespace)));
            return root.ReplaceNode(ns, ns.Members.First());
        }
        if (targetNamespace.Length == 0) return root;
        var members = root.Members;
        return root.WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(
            SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(targetNamespace)).WithMembers(members)));
    }
}
