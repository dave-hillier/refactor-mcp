using ModelContextProtocol;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class CreateAdapterTests : TestBase
{
    [Fact]
    public async Task CreateAdapter_AddsClass()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "Adapter.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForAdapter());

        var result = await CreateAdapterTool.CreateAdapter(
            SolutionPath,
            testFile,
            "LegacyLogger",
            "Write",
            "LoggerAdapter");

        Assert.Contains("Created adapter", result);
        var text = await File.ReadAllTextAsync(testFile);
        Assert.Contains("LoggerAdapter", text);
    }
}
