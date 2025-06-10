using ModelContextProtocol.Server;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using System.Collections.Generic;


public static partial class RefactoringTools
{
    private static readonly Dictionary<string, Solution> _loadedSolutions = new();

    private static async Task<Solution> GetOrLoadSolution(string solutionPath)
    {
        if (_loadedSolutions.TryGetValue(solutionPath, out var cachedSolution))
            return cachedSolution;

        using var workspace = MSBuildWorkspace.Create();
        var solution = await workspace.OpenSolutionAsync(solutionPath);
        _loadedSolutions[solutionPath] = solution;
        return solution;
    }

    private static Document? GetDocumentByPath(Solution solution, string filePath)
    {
        var normalizedPath = Path.GetFullPath(filePath);
        return solution.Projects
            .SelectMany(p => p.Documents)
            .FirstOrDefault(d => Path.GetFullPath(d.FilePath ?? "") == normalizedPath);
    }

    private static bool TryParseRange(string range, out int startLine, out int startColumn, out int endLine, out int endColumn)
    {
        startLine = startColumn = endLine = endColumn = 0;
        var parts = range.Split('-');
        if (parts.Length != 2) return false;
        var startParts = parts[0].Split(':');
        var endParts = parts[1].Split(':');
        if (startParts.Length != 2 || endParts.Length != 2) return false;
        return int.TryParse(startParts[0], out startLine) &&
               int.TryParse(startParts[1], out startColumn) &&
               int.TryParse(endParts[0], out endLine) &&
               int.TryParse(endParts[1], out endColumn);
    }
}
