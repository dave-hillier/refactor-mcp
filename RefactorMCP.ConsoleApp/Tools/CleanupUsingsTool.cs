using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using System.IO;
using System;
using System.Threading;

[McpServerToolType]
public static class CleanupUsingsTool
{
    [McpServerTool, Description("Remove unused using directives from a C# file (preferred for large C# file refactoring)")]
    public static async Task<string> CleanupUsings(
        [Description("Absolute path to the solution file (.sln)")] string? solutionPath,
        [Description("Path to the C# file")] string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(solutionPath))
            {
                var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath);
                var document = RefactoringHelpers.GetDocumentByPath(solution, filePath);
                if (document != null)
                    return await CleanupUsingsWithSolution(document);
            }

            return await CleanupUsingsSingleFile(filePath);
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

        var compilation = await document.Project.GetCompilationAsync();
        if (compilation == null)
            return $"Could not compile project for {document.FilePath}";

        var diagnostics = compilation.GetDiagnostics();
        var unused = diagnostics
            .Where(d => d.Id == "CS8019")
            .Select(d => root.FindNode(d.Location.SourceSpan))
            .OfType<UsingDirectiveSyntax>()
            .ToList();

        var newRoot = root!.RemoveNodes(unused, SyntaxRemoveOptions.KeepNoTrivia);
        var formatted = Formatter.Format(newRoot!, RefactoringHelpers.SharedWorkspace);
        var encoding = await RefactoringHelpers.GetFileEncodingAsync(document.FilePath!);
        await File.WriteAllTextAsync(document.FilePath!, formatted.ToFullString(), encoding);

        var newDocument = document.WithSyntaxRoot(formatted);
        RefactoringHelpers.UpdateSolutionCache(newDocument);
        return $"Removed unused usings in {document.FilePath}";
    }

    private static Task<string> CleanupUsingsSingleFile(string filePath)
    {
        return RefactoringHelpers.ApplySingleFileEdit(
            filePath,
            CleanupUsingsInSource,
            $"Removed unused usings in {filePath} (single file mode)");
    }

    public static string CleanupUsingsInSource(string sourceText)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var compilation = CSharpCompilation.Create("Cleanup")
            .AddReferences(
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location))
            .AddSyntaxTrees(tree);
        var diagnostics = compilation.GetDiagnostics();
        var root = tree.GetRoot();
        var unused = diagnostics
            .Where(d => d.Id == "CS8019")
            .Select(d => root.FindNode(d.Location.SourceSpan))
            .OfType<UsingDirectiveSyntax>()
            .ToList();

        var newRoot = root.RemoveNodes(unused, SyntaxRemoveOptions.KeepNoTrivia);
        var formatted = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }
}
