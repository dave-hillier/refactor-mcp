using ModelContextProtocol;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class IntroduceParameterTests : TestBase
{
    [Fact]
    public async Task IntroduceParameter_ValidExpression_ReturnsSuccess()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "IntroduceParameter.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForIntroduceVariable());

        var result = await IntroduceParameterTool.IntroduceParameter(
            SolutionPath,
            testFile,
            "FormatResult",
            "42:50-42:63",
            "processedValue");

        Assert.Contains("Successfully introduced parameter", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("processedValue", fileContent);
    }

    [Fact]
    public async Task IntroduceParameter_InvalidMethod_ReturnsError()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath);
        await Assert.ThrowsAsync<McpException>(async () =>
            await IntroduceParameterTool.IntroduceParameter(
                SolutionPath,
                ExampleFilePath,
                "Nonexistent",
                "1:1-1:2",
                "param"));
    }
}
