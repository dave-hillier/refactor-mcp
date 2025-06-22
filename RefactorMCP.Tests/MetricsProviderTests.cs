using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class MetricsProviderTests : TestBase
{
    [Fact]
    public async Task GetFileMetrics_CachesToDiskAndMemory()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath);

        // First call computes metrics and writes them to disk
        var first = await MetricsProvider.GetFileMetrics(SolutionPath, ExampleFilePath);
        using var json = JsonDocument.Parse(first);
        Assert.True(json.RootElement.TryGetProperty("linesOfCode", out _));

        var solutionDir = Path.GetDirectoryName(SolutionPath)!;
        var relative = Path.GetRelativePath(solutionDir, ExampleFilePath);
        var metricsPath = Path.Combine(solutionDir, ".refactor-mcp", "metrics", relative);
        var metricsFile = Path.ChangeExtension(metricsPath, ".json");

        Assert.True(File.Exists(metricsFile));
        var diskFirst = await File.ReadAllTextAsync(metricsFile);
        Assert.Equal(first, diskFirst);

        // Modify the metrics file on disk
        const string modified = "modified";
        await File.WriteAllTextAsync(metricsFile, modified);

        // Second call should return cached result, not the modified file
        var second = await MetricsProvider.GetFileMetrics(SolutionPath, ExampleFilePath);
        Assert.Equal(first, second);

        var diskSecond = await File.ReadAllTextAsync(metricsFile);
        Assert.Equal(modified, diskSecond);
    }
}
