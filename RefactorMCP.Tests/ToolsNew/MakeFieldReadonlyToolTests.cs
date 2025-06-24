using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests.ToolsNew;

public class MakeFieldReadonlyToolTests : RefactorMCP.Tests.TestBase
{
    [Fact]
    public async Task MakeFieldReadonly_AddsReadonly()
    {
        const string initialCode = """
public class Sample
{
    private string name;
    public Sample() { }
}
""";

        const string expectedCode = """
public class Sample
{
    private readonly string name;
    public Sample() { }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "Readonly.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        var result = await MakeFieldReadonlyTool.MakeFieldReadonly(
            SolutionPath,
            testFile,
            "name");

        Assert.Contains("Successfully made field", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Equal(expectedCode, fileContent.Replace("\r\n", "\n"));
    }
}
