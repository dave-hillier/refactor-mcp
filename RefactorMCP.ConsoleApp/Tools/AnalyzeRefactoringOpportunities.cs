using ModelContextProtocol.Server;
using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RefactorMCP.ConsoleApp.SyntaxRewriters;
using System.ComponentModel;

[McpServerToolType, McpServerPromptType]
public static class AnalyzeRefactoringOpportunitiesTool
{
    [McpServerPrompt, Description("Analyze a C# file for refactoring opportunities like long methods or unused code")]
    public static async Task<string> AnalyzeRefactoringOpportunities(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath)
    {
        try
        {
            var (syntaxTree, model, solution) = await LoadSyntaxTreeAndModel(solutionPath, filePath);
            var root = await syntaxTree.GetRootAsync();

            var walker = new RefactoringOpportunityWalker(model, solution);
            walker.Visit(root);
            await walker.PostProcessAsync();
            var suggestions = walker.Suggestions;

            if (suggestions.Count == 0)
                return "No obvious refactoring opportunities detected";

            return "Suggestions:\n" + string.Join("\n", suggestions);
        }
        catch (Exception ex)
        {
            throw new McpException($"Error analyzing file: {ex.Message}", ex);
        }
    }

    private static async Task<(SyntaxTree tree, SemanticModel? model, Solution? solution)> LoadSyntaxTreeAndModel(string solutionPath, string filePath)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath);
        var document = RefactoringHelpers.GetDocumentByPath(solution, filePath);
        if (document != null)
        {
            var tree = await document.GetSyntaxTreeAsync();
            if (tree == null)
            {
                var (text, _) = await RefactoringHelpers.ReadFileWithEncodingAsync(filePath);
                tree = CSharpSyntaxTree.ParseText(text);
            }
            var model = await document.GetSemanticModelAsync();
            return (tree, model, solution);
        }

        var (fileText, _) = await RefactoringHelpers.ReadFileWithEncodingAsync(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(fileText);
        return (syntaxTree, null, null);
    }

}
