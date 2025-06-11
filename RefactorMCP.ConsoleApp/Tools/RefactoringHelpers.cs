using ModelContextProtocol.Server;
using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Caching.Memory;
using System;



internal static class RefactoringHelpers
{
    internal static MemoryCache SolutionCache = new(new MemoryCacheOptions());

    private static readonly Lazy<AdhocWorkspace> _workspace =
        new(() => new AdhocWorkspace());

    internal static AdhocWorkspace SharedWorkspace => _workspace.Value;

    internal static async Task<Solution> GetOrLoadSolution(string solutionPath)
    {

        if (SolutionCache.TryGetValue(solutionPath, out Solution? cachedSolution))
        {
            Directory.SetCurrentDirectory(Path.GetDirectoryName(solutionPath)!);
            return cachedSolution!;
        }
        using var workspace = MSBuildWorkspace.Create();
        var solution = await workspace.OpenSolutionAsync(solutionPath);
        SolutionCache.Set(solutionPath, solution);
        Directory.SetCurrentDirectory(Path.GetDirectoryName(solutionPath)!);
        return solution;
    }

    internal static Document? GetDocumentByPath(Solution solution, string filePath)
    {
        var normalizedPath = Path.GetFullPath(filePath);
        return solution.Projects
            .SelectMany(p => p.Documents)
            .FirstOrDefault(d => Path.GetFullPath(d.FilePath ?? "") == normalizedPath);
    }

    internal static bool TryParseRange(string range, out int startLine, out int startColumn, out int endLine, out int endColumn)
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

    internal static string ThrowMcpException(string message)
    {
        throw new McpException(message);
    }
}
