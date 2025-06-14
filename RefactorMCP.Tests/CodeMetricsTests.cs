using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class CodeMetricsTests : IDisposable
{
    private static readonly string SolutionPath = TestUtilities.GetSolutionPath();
    private static readonly string ExampleFilePath = Path.Combine(Path.GetDirectoryName(SolutionPath)!, "RefactorMCP.Tests", "ExampleCode.cs");
    private readonly string _originalDir = Directory.GetCurrentDirectory();

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDir);
    }

    [Fact]
    public async Task GetFileMetrics_ReturnsJson()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath);
        var result = await CodeMetricsTool.GetFileMetrics(SolutionPath, ExampleFilePath);
        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("linesOfCode", out _));
        Assert.True(doc.RootElement.TryGetProperty("classes", out var classes));
        Assert.True(classes.GetArrayLength() > 0);
    }
}

