using ModelContextProtocol;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class CleanupUsingsTests : TestBase
{
    [Fact]
    public async Task CleanupUsings_RemovesUnusedUsings()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "CleanupSample.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForCleanupUsings());

        var result = await CleanupUsingsTool.CleanupUsings(SolutionPath, testFile);

        Assert.Contains("Removed unused usings", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.DoesNotContain("System.Text", fileContent);
    }
}
