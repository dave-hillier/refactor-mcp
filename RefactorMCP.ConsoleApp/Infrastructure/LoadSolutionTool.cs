using ModelContextProtocol.Server;
using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Caching.Memory;
using RefactorMCP.ConsoleApp.Tools;
using System.ComponentModel;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;


[McpServerToolType]
public static class LoadSolutionTool
{
    [McpServerTool, Description("Start a new session by clearing caches then load a solution file")]
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

            RefactoringHelpers.EnsureMsBuildRegistered();

            // Loading a solution starts a new session: previous sessions, their
            // move history and the parse caches are all dropped first.
            RefactoringHelpers.ClearAllCaches();

            var session = SessionRegistry.GetOrCreate(solutionPath);

            var metricsDir = Path.Combine(session.SolutionDirectory, ".refactor-mcp", "metrics");
            Directory.CreateDirectory(metricsDir);

            var solution = await session.GetOrLoadAsync(progress, cancellationToken);

            var projects = solution.Projects.Select(p => p.Name).ToList();
            var message = $"Successfully loaded solution '{session.SolutionFileName}' with {projects.Count} projects: {string.Join(", ", projects)}";
            progress?.Report(message);
            return message;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error loading solution: {ex.Message}", ex);
        }
    }
}
