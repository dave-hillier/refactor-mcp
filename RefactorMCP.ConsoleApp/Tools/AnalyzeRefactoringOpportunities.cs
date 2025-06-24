using ModelContextProtocol.Server;
using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RefactorMCP.ConsoleApp.SyntaxWalkers;
using System.ComponentModel;
using System.Threading;

[McpServerToolType, McpServerPromptType]
public static class AnalyzeRefactoringOpportunitiesTool
{
    [McpServerPrompt, Description("Analyze a C# file for refactoring opportunities like long methods or unused code")]
    public static async Task<string> AnalyzeRefactoringOpportunities(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (syntaxTree, model, solution) = await LoadSyntaxTreeAndModel(solutionPath, filePath, cancellationToken);
            var root = await syntaxTree.GetRootAsync(cancellationToken);

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

    private static async Task<(SyntaxTree tree, SemanticModel? model, Solution? solution)> LoadSyntaxTreeAndModel(string solutionPath, string filePath, CancellationToken cancellationToken)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var document = RefactoringHelpers.GetDocumentByPath(solution, filePath);
        if (document != null)
        {
            var tree = await document.GetSyntaxTreeAsync(cancellationToken);
            if (tree == null)
            {
                var (text, _) = await RefactoringHelpers.ReadFileWithEncodingAsync(filePath, cancellationToken);
                tree = CSharpSyntaxTree.ParseText(text);
            }
            var model = await document.GetSemanticModelAsync(cancellationToken);
            return (tree, model, solution);
        }

        var (fileText, _) = await RefactoringHelpers.ReadFileWithEncodingAsync(filePath, cancellationToken);
        var syntaxTree = CSharpSyntaxTree.ParseText(fileText);
        return (syntaxTree, null, null);
    }

}
