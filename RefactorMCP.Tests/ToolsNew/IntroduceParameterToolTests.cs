using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using Xunit;

namespace RefactorMCP.Tests.ToolsNew;

public class IntroduceParameterToolTests : RefactorMCP.Tests.TestBase
{
    [Fact]
    public async Task IntroduceParameter_ValidExpression_ReturnsSuccess()
    {
        var testFile = Path.Combine(TestOutputPath, "IntroduceParameter.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForIntroduceVariable());
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var result = await IntroduceParameterTool.IntroduceParameter(
            SolutionPath,
            testFile,
            "FormatResult",
            "42:42-42:59",
            "processedValue");

        Assert.Contains("Successfully introduced parameter", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("processedValue", fileContent);
    }

    [Fact]
    public async Task IntroduceParameter_InvalidMethod_ReturnsError()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var result = await IntroduceParameterTool.IntroduceParameter(
            SolutionPath,
            ExampleFilePath,
            "Nonexistent",
            "1:1-1:2",
            "param");
        Assert.Equal("Error: No method named 'Nonexistent' found", result);
    }
}
