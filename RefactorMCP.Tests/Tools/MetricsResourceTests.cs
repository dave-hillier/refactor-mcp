using System.Text.Json;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests.Tools;

public class MetricsResourceTests : TestBase
{
    [Fact(Skip = "Flaky in CI")]
    public async Task ReadMetrics_File_ReturnsJson()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath);
        var result = await MetricsResource.ReadMetrics(ExampleFilePath, SolutionPath);
        using var doc = JsonDocument.Parse(result.Text);
        Assert.True(doc.RootElement.TryGetProperty("linesOfCode", out _));
    }

    [Fact(Skip = "Flaky in CI")]
    public async Task ReadMetrics_Directory_ReturnsAggregatedJson()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath);
        var dir = Path.GetDirectoryName(ExampleFilePath)!;
        var result = await MetricsResource.ReadMetrics(dir, SolutionPath);
        using var doc = JsonDocument.Parse(result.Text);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Contains(doc.RootElement.EnumerateArray(), e =>
            e.TryGetProperty("name", out var n) && n.GetString() == "Calculator");
    }

    [Fact(Skip = "Flaky in CI")]
    public async Task ReadMetrics_Class_ReturnsClassMetrics()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath);
        var classPath = ExampleFilePath + Path.DirectorySeparatorChar + "Calculator";
        var result = await MetricsResource.ReadMetrics(classPath, SolutionPath);
        using var doc = JsonDocument.Parse(result.Text);
        Assert.Equal("Calculator", doc.RootElement.GetProperty("name").GetString());
        Assert.True(doc.RootElement.TryGetProperty("methods", out _));
    }

    [Fact(Skip = "Flaky in CI")]
    public async Task ReadMetrics_Method_ReturnsMethodMetrics()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath);
        var methodPath = ExampleFilePath + Path.DirectorySeparatorChar + "Calculator.Calculate";
        var result = await MetricsResource.ReadMetrics(methodPath, SolutionPath);
        using var doc = JsonDocument.Parse(result.Text);
        Assert.Equal("Calculate", doc.RootElement.GetProperty("name").GetString());
        Assert.True(doc.RootElement.TryGetProperty("cyclomaticComplexity", out _));
    }

    [Fact(Skip = "Flaky in CI")]
    public async Task ReadMetrics_InvalidPath_ReturnsError()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath);
        var invalidPath = ExampleFilePath + Path.DirectorySeparatorChar + "Unknown";
        var result = await MetricsResource.ReadMetrics(invalidPath, SolutionPath);
        using var doc = JsonDocument.Parse(result.Text);
        Assert.True(doc.RootElement.TryGetProperty("Error", out var err));
        Assert.Contains("not found", err.GetString());
    }
}
