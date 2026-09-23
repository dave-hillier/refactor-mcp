using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;

[McpServerToolType]
public static class MakeMethodAsyncTool
{
    [McpServerTool, Description("Make a method that blocks on tasks async: it awaits them and returns a task, calls in async methods await it, and every other caller blocks on the task it returns")]
    public static async Task<string> MakeMethodAsync(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file declaring the method")] string filePath,
        [Description("Name of the method")] string methodName,
        [Description("A line of the method's declaration (1-based), to choose between overloads")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var method = (await SolutionEdits.FindMethodAsync(solution, filePath, methodName, line, cancellationToken)).OriginalDefinition;
            var (_, blocking) = await ConvertToAsyncTool.MakeAsyncAsync(solution, method, convertCallers: false, cancellationToken);
            return $"Successfully made '{methodName}' async, with {blocking} call(s) blocking on the task it returns";
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error making method async: {ex.Message}", ex);
        }
    }
}
