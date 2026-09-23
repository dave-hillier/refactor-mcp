using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace RefactorMCP.ConsoleApp.Tools.Moving;

[McpServerToolType]
public static class SyncNamespaceWithFolderTool
{
    [McpServerTool, Description("Set a file's namespace to the project's root namespace followed by the file's " +
        "folders, updating references and usings across the solution")]
    public static async Task<string> SyncNamespaceWithFolder(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file whose namespace should match its folder")] string filePath,
        CancellationToken cancellationToken = default)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var document = MovingSupport.DocumentOrThrow(solution, filePath);
        var root = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync(cancellationToken))!;
        var model = (await document.GetSemanticModelAsync(cancellationToken))!;

        var namespaces = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().ToList();
        if (namespaces.Count == 0)
            throw new McpException($"Error: {Path.GetFileName(filePath)} declares no namespace; its types are in the global namespace");
        if (namespaces.Count > 1)
            throw new McpException($"Error: {Path.GetFileName(filePath)} declares more than one namespace");

        var declaration = namespaces[0];
        var expected = FolderNamespace(document);
        if (!NamespaceMover.IsValidNamespace(expected))
            throw new McpException($"Error: The folder path gives '{expected}', which is not a valid namespace name");
        if (declaration.Name.ToString() == expected)
            throw new McpException($"Error: The namespace of {Path.GetFileName(filePath)} already matches its folder ({expected})");

        var types = declaration.Members
            .Select(member => model.GetDeclaredSymbol(member, cancellationToken))
            .OfType<INamedTypeSymbol>()
            .ToList();
        foreach (var type in types)
        {
            if (await NamespaceMover.NameTakenAsync(solution, type, expected, cancellationToken))
                throw new McpException($"Error: Namespace {expected} already contains a type named {type.Name}");
            if (type.DeclaringSyntaxReferences.Length > 1)
                throw new McpException($"Error: {type.Name} is a partial type declared in several places; its other parts would stay behind");
        }

        var updated = await NamespaceMover.MoveAsync(
            solution,
            document,
            types,
            expected,
            unit =>
            {
                var ns = unit.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().Single();
                return unit.ReplaceNode(ns, ns.WithName(SyntaxFactory.ParseName(expected).WithTriviaFrom(ns.Name)));
            },
            cancellationToken);
        await MovingSupport.ApplyAsync(solution, updated, cancellationToken);

        return $"Successfully changed the namespace of {Path.GetFileName(filePath)} to {expected}";
    }

    /// <summary>
    /// The project's root namespace, or its name when it sets none, followed
    /// by the folders between the project and the file.
    /// </summary>
    private static string FolderNamespace(Document document)
    {
        var project = document.Project;
        var rootNamespace = string.IsNullOrEmpty(project.DefaultNamespace) ? project.Name : project.DefaultNamespace;
        var folders = Path.GetRelativePath(Path.GetDirectoryName(project.FilePath!)!, Path.GetDirectoryName(document.FilePath!)!)
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
            .Where(part => part != ".")
            .Select(part => part.Replace(' ', '_').Replace('-', '_'));
        return string.Join(".", new[] { rootNamespace }.Concat(folders));
    }
}
