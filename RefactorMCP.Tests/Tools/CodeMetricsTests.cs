using ModelContextProtocol;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class CodeMetricsToolTests : TestBase
{
    [Fact]
    public async Task RepeatedCalls_UseCachedValue()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath);
        var cacheFile = Path.Combine(Path.GetDirectoryName(SolutionPath)!, "codeMetricsCache.json");
        if (File.Exists(cacheFile))
            File.Delete(cacheFile);

        var metricsFile = Path.Combine(TestOutputPath, "MetricsSample.cs");
        await TestUtilities.CreateTestFile(metricsFile, TestUtilities.GetSampleCodeForIntroduceField());

        var first = await CodeMetricsTool.GetFileMetrics(SolutionPath, metricsFile);
        await File.AppendAllTextAsync(metricsFile, "\npublic class Added {}\n");
        var second = await CodeMetricsTool.GetFileMetrics(SolutionPath, metricsFile);

        Assert.Equal(first, second);
        Assert.True(File.Exists(cacheFile));
    }
}
