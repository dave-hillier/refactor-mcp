using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests.ToolsNew;

public class ExtractMethodToolTests : RefactorMCP.Tests.TestBase
{
    [Fact]
    public async Task ExtractMethod_CreatesNewMethod()
    {
        const string initialCode = """
using System;

public class Sample
{
    public int Calc(int a, int b)
    {
        if (a < 0 || b < 0)
        {
            throw new ArgumentException();
        }
        var result = a + b;
        return result;
    }
}
""";

        const string expectedCode = """
using System;

public class Sample
{
    public int Calc(int a, int b)
    {
        ValidateInputs();
        var result = a + b;
        return result;
    }

    private void ValidateInputs()
    {
        if (a < 0 || b < 0)
        {
            throw new ArgumentException();
        }
    }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "ExtractMethod.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        var result = await ExtractMethodTool.ExtractMethod(
            SolutionPath,
            testFile,
            "6:9-9:10",
            "ValidateInputs");

        Assert.Contains("Successfully extracted method", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Equal(expectedCode, fileContent.Replace("\r\n", "\n"));
    }
}
