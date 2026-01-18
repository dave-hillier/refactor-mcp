using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

[McpServerToolType]
public static class FeatureFlagRefactorTool
{
    [McpServerTool, Description("Convert feature flag condition to strategy pattern")]
    public static async Task<string> FeatureFlagRefactor(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Feature flag name")] string flagName)
    {
        try
        {
            return await RefactoringHelpers.RunWithSolutionOrFile(
                solutionPath,
                filePath,
                doc => RefactorWithSolution(doc, flagName),
                path => RefactorSingleFile(path, flagName));
        }
        catch (Exception ex)
        {
            throw new McpException($"Error refactoring feature flag: {ex.Message}", ex);
        }
    }

    private static async Task<string> RefactorWithSolution(Document document, string flagName)
    {
        var sourceText = await document.GetTextAsync();
        var syntaxRoot = await document.GetSyntaxRootAsync();
        var rewriter = new FeatureFlagRewriter(flagName);
        var newRoot = (CompilationUnitSyntax)rewriter.Visit(syntaxRoot!);
        if (!rewriter.GeneratedMembers.Any())
            throw new McpException($"Error: Feature flag '{flagName}' not found");
        newRoot = newRoot.AddMembers(rewriter.GeneratedMembers.ToArray());
        var formattedRoot = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        var newDocument = document.WithSyntaxRoot(formattedRoot);
        var newText = await newDocument.GetTextAsync();
        var encoding = await RefactoringHelpers.GetFileEncodingAsync(document.FilePath!);
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString(), encoding);
        RefactoringHelpers.UpdateSolutionCache(newDocument);
        Log(document.FilePath!, flagName);
        return $"Refactored feature flag '{flagName}' in {document.FilePath} (solution mode)";
    }

    private static Task<string> RefactorSingleFile(string filePath, string flagName)
    {
        return RefactoringHelpers.ApplySingleFileEdit(
            filePath,
            text => RefactorInSource(text, flagName),
            $"Refactored feature flag '{flagName}' in {filePath} (single file mode)");
    }

    public static string RefactorInSource(string sourceText, string flagName)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetRoot();
        var rewriter = new FeatureFlagRewriter(flagName);
        var newRoot = (CompilationUnitSyntax)rewriter.Visit(root);
        if (!rewriter.GeneratedMembers.Any())
            throw new McpException($"Error: Feature flag '{flagName}' not found");
        newRoot = newRoot.AddMembers(rewriter.GeneratedMembers.ToArray());
        var formatted = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }

    private static void Log(string file, string flag)
    {
        try
        {
            var path = Path.Combine(Path.GetDirectoryName(file)!, "refactor-report.json");
            var entry = $"{{\"file\":\"{file}\",\"flag\":\"{flag}\",\"timestamp\":\"{DateTime.UtcNow:o}\"}}";
            File.AppendAllText(path, entry + System.Environment.NewLine);
        }
        catch
        {
        }
    }
}
