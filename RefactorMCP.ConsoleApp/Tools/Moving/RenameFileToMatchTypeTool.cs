using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace RefactorMCP.ConsoleApp.Tools.Moving;

[McpServerToolType]
public static class RenameFileToMatchTypeTool
{
    [McpServerTool, Description("Rename a C# file to the name of the single top-level type it declares, " +
        "keeping it in the same folder and project")]
    public static async Task<string> RenameFileToMatchType(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file to rename")] string filePath,
        CancellationToken cancellationToken = default)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var document = MovingSupport.DocumentOrThrow(solution, filePath);
        var root = await document.GetSyntaxRootAsync(cancellationToken)
            ?? throw new McpException($"Error: {filePath} has no syntax tree");

        var typeNames = TopLevelTypeNames(root);
        if (typeNames.Count == 0)
            throw new McpException($"Error: {filePath} declares no top-level type");
        if (typeNames.Count > 1)
            throw new McpException($"Error: {filePath} declares more than one top-level type ({string.Join(", ", typeNames)})");

        var oldPath = document.FilePath!;
        var newPath = Path.Combine(Path.GetDirectoryName(oldPath)!, $"{typeNames[0]}.cs");
        if (string.Equals(oldPath, newPath, StringComparison.Ordinal))
            throw new McpException($"Error: The file name already matches the type '{typeNames[0]}'");

        // A change of case only is still a rename, even where the file system
        // reports the new name as taken by the file itself.
        var caseOnly = string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase);
        if (!caseOnly && (File.Exists(newPath) || RefactoringHelpers.GetDocumentByPath(solution, newPath) is not null))
            throw new McpException($"Error: A file named {Path.GetFileName(newPath)} already exists in {Path.GetDirectoryName(newPath)}");

        var project = document.Project.RemoveDocument(document.Id);
        var renamed = MovingSupport.AddDocument(project, newPath, root);
        await MovingSupport.ApplyAsync(solution, renamed.Project.Solution, cancellationToken);

        return $"Successfully renamed {Path.GetFileName(oldPath)} to {Path.GetFileName(newPath)}";
    }

    /// <summary>Names of the types declared outside any other type, each once.</summary>
    internal static System.Collections.Generic.List<string> TopLevelTypeNames(SyntaxNode root) =>
        root.DescendantNodes(node => node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax)
            .Where(node => node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax)
            .Select(node => node switch
            {
                BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
                DelegateDeclarationSyntax del => del.Identifier.ValueText,
                _ => throw new InvalidOperationException(),
            })
            .Distinct(StringComparer.Ordinal)
            .ToList();
}
