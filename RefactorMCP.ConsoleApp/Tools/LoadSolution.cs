using ModelContextProtocol.Server;
using Microsoft.CodeAnalysis.MSBuild;
using System.ComponentModel;
using System.IO;


public static partial class RefactoringTools
{
    [McpServerTool, Description("Load a solution file for refactoring operations (preferred for large-file refactoring)")]
    public static async Task<string> LoadSolution(
        [Description("Path to the solution file (.sln)")] string solutionPath)
    {
        try
        {
            if (!File.Exists(solutionPath))
            {
                return $"Error: Solution file not found at {solutionPath}";
            }

            if (_loadedSolutions.TryGetValue(solutionPath, out var cached))
            {
                var cachedProjects = cached.Projects.Select(p => p.Name).ToList();
                return $"Successfully loaded solution '{Path.GetFileName(solutionPath)}' with {cachedProjects.Count} projects: {string.Join(", ", cachedProjects)}";
            }

            using var workspace = MSBuildWorkspace.Create();
            var solution = await workspace.OpenSolutionAsync(solutionPath);

            _loadedSolutions[solutionPath] = solution;

            var projects = solution.Projects.Select(p => p.Name).ToList();
            return $"Successfully loaded solution '{Path.GetFileName(solutionPath)}' with {projects.Count} projects: {string.Join(", ", projects)}";
        }
        catch (Exception ex)
        {
            return $"Error loading solution: {ex.Message}";
        }
    }
}
