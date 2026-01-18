using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using Xunit;

namespace RefactorMCP.Tests.Tools;

public class CreateAdapterToolTests : RefactorMCP.Tests.TestBase
{
    [Fact]
    public async Task CreateAdapter_AddsClass()
    {
        const string initialCode = "public class LegacyLogger { public void Write(string message){ System.Console.WriteLine(message); } }";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "Adapter.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        var result = await CreateAdapterTool.CreateAdapter(
            SolutionPath,
            testFile,
            "LegacyLogger",
            "Write",
            "LoggerAdapter");

        Assert.Contains("Created adapter", result);
        var text = await File.ReadAllTextAsync(testFile);
        Assert.Contains("LoggerAdapter", text);
    }
}
