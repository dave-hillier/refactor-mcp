using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class MetricsResourceTests : TestBase
{
    [Fact]
    public async Task ReadMetrics_File_ReturnsJson()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath);
        var result = await MetricsResource.ReadMetrics(ExampleFilePath, SolutionPath);
        using var doc = JsonDocument.Parse(result.Text);
        Assert.True(doc.RootElement.TryGetProperty("linesOfCode", out _));
    }
}
