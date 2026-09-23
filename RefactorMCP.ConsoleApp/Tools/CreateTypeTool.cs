using ModelContextProtocol.Server;
using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.ComponentModel;

[McpServerToolType]
public static class CreateTypeTool
{
    private static readonly string[] Kinds = { "class", "interface", "record", "struct" };

    [McpServerTool, Description("Create an empty class, interface, record or struct in a new file or at the end of an existing one")]
    public static async Task<string> CreateType(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path of the file to create, or of an existing file to add the type to")] string filePath,
        [Description("Name of the new type, with type parameters when generic, e.g. Page<T>")] string name,
        [Description("class, interface, record or struct (default class)")] string kind = "class",
        [Description("Namespace for the type (optional; defaults to the file's namespace, or the one the project's files in that folder use)")] string? namespaceName = null,
        [Description("Base class, or base interface of an interface (optional)")] string? baseType = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var typeName = ParseName(name);
            if (!Kinds.Contains(kind))
                throw new McpException($"Error: Unknown kind '{kind}'; use class, interface, record or struct");

            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var path = RefactoringHelpers.ResolvePath(filePath)!;
            var existing = RefactoringHelpers.GetDocumentByPath(solution, path);
            var project = existing?.Project ?? TypeRefactoringHelpers.ProjectForPath(solution, path);

            var ns = namespaceName ?? (existing is not null
                ? await FileNamespaceAsync(existing, cancellationToken)
                : await ProjectNamespaceAsync(project, path, cancellationToken));

            var compilation = await project.GetCompilationAsync(cancellationToken);
            var qualified = string.IsNullOrEmpty(ns) ? typeName.Identifier.ValueText : $"{ns}.{typeName.Identifier.ValueText}";
            var metadataName = string.IsNullOrEmpty(ns)
                ? TypeRefactoringHelpers.MetadataName(typeName)
                : $"{ns}.{TypeRefactoringHelpers.MetadataName(typeName)}";
            if (compilation!.GetTypeByMetadataName(metadataName) is not null)
                throw new McpException($"Error: A type named {qualified} already exists");

            var declaration = $"public {kind} {name}{(baseType is null ? "" : $" : {baseType}")}";
            var document = existing is null
                ? AddFile(project, path, ns, declaration, await FileScopedStyleAsync(project, cancellationToken))
                : await AddToFileAsync(existing, ns, declaration, cancellationToken);

            if (baseType is not null)
                document = await ResolveBaseTypeAsync(document, kind, baseType, cancellationToken);

            await TypeRefactoringHelpers.ApplyAsync(solution, document.Project.Solution, cancellationToken);
            return $"Successfully created {kind} {qualified} in {path}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error creating type: {ex.Message}", ex);
        }
    }

    /// <summary>A type name: an identifier, optionally with type parameter names.</summary>
    private static SimpleNameSyntax ParseName(string name)
    {
        var parsed = SyntaxFactory.ParseTypeName(name);
        var valid = !parsed.ContainsDiagnostics
            && parsed.ToString() == name.Trim()
            && parsed switch
            {
                IdentifierNameSyntax id => IsIdentifier(id.Identifier),
                GenericNameSyntax generic => IsIdentifier(generic.Identifier)
                    && generic.TypeArgumentList.Arguments.All(a => a is IdentifierNameSyntax arg && IsIdentifier(arg.Identifier)),
                _ => false,
            };

        return valid
            ? (SimpleNameSyntax)parsed
            : throw new McpException($"Error: '{name}' is not a valid type name");

        static bool IsIdentifier(SyntaxToken token) =>
            SyntaxFacts.IsValidIdentifier(token.ValueText) && SyntaxFacts.GetKeywordKind(token.ValueText) == SyntaxKind.None;
    }

    private static async Task<string> FileNamespaceAsync(Document document, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        return root!.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().LastOrDefault()?.Name.ToString() ?? "";
    }

    /// <summary>
    /// The namespace most files in the new file's folder declare, or failing
    /// that most files in the project; none when they declare none.
    /// </summary>
    private static async Task<string> ProjectNamespaceAsync(Project project, string path, CancellationToken cancellationToken)
    {
        var documents = TypeRefactoringHelpers.SourceDocuments(project).ToList();
        var folder = Path.GetDirectoryName(Path.GetFullPath(path));
        var inFolder = documents.Where(d => Path.GetDirectoryName(Path.GetFullPath(d.FilePath!)) == folder).ToList();

        var namespaces = new List<string>();
        foreach (var document in inFolder.Count > 0 ? inFolder : documents)
            namespaces.Add(await FileNamespaceAsync(document, cancellationToken));

        return namespaces
            .GroupBy(n => n)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault() ?? "";
    }

    /// <summary>Whether most of the project's files use file-scoped namespaces.</summary>
    private static async Task<bool> FileScopedStyleAsync(Project project, CancellationToken cancellationToken)
    {
        int fileScoped = 0, block = 0;
        foreach (var document in TypeRefactoringHelpers.SourceDocuments(project))
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            fileScoped += root!.ChildNodes().OfType<FileScopedNamespaceDeclarationSyntax>().Count();
            block += root.ChildNodes().OfType<NamespaceDeclarationSyntax>().Count();
        }

        return fileScoped > block;
    }

    private static Document AddFile(Project project, string path, string ns, string declaration, bool fileScoped)
    {
        var eol = TypeRefactoringHelpers.EndOfLine(project);
        var lines = string.IsNullOrEmpty(ns)
            ? new[] { declaration, "{", "}" }
            : fileScoped
                ? new[] { $"namespace {ns};", "", declaration, "{", "}" }
                : new[] { $"namespace {ns}", "{", $"    {declaration}", "    {", "    }", "}" };

        var text = string.Join(eol, lines) + eol;
        return project.AddDocument(Path.GetFileName(path), TypeRefactoringHelpers.Text(text), filePath: path);
    }

    /// <summary>
    /// Adds the type after the last member of the file's namespace, or of the
    /// file when the namespace is the global one, separated by a blank line.
    /// </summary>
    private static async Task<Document> AddToFileAsync(Document document, string ns, string declaration, CancellationToken cancellationToken)
    {
        var root = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync(cancellationToken))!;
        var eol = TypeRefactoringHelpers.EndOfLine(root);
        var type = SyntaxFactory.ParseMemberDeclaration($"{declaration}{eol.ToFullString()}{{{eol.ToFullString()}}}{eol.ToFullString()}")!
            .WithLeadingTrivia(eol)
            .WithAdditionalAnnotations(Formatter.Annotation);

        var container = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().LastOrDefault(n => n.Name.ToString() == ns);
        CompilationUnitSyntax updated;
        if (container is not null)
            updated = root.ReplaceNode(container, container.AddMembers(type));
        else if (string.IsNullOrEmpty(ns))
            updated = root.AddMembers(type);
        else
            updated = root.AddMembers(SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(ns))
                .AddMembers(type.WithLeadingTrivia())
                .WithLeadingTrivia(eol)
                .WithAdditionalAnnotations(Formatter.Annotation));

        var changed = document.WithSyntaxRoot(updated);
        return await Formatter.FormatAsync(changed, Formatter.Annotation, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Checks the base type binds, importing its namespace when that is what
    /// it takes, and that the new kind of type can derive from it.
    /// </summary>
    private static async Task<Document> ResolveBaseTypeAsync(Document document, string kind, string baseType, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var baseSyntax = root!.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Last().BaseList!.Types[0].Type;
        var annotation = new SyntaxAnnotation();
        document = document.WithSyntaxRoot(root.ReplaceNode(baseSyntax, baseSyntax.WithAdditionalAnnotations(annotation)));

        (document, var resolved) = await TypeRefactoringHelpers.ResolveTypeAsync(document, annotation, baseType, cancellationToken);
        var symbol = (INamedTypeSymbol)resolved;

        var reason = (kind, symbol) switch
        {
            (_, { TypeKind: TypeKind.Interface }) => null,
            ("interface", _) => "an interface can only extend interfaces",
            ("struct", _) => "a struct can only implement interfaces",
            ("record", { IsRecord: false }) => "a record can only derive from a record",
            ("class", { IsRecord: true }) => "a class cannot derive from a record",
            (_, { TypeKind: not TypeKind.Class }) => $"it is a {symbol.TypeKind.ToString().ToLowerInvariant()}",
            (_, { IsSealed: true }) => "it is sealed",
            (_, { IsStatic: true }) => "it is static",
            _ => null,
        };

        return reason is null
            ? document
            : throw new McpException($"Error: {symbol.ToDisplayString()} cannot be a base type of a {kind}: {reason}");
    }
}
