using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using Xunit;

namespace RefactorMCP.Tests.Tools;

public class ConvertToExtensionMethodToolTests : RefactorMCP.Tests.TestBase
{
    [Fact]
    public async Task ConvertToExtensionMethod_ReturnsSuccess()
    {
        const string initialCode = "public class Calculator { public string GetFormattedNumber(int n){ return $\"{n}\"; } }";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "ConvertToExtension.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        var result = await ConvertToExtensionMethodTool.ConvertToExtensionMethod(
            SolutionPath,
            testFile,
            "GetFormattedNumber",
            null);

        Assert.Contains("Successfully converted method 'GetFormattedNumber' to extension method", result);
    }
}
