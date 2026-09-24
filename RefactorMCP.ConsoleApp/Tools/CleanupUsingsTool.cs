using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.IO;
using System;
using System.Threading;

[McpServerToolType]
public static class CleanupUsingsTool
{
    [McpServerTool, Description("Remove unused using directives from a C# file (preferred for large C# file refactoring)")]
    public static async Task<string> CleanupUsings(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await RefactoringHelpers.RunWithSolution(solutionPath, filePath, CleanupUsingsWithSolution);
        }
        catch (Exception ex)
        {
            throw new McpException($"Error cleaning up usings: {ex.Message}", ex);
        }
    }

    private static async Task<string> CleanupUsingsWithSolution(Document document)
    {
        var root = await document.GetSyntaxRootAsync();
        if (root == null)
            return $"No content in {document.FilePath}";

        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null)
            return $"Could not get semantic model for {document.FilePath}";

        var diagnostics = semanticModel.GetDiagnostics();
        var unused = diagnostics
            .Where(d => d.Id == "CS8019")
            .Select(d => root.FindNode(d.Location.SourceSpan))
            .OfType<UsingDirectiveSyntax>()
            .Distinct()
            .ToList();

        if (unused.Count == 0)
            return $"No unused usings in {document.FilePath}";

        // Only the directives change; the rest of the file stays as written.
        var newRoot = DeclarationRemoval.RemoveUsings(root, unused);
        var encoding = await RefactoringHelpers.GetFileEncodingAsync(document.FilePath!);
        await File.WriteAllTextAsync(document.FilePath!, newRoot.ToFullString(), encoding);

        var newDocument = document.WithSyntaxRoot(newRoot);
        RefactoringHelpers.UpdateSolutionCache(newDocument);
        return $"Removed unused usings in {document.FilePath}";
    }

}
