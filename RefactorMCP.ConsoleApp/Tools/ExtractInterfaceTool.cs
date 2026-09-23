using ModelContextProtocol.Server;
using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.ComponentModel;
using System.Threading;

[McpServerToolType]
public static class ExtractInterfaceTool
{
    [McpServerTool, Description("Extract an interface from a class's public members into a new file, and make the class implement it")]
    public static async Task<string> ExtractInterface(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file containing the class")] string filePath,
        [Description("Name of the class to extract from")] string className,
        [Description("Comma separated list of member names to include; empty for every public instance member")] string memberList,
        [Description("Path to write the generated interface file")] string interfaceFilePath,
        [Description("Name of the interface (optional; defaults to the file name)")] string? interfaceName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (solution, document) = await TypeRefactoringHelpers.LoadDocumentAsync(solutionPath, filePath, cancellationToken);
            var (type, declaration) = await TypeRefactoringHelpers.FindTypeAsync(document, className, cancellationToken);
            var model = (await document.GetSemanticModelAsync(cancellationToken))!;

            interfaceFilePath = RefactoringHelpers.ResolvePath(interfaceFilePath)!;
            interfaceName ??= Path.GetFileNameWithoutExtension(interfaceFilePath);
            if (!SyntaxFacts.IsValidIdentifier(interfaceName))
                throw new McpException($"Error: '{interfaceName}' is not a valid interface name");

            var ns = type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString();
            var metadataName = (ns.Length > 0 ? ns + "." : "") + interfaceName
                + (type.Arity > 0 ? $"`{type.Arity}" : "");
            if (model.Compilation.GetTypeByMetadataName(metadataName) is not null)
                throw new McpException($"Error: A type named {(ns.Length > 0 ? ns + "." : "")}{interfaceName} already exists");

            var members = ChooseMembers(declaration, model, memberList);
            var text = InterfaceText(declaration, model, members, interfaceName, ns);

            var interfaceType = SyntaxFactory.ParseTypeName(interfaceName + (declaration.TypeParameterList?.WithoutTrivia().ToString() ?? ""));
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var updated = document.WithSyntaxRoot(root!.ReplaceNode(declaration, TypeRefactoringHelpers.AddBaseType(declaration, interfaceType, first: false)));
            var withInterface = updated.Project
                .AddDocument(Path.GetFileName(interfaceFilePath), TypeRefactoringHelpers.Text(text), filePath: interfaceFilePath)
                .Project.Solution;

            await TypeRefactoringHelpers.ApplyIfCompilesAsync(
                solution,
                withInterface,
                errors => $"Error: Extracting {interfaceName} would break the build: {TypeRefactoringHelpers.Describe(errors)}",
                cancellationToken);

            return $"Successfully extracted interface '{interfaceName}' to {interfaceFilePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error extracting interface: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// The members named in <paramref name="memberList"/>, every overload of a
    /// named method included, or every public instance member when it is empty.
    /// </summary>
    private static IReadOnlyList<MemberDeclarationSyntax> ChooseMembers(TypeDeclarationSyntax declaration, SemanticModel model, string memberList)
    {
        var names = memberList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (names.Length == 0)
        {
            var all = declaration.Members.Where(m => MemberName(m) is not null && Eligible(m, model)).ToList();
            return all.Count > 0
                ? all
                : throw new McpException($"Error: No matching members found; {declaration.Identifier.ValueText} has no public instance members");
        }

        var chosen = new List<MemberDeclarationSyntax>();
        foreach (var name in names)
        {
            var matching = declaration.Members.Where(m => MemberName(m) == name).ToList();
            if (matching.Count == 0)
                throw new McpException($"Error: No member named {name} in {declaration.Identifier.ValueText}");

            var ineligible = matching.FirstOrDefault(m => !Eligible(m, model));
            if (ineligible is not null)
                throw new McpException($"Error: {name} is not a public instance member, so {declaration.Identifier.ValueText} cannot implement it through an interface");

            chosen.AddRange(matching);
        }

        return declaration.Members.Where(chosen.Contains).ToList();
    }

    private static string? MemberName(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax method => method.Identifier.ValueText,
        PropertyDeclarationSyntax property => property.Identifier.ValueText,
        IndexerDeclarationSyntax => "this",
        EventDeclarationSyntax @event => @event.Identifier.ValueText,
        EventFieldDeclarationSyntax events => events.Declaration.Variables.Count == 1 ? events.Declaration.Variables[0].Identifier.ValueText : null,
        _ => null,
    };

    private static bool Eligible(MemberDeclarationSyntax member, SemanticModel model)
    {
        var symbol = member is EventFieldDeclarationSyntax events
            ? model.GetDeclaredSymbol(events.Declaration.Variables[0])
            : model.GetDeclaredSymbol(member);
        return symbol is { DeclaredAccessibility: Accessibility.Public, IsStatic: false }
            && symbol is not IMethodSymbol { MethodKind: not MethodKind.Ordinary };
    }

    /// <summary>
    /// The interface file: the class's namespace in the style its file uses,
    /// the usings the signatures need, and a declaration per member with its
    /// documentation comment.
    /// </summary>
    private static string InterfaceText(
        TypeDeclarationSyntax declaration,
        SemanticModel model,
        IReadOnlyList<MemberDeclarationSyntax> members,
        string interfaceName,
        string ns)
    {
        var eol = TypeRefactoringHelpers.EndOfLine(declaration.SyntaxTree.GetRoot()).ToFullString();
        var fileScoped = declaration.Ancestors().OfType<FileScopedNamespaceDeclarationSyntax>().Any();
        var indent = ns.Length > 0 && !fileScoped ? "    " : "";

        var usings = members
            .SelectMany(m => SignatureParts(m).SelectMany(part => TypeRefactoringHelpers.NamespacesUsedBy(part, model)))
            .Concat(declaration.ConstraintClauses.SelectMany(c => TypeRefactoringHelpers.NamespacesUsedBy(c, model)))
            .Where(u => u != ns && !ns.StartsWith(u + ".", StringComparison.Ordinal))
            .Distinct()
            .OrderBy(u => u == "System" || u.StartsWith("System.", StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(u => u, StringComparer.Ordinal)
            .ToList();

        var lines = new List<string>();
        lines.AddRange(usings.Select(u => $"using {u};"));
        if (usings.Count > 0)
            lines.Add("");

        if (ns.Length > 0)
            lines.AddRange(fileScoped ? new[] { $"namespace {ns};", "" } : new[] { $"namespace {ns}", "{" });

        var constraints = declaration.ConstraintClauses.Count == 0
            ? ""
            : " " + string.Join(" ", declaration.ConstraintClauses.Select(c => c.NormalizeWhitespace().ToString()));
        lines.Add($"{indent}public interface {interfaceName}{declaration.TypeParameterList?.NormalizeWhitespace()}{constraints}");
        lines.Add($"{indent}{{");
        foreach (var member in members)
        {
            lines.AddRange(DocumentationLines(member).Select(l => $"{indent}    {l}"));
            lines.Add($"{indent}    {Signature(member)}");
        }

        lines.Add($"{indent}}}");
        if (ns.Length > 0 && !fileScoped)
            lines.Add("}");

        return string.Join(eol, lines) + eol;
    }

    private static IEnumerable<SyntaxNode> SignatureParts(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax method => new SyntaxNode?[] { method.ReturnType, method.TypeParameterList, method.ParameterList }
            .Concat(method.ConstraintClauses).OfType<SyntaxNode>(),
        PropertyDeclarationSyntax property => new[] { property.Type },
        IndexerDeclarationSyntax indexer => new SyntaxNode[] { indexer.Type, indexer.ParameterList },
        EventDeclarationSyntax @event => new[] { @event.Type },
        EventFieldDeclarationSyntax events => new[] { events.Declaration.Type },
        _ => Array.Empty<SyntaxNode>(),
    };

    /// <summary>How the interface declares a member: its signature, without modifiers or body.</summary>
    private static string Signature(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax method =>
            $"{Normal(method.ReturnType)} {method.Identifier.ValueText}{Normal(method.TypeParameterList)}{Normal(method.ParameterList)}"
            + string.Concat(method.ConstraintClauses.Select(c => " " + Normal(c))) + ";",
        PropertyDeclarationSyntax property => $"{Normal(property.Type)} {property.Identifier.ValueText} {Accessors(property.AccessorList)}",
        IndexerDeclarationSyntax indexer => $"{Normal(indexer.Type)} this{Normal(indexer.ParameterList)} {Accessors(indexer.AccessorList)}",
        EventDeclarationSyntax @event => $"event {Normal(@event.Type)} {@event.Identifier.ValueText};",
        EventFieldDeclarationSyntax events => $"event {Normal(events.Declaration.Type)} {events.Declaration.Variables[0].Identifier.ValueText};",
        _ => throw new InvalidOperationException($"Cannot declare a {member.Kind()} in an interface"),
    };

    /// <summary>The accessors callers can use: a private setter is the class's own business.</summary>
    private static string Accessors(AccessorListSyntax? accessors)
    {
        var visible = accessors is null
            ? new[] { "get" }
            : accessors.Accessors.Where(a => a.Modifiers.Count == 0).Select(a => a.Keyword.ValueText).ToArray();
        return $"{{ {string.Join(" ", visible.Select(a => a + ";"))} }}";
    }

    private static string Normal(SyntaxNode? node) => node?.WithoutTrivia().NormalizeWhitespace().ToString() ?? "";

    /// <summary>A member's documentation comment, one line per entry, without its indentation.</summary>
    private static IEnumerable<string> DocumentationLines(MemberDeclarationSyntax member) =>
        member.GetLeadingTrivia()
            .Where(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            .SelectMany(t => t.ToFullString().Replace("\r\n", "\n").TrimEnd('\n').Split('\n'))
            .Select(l => l.TrimStart());
}
