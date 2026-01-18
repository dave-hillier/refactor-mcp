using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests.Tools;

public class IntroduceVariableToolTests : RefactorMCP.Tests.TestBase
{
    [Fact]
    public async Task IntroduceVariable_CreatesLocalVariable()
    {
        const string initialCode = """
public class Sample
{
    public string FormatResult(int value)
    {
        return $"Result: {value * 2 + 10}";
    }
}
""";

        const string expectedCode = """
public class Sample
{
    public string FormatResult(int value)
    {
        string processedValue = $"Result: {value * 2 + 10}";
        return processedValue;
    }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "IntroduceVariable.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        var result = await IntroduceVariableTool.IntroduceVariable(
            SolutionPath,
            testFile,
            "4:24-4:37",
            "processedValue");

        Assert.Contains("Successfully introduced variable", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Equal(expectedCode, fileContent.Replace("\r\n", "\n"));
    }
}
