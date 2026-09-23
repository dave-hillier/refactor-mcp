using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

[McpServerToolType]
public static class InlineConstantTool
{
    [McpServerTool, Description("Replace every use of a constant field across the solution with its value, then remove the constant")]
    public static async Task<string> InlineConstant(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file declaring the constant")] string filePath,
        [Description("Name of the constant to inline")] string constantName)
    {
        try
        {
            var document = await FieldPropertyRefactoring.GetDocumentAsync(solutionPath, filePath);
            var field = await FieldPropertyRefactoring.FindFieldAsync(document, constantName);
            constantName = field.Name;
            if (!field.IsConst)
                throw new McpException($"Error: '{constantName}' is not a constant");

            var solution = document.Project.Solution;
            var references = await SymbolFinder.FindReferencesAsync(field, solution);
            var inlined = await FieldPropertyRefactoring.InlineFieldValueAsync(solution, field, references);

            await FieldPropertyRefactoring.WriteChangesAsync(solution, inlined);
            return $"Successfully inlined constant '{constantName}'";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error inlining constant: {ex.Message}", ex);
        }
    }
}
