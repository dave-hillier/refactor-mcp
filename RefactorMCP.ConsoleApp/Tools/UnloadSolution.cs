using ModelContextProtocol.Server;
using Microsoft.Extensions.Caching.Memory;
using System.ComponentModel;
using System.IO;

[McpServerToolType]
public static class UnloadSolutionTool
{
    [McpServerTool, Description("Unload a solution and remove it from the cache")]
    public static string UnloadSolution(
        [Description("Path to the solution file (.sln)")] string solutionPath)
    {
        if (RefactoringHelpers.SolutionCache.TryGetValue(solutionPath, out _))
        {
            RefactoringHelpers.SolutionCache.Remove(solutionPath);
            return $"Unloaded solution '{Path.GetFileName(solutionPath)}' from cache";
        }

        return $"Solution '{Path.GetFileName(solutionPath)}' was not loaded";
    }

    [McpServerTool, Description("Clear all cached solutions")]
    public static string ClearSolutionCache()
    {
        RefactoringHelpers.SolutionCache.Dispose();
        RefactoringHelpers.SolutionCache = new MemoryCache(new MemoryCacheOptions());
        return "Cleared all cached solutions";
    }
}
