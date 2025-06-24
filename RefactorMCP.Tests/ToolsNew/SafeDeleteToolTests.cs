using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests.ToolsNew;

public class SafeDeleteToolTests : RefactorMCP.Tests.TestBase
{
    [Fact]
    public async Task SafeDeleteField_RemovesUnusedField()
    {
        const string initialCode = """
public class Sample
{
    private int unused;
}
""";

        const string expectedCode = """
public class Sample
{
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "SafeDelete.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        var result = await SafeDeleteTool.SafeDeleteField(
            SolutionPath,
            testFile,
            "unused");

        Assert.Contains("Successfully deleted field", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Equal(expectedCode, fileContent.Replace("\r\n", "\n"));
    }
}
