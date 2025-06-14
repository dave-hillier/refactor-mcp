using ModelContextProtocol.Server;
using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Caching.Memory;
using System.ComponentModel;
using System.IO;


[McpServerToolType]
public static class LoadSolutionTool
{
    [McpServerTool, Description("Load a solution file for refactoring operations and set the current directory to the solution directory")]
    public static async Task<string> LoadSolution(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath)
    {
        try
        {
            if (!File.Exists(solutionPath))
            {
                return RefactoringHelpers.ThrowMcpException($"Error: Solution file not found at {solutionPath}");
            }
            Directory.SetCurrentDirectory(Path.GetDirectoryName(solutionPath)!);

            if (RefactoringHelpers.SolutionCache.TryGetValue(solutionPath, out Solution? cached))
            {
                var cachedProjects = cached.Projects.Select(p => p.Name).ToList();
                return $"Successfully loaded solution '{Path.GetFileName(solutionPath)}' with {cachedProjects.Count} projects: {string.Join(", ", cachedProjects)}";
            }

            using var workspace = RefactoringHelpers.CreateWorkspace();
            var solution = await workspace.OpenSolutionAsync(solutionPath);

            RefactoringHelpers.SolutionCache.Set(solutionPath, solution);

            var projects = solution.Projects.Select(p => p.Name).ToList();
            return $"Successfully loaded solution '{Path.GetFileName(solutionPath)}' with {projects.Count} projects: {string.Join(", ", projects)}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error loading solution: {ex.Message}", ex);
        }
    }
}
