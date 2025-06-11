using ModelContextProtocol;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class ConvertToStaticWithInstanceTests : TestBase
{
    [Fact]
    public async Task ConvertToStaticWithInstance_ReturnsSuccess()
    {
        await RefactoringTools.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "ConvertToStaticInstance.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForConvertToStaticInstance());

        var result = await RefactoringTools.ConvertToStaticWithInstance(
            SolutionPath,
            testFile,
            "GetFormattedNumber",
            "instance");

        Assert.Contains("Successfully converted method 'GetFormattedNumber' to static with instance parameter", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("static string GetFormattedNumber", fileContent);
        Assert.Contains("Calculator instance", fileContent);
    }
}
