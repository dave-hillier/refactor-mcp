using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class ClassLengthMetricsTests : IDisposable
{
    private static readonly string SolutionPath = GetSolutionPath();
    private readonly string _originalDir = Directory.GetCurrentDirectory();

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDir);
    }

    private static string GetSolutionPath()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var dir = new DirectoryInfo(currentDir);
        while (dir != null)
        {
            var sln = Path.Combine(dir.FullName, "RefactorMCP.sln");
            if (File.Exists(sln)) return sln;
            dir = dir.Parent;
        }
        return "./RefactorMCP.sln";
    }

    [Fact]
    public async Task ListClassLengths_ReturnsMetrics()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath);
        var result = await ClassLengthMetricsTool.ListClassLengths(SolutionPath);
        Assert.Contains("Calculator", result);
        Assert.Contains("MathUtilities", result);
    }
}
