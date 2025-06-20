using ModelContextProtocol;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class FeatureFlagRefactorTests : TestBase
{
    [Fact]
    public async Task FeatureFlagRefactor_RewritesFile()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "FeatureFlag.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForFeatureFlag());

        var result = await FeatureFlagRefactorTool.FeatureFlagRefactor(
            SolutionPath,
            testFile,
            "CoolFeature");

        Assert.Contains("Refactored feature flag", result);
        var content = await File.ReadAllTextAsync(testFile);
        Assert.Contains("ICoolFeatureStrategy", content);
        Assert.Contains("_coolFeatureStrategy", content);
    }

    [Fact]
    public async Task FeatureFlagRefactor_NoFlagFound_Throws()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "FeatureFlagMissing.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForFeatureFlag());

        await Assert.ThrowsAsync<McpException>(async () =>
            await FeatureFlagRefactorTool.FeatureFlagRefactor(
                SolutionPath,
                testFile,
                "Other"));
    }
}
