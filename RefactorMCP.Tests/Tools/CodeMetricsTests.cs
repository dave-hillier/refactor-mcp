using ModelContextProtocol;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace RefactorMCP.Tests;

public class CodeMetricsToolTests : TestBase
{
    [Fact]
    public async Task RepeatedCalls_UseCachedValue()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath);
        var field = typeof(CodeMetricsTool).GetField("_cache", BindingFlags.NonPublic | BindingFlags.Static);
        (field?.GetValue(null) as MemoryCache)?.Compact(1.0);
        var cacheFile = Path.Combine(Path.GetDirectoryName(SolutionPath)!, "codeMetricsCache.json");
        if (File.Exists(cacheFile))
            File.Delete(cacheFile);

        var metricsFile = Path.Combine(TestOutputPath, $"MetricsSample_{Guid.NewGuid()}.cs");
        await TestUtilities.CreateTestFile(metricsFile, TestUtilities.GetSampleCodeForIntroduceField());

        var first = await CodeMetricsTool.GetFileMetrics(SolutionPath, metricsFile);
        await File.AppendAllTextAsync(metricsFile, "\npublic class Added {}\n");
        var second = await CodeMetricsTool.GetFileMetrics(SolutionPath, metricsFile);

        Assert.Equal(first, second);
        Assert.True(File.Exists(cacheFile));
    }
}
