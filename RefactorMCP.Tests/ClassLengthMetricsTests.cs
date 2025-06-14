using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class ClassLengthMetricsTests : IDisposable
{
    private static readonly string SolutionPath = TestHelpers.GetSolutionPath();
    private readonly string _originalDir = Directory.GetCurrentDirectory();

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDir);
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
