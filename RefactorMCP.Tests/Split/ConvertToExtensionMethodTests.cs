using ModelContextProtocol;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class ConvertToExtensionMethodTests : TestBase
{
    [Fact]
    public async Task ConvertToExtensionMethod_ReturnsSuccess()
    {
        await RefactoringTools.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "ConvertToExtension.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForConvertToExtension());

        var result = await RefactoringTools.ConvertToExtensionMethod(
            SolutionPath,
            testFile,
            "GetFormattedNumber",
            null);

        Assert.Contains("Successfully converted method 'GetFormattedNumber' to extension method", result);
    }
}
