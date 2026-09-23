using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using ModelContextProtocol;

namespace RefactorMCP.ConsoleApp.Tools.Moving;

/// <summary>
/// What the tools that move members and types share: finding the declaration
/// a call names, tidying the documents they edit, and writing a changed
/// solution back to disk and to the session.
/// </summary>
internal static class MovingSupport
{
    /// <summary>
    /// The member or type named <paramref name="name"/> declared in
    /// <paramref name="document"/>. <paramref name="line"/>, 1-based, picks
    /// between overloads and other declarations sharing the name.
    /// </summary>
    public static async Task<ISymbol> FindDeclaredSymbolAsync(
        Document document,
        string name,
        int? line,
        Func<ISymbol, bool> accept,
        string description,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken)
            ?? throw new McpException($"Error: {document.FilePath} has no syntax tree");
        var model = await document.GetSemanticModelAsync(cancellationToken)
            ?? throw new McpException($"Error: {document.FilePath} has no semantic model");

        var candidates = root.DescendantNodes()
            .Where(node => node is MemberDeclarationSyntax or VariableDeclaratorSyntax)
            .Select(node => (Node: node, Symbol: model.GetDeclaredSymbol(node, cancellationToken)))
            .Where(c => c.Symbol is not null && c.Symbol.Name == name && accept(c.Symbol))
            .ToList();

        if (line is not null)
        {
            candidates = candidates
                .Where(c => c.Symbol!.Locations.Any(l =>
                    l.SourceTree == root.SyntaxTree && l.GetLineSpan().StartLinePosition.Line + 1 == line))
                .ToList();
        }

