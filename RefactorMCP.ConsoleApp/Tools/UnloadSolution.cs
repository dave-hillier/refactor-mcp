using ModelContextProtocol.Server;
using Microsoft.Extensions.Caching.Memory;
using System.ComponentModel;
using System.IO;

public static partial class RefactoringTools
{
    [McpServerTool, Description("Unload a solution and remove it from the cache")]
    public static string UnloadSolution(
        [Description("Path to the solution file (.sln)")] string solutionPath)
    {
        if (_solutionCache.TryGetValue(solutionPath, out _))
        {
            _solutionCache.Remove(solutionPath);
            return $"Unloaded solution '{Path.GetFileName(solutionPath)}' from cache";
        }

        return $"Solution '{Path.GetFileName(solutionPath)}' was not loaded";
    }

    [McpServerTool, Description("Clear all cached solutions")]
    public static string ClearSolutionCache()
    {
        _solutionCache.Dispose();
        _solutionCache = new MemoryCache(new MemoryCacheOptions());
        return "Cleared all cached solutions";
    }
}
