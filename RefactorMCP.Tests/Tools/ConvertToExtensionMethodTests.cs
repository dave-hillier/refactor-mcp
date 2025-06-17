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
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "ConvertToExtension.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForConvertToExtension());

        var result = await ConvertToExtensionMethodTool.ConvertToExtensionMethod(
            SolutionPath,
            testFile,
            "GetFormattedNumber",
            null);

        Assert.Contains("Successfully converted method 'GetFormattedNumber' to extension method", result);
    }
}
