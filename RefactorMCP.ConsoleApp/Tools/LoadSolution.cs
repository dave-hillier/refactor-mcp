using ModelContextProtocol.Server;
using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Caching.Memory;
using RefactorMCP.ConsoleApp.Move;
using System.ComponentModel;
using System.IO;
using System.Collections.Generic;
using System.Threading;


[McpServerToolType]
public static class LoadSolutionTool
{
    [McpServerTool, Description("Start a new session by clearing caches then load a solution file and set the current directory")]
    public static async Task<string> LoadSolution(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(solutionPath))
            {
                throw new McpException($"Error: Solution file not found at {solutionPath}");
            }

            RefactoringHelpers.ClearAllCaches();
            MoveMethodTool.ResetMoveHistory();

            var logDir = Path.Combine(Path.GetDirectoryName(solutionPath)!, ".refactor-mcp");
            ToolCallLogger.SetLogDirectory(logDir);
            ToolCallLogger.Log(nameof(LoadSolution), new Dictionary<string, string?> { ["solutionPath"] = solutionPath });

            Directory.SetCurrentDirectory(Path.GetDirectoryName(solutionPath)!);
            progress?.Report($"Loading {solutionPath}");

            if (RefactoringHelpers.SolutionCache.TryGetValue(solutionPath, out Solution? cached))
            {
                var cachedProjects = cached!.Projects.Select(p => p.Name).ToList();
                return $"Successfully loaded solution '{Path.GetFileName(solutionPath)}' with {cachedProjects.Count} projects: {string.Join(", ", cachedProjects)}";
            }

            using var workspace = RefactoringHelpers.CreateWorkspace();
            var solution = await workspace.OpenSolutionAsync(solutionPath, progress: null, cancellationToken);

            RefactoringHelpers.SolutionCache.Set(solutionPath, solution);

            var metricsDir = Path.Combine(Path.GetDirectoryName(solutionPath)!, ".refactor-mcp", "metrics");
            Directory.CreateDirectory(metricsDir);

            var projects = solution.Projects.Select(p => p.Name).ToList();
            var message = $"Successfully loaded solution '{Path.GetFileName(solutionPath)}' with {projects.Count} projects: {string.Join(", ", projects)}";
            progress?.Report(message);
            return message;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error loading solution: {ex.Message}", ex);
        }
    }
}
