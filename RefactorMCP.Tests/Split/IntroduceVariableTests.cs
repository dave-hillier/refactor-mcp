using ModelContextProtocol;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class IntroduceVariableTests : TestBase
{
    [Fact]
    public async Task IntroduceVariable_ValidExpression_ReturnsSuccess()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "IntroduceVariableTest.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForIntroduceVariable());

        var result = await IntroduceVariableTool.IntroduceVariable(
            SolutionPath,
            testFile,
            "42:50-42:63",
            "processedValue");

        Assert.Contains("Successfully introduced variable", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("processedValue", fileContent);
    }
}
