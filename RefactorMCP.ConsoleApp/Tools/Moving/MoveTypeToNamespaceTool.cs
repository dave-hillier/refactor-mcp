using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace RefactorMCP.ConsoleApp.Tools.Moving;

[McpServerToolType]
public static class MoveTypeToNamespaceTool
{
    [McpServerTool, Description("Move a top-level type to another namespace, updating qualified references " +
        "and adding usings across the solution. The type stays in its file.")]
    public static async Task<string> MoveTypeToNamespace(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file declaring the type")] string filePath,
        [Description("Name of the type to move")] string typeName,
        [Description("The namespace to move the type to, e.g. Shop.Billing")] string targetNamespace,
        CancellationToken cancellationToken = default)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var document = MovingSupport.DocumentOrThrow(solution, filePath);
        var type = (INamedTypeSymbol)await MovingSupport.FindDeclaredSymbolAsync(
            document, typeName, line: null, s => s is INamedTypeSymbol, "type", cancellationToken);

        if (type.ContainingType is not null)
            throw new McpException($"Error: Type {typeName} is nested in {type.ContainingType.Name}; its namespace is its container's");
        if (!NamespaceMover.IsValidNamespace(targetNamespace))
            throw new McpException($"Error: '{targetNamespace}' is not a valid namespace name");
        if (type.ContainingNamespace.ToDisplayString() == targetNamespace)
            throw new McpException($"Error: {typeName} is already in namespace {targetNamespace}");
        if (type.DeclaringSyntaxReferences.Length > 1)
            throw new McpException($"Error: {typeName} is a partial type declared in several places; move each part's namespace together");
        if (await NamespaceMover.NameTakenAsync(solution, type, targetNamespace, cancellationToken))
            throw new McpException($"Error: Namespace {targetNamespace} already contains a type named {typeName}");

        var declaration = (MemberDeclarationSyntax)await type.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken);
        EnsureCanRestructure(declaration, typeName);

        var updated = await NamespaceMover.MoveAsync(
            solution,
            document,
            new[] { type },
            targetNamespace,
            root => Restructure(root, targetNamespace),
            cancellationToken);
        await MovingSupport.ApplyAsync(solution, updated, cancellationToken);

        return $"Successfully moved {typeName} to namespace {targetNamespace}";
    }

    private static void EnsureCanRestructure(MemberDeclarationSyntax declaration, string typeName)
    {
        switch (declaration.Parent)
        {
            case NamespaceDeclarationSyntax { Parent: not CompilationUnitSyntax }:
                throw new McpException($"Error: {typeName} is declared in a nested namespace block; move it out of the nested block first");
            case FileScopedNamespaceDeclarationSyntax ns when ns.Members.Count > 1:
                throw new McpException($"Error: {typeName} shares a file-scoped namespace with other types; move it to its own file first");
        }
    }

    /// <summary>
    /// Puts the moved declaration in the new namespace: its namespace is
    /// renamed when it holds nothing else, otherwise it moves to a new block
    /// after the old one.
    /// </summary>
    private static CompilationUnitSyntax Restructure(CompilationUnitSyntax root, string targetNamespace)
    {
        var declaration = root.GetAnnotatedNodes(NamespaceMover.MovedDeclaration).OfType<MemberDeclarationSyntax>().Single();
        var name = SyntaxFactory.ParseName(targetNamespace);

        switch (declaration.Parent)
        {
            case BaseNamespaceDeclarationSyntax ns when ns.Members.Count == 1:
                return root.ReplaceNode(ns, ns.WithName(name.WithTriviaFrom(ns.Name)));

            case NamespaceDeclarationSyntax ns:
                var block = ns
                    .WithName(name.WithTriviaFrom(ns.Name))
                    .WithUsings(default)
                    .WithMembers(MemberLayout.KeepOnly(ns.Members, declaration))
                    .WithLeadingTrivia(SyntaxFactory.EndOfLine("\n"));
                var remaining = ns.WithMembers(MemberLayout.Remove(ns.Members, declaration));
                return root.WithMembers(root.Members.Replace(ns, remaining).Insert(root.Members.IndexOf(ns) + 1, block));

            default:
                var wrapped = SyntaxFactory.NamespaceDeclaration(name)
                    .AddMembers(MemberLayout.WithoutLeadingBlankLines(declaration))
                    .WithLeadingTrivia(MemberLayout.LeadingBlankLines(declaration.GetLeadingTrivia()))
                    .WithAdditionalAnnotations(Formatter.Annotation);
                return root.ReplaceNode(declaration, wrapped);
        }
    }
}
