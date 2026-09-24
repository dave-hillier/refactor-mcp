using ModelContextProtocol.Server;
using System.ComponentModel;
using System.IO;
using System.Threading;

[McpServerToolType]
public static class UnloadSolutionTool
{
    [McpServerTool, Description("Unload a solution and remove it from the cache")]
    public static string UnloadSolution(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        CancellationToken cancellationToken = default)
    {
        if (SessionRegistry.Unload(solutionPath))
        {
            return $"Unloaded solution '{Path.GetFileName(solutionPath)}' from cache";
        }

        return $"Solution '{Path.GetFileName(solutionPath)}' was not loaded";
    }

    [McpServerTool, Description("Clear all cached solutions")]
    public static string ClearSolutionCache(
        CancellationToken cancellationToken = default)
    {
        RefactoringHelpers.ClearAllCaches();
        return "Cleared all cached solutions";
    }
}
