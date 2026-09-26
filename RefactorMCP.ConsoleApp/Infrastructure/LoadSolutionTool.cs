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
    [McpServerTool, Description("Optional: every tool loads the solution it is given on first use. " +
        "This clears every loaded solution and cache, then loads this one and lists its projects. " +
        "Use it to start afresh or to load a large solution before the first refactoring")]
    public static async Task<string> LoadSolution(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
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