        var symbols = candidates.Select(c => c.Symbol!).Distinct(SymbolEqualityComparer.Default).ToList();
        return symbols.Count switch
        {
            0 => throw new McpException($"Error: No {description} named '{name}' found in {document.FilePath}"),
            1 => symbols[0],
            _ => throw new McpException($"Error: Several declarations named '{name}' found; pass the line of the one to use"),
        };
    }

    public static Document DocumentOrThrow(Solution solution, string filePath) =>
        RefactoringHelpers.GetDocumentByPath(solution, filePath)
        ?? throw new McpException($"Error: File {filePath} not found in the solution");

    /// <summary>
    /// Adds the usings that annotated names need, reduces names annotated for
    /// simplification, and formats annotated nodes.
    /// </summary>
    public static async Task<Document> TidyAsync(Document document, CancellationToken cancellationToken)
    {
        document = await ImportAdder.AddImportsAsync(document, Simplifier.AddImportsAnnotation, cancellationToken: cancellationToken);
        document = await Simplifier.ReduceAsync(document, Simplifier.Annotation, cancellationToken: cancellationToken);
        document = await Formatter.FormatAsync(document, Formatter.Annotation, cancellationToken: cancellationToken);
        return document;
    }

    /// <summary>Tidies every document that differs between the two solutions.</summary>
    public static async Task<Solution> TidyChangedDocumentsAsync(Solution original, Solution updated, CancellationToken cancellationToken)
    {
        foreach (var id in ChangedOrAddedDocuments(original, updated))
        {
            var tidied = await TidyAsync(updated.GetDocument(id)!, cancellationToken);
            updated = tidied.Project.Solution;
        }

        return updated;
    }

    /// <summary>
    /// Writes the documents <paramref name="updated"/> changed, added or
    /// removed relative to <paramref name="original"/>, then makes it the
    /// session's solution so later calls see the edit.
    /// </summary>
    public static async Task ApplyAsync(Solution original, Solution updated, CancellationToken cancellationToken)
    {
        var changes = updated.GetChanges(original);

        foreach (var project in changes.GetProjectChanges())
        {
            foreach (var id in project.GetRemovedDocuments())
            {
                var path = original.GetDocument(id)!.FilePath!;
                if (updated.Projects.SelectMany(p => p.Documents).All(d => d.FilePath != path) && File.Exists(path))
                    File.Delete(path);
                RefactoringHelpers.EvictFileCaches(path);
            }
        }

        foreach (var id in ChangedOrAddedDocuments(original, updated))
        {
            var document = updated.GetDocument(id)!;
            var path = document.FilePath!;
            var text = await document.GetTextAsync(cancellationToken);
            var encoding = File.Exists(path)
                ? await RefactoringHelpers.GetFileEncodingAsync(path, cancellationToken)
                : text.Encoding ?? new System.Text.UTF8Encoding(false);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, text.ToString(), encoding, cancellationToken);
            RefactoringHelpers.EvictFileCaches(path);
        }

        if (!string.IsNullOrEmpty(updated.FilePath))
            SessionRegistry.GetOrCreate(updated.FilePath).Replace(updated);
    }

    private static IEnumerable<DocumentId> ChangedOrAddedDocuments(Solution original, Solution updated)
    {
        foreach (var project in updated.GetChanges(original).GetProjectChanges())
        {
            foreach (var id in project.GetChangedDocuments(onlyGetDocumentsWithTextChanges: true))
                yield return id;
            foreach (var id in project.GetAddedDocuments())
                yield return id;
        }

        foreach (var project in updated.GetChanges(original).GetAddedProjects())
        {
            foreach (var document in project.Documents)
                yield return document.Id;
        }
    }

    /// <summary>
    /// A document at <paramref name="filePath"/> in <paramref name="project"/>,
    /// with the folders its path implies relative to the project directory.
    /// </summary>
    public static Document AddDocument(Project project, string filePath, SyntaxNode root)
    {
        var projectDirectory = Path.GetDirectoryName(project.FilePath!)!;
        var folders = Path.GetRelativePath(projectDirectory, Path.GetDirectoryName(filePath)!)
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
            .Where(part => part != ".")
            .ToList();
        return project.AddDocument(Path.GetFileName(filePath), root, folders, filePath);
    }

    /// <summary>The name a type is written with in C#: <c>Order</c>, not <c>Order`1</c>.</summary>
    public static string TypeFileName(INamedTypeSymbol type) => $"{type.Name}.cs";

    /// <summary>A type name as a fully qualified expression the simplifier reduces and imports.</summary>
    public static TypeSyntax QualifiedType(ITypeSymbol type) =>
        ((TypeSyntax)SyntaxGenerator.GetGenerator(new AdhocWorkspace(), LanguageNames.CSharp).TypeExpression(type, addImport: true))
            .WithAdditionalAnnotations(Simplifier.Annotation);

    /// <summary>
    /// Raises a private member to internal, so code that moved out of its type
    /// can still reach it. Returns the declaration unchanged otherwise.
    /// </summary>
    public static SyntaxTokenList RaisePrivateToInternal(SyntaxTokenList modifiers)
    {
        var isPrivate = modifiers.Any(SyntaxKind.PrivateKeyword);
        var hasAccessibility = modifiers.Any(m => m.Kind() is SyntaxKind.PublicKeyword or SyntaxKind.InternalKeyword
            or SyntaxKind.ProtectedKeyword or SyntaxKind.PrivateKeyword);

        if (isPrivate)
        {
            var index = modifiers.IndexOf(SyntaxKind.PrivateKeyword);
            var old = modifiers[index];
            return modifiers.Replace(old, SyntaxFactory.Token(old.LeadingTrivia, SyntaxKind.InternalKeyword, old.TrailingTrivia));
        }

        if (!hasAccessibility)
            return modifiers.Insert(0, SyntaxFactory.Token(SyntaxKind.InternalKeyword).WithTrailingTrivia(SyntaxFactory.Space));

        return modifiers;
    }

    /// <summary>A camel-case name for a parameter or local holding a <paramref name="typeName"/>.</summary>
    public static string CamelCase(string typeName)
    {
        var name = typeName.TrimStart('_');
        if (name.Length == 0)
            return "value";
        var camel = char.ToLowerInvariant(name[0]) + name[1..];
        return SyntaxFacts.GetKeywordKind(camel) != SyntaxKind.None ? "@" + camel : camel;
    }
}
