using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests.ToolsNew;

public class MoveTypeToFileToolTests : RefactorMCP.Tests.TestBase
{
    [Fact]
    public async Task MoveTypeToFile_CreatesNewFile()
    {
        const string initialCode = """
public class TempClass { }
""";

        const string expectedTarget = """
public class TempClass { }
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "MoveType.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        var result = await MoveTypeToFileTool.MoveToSeparateFile(
            SolutionPath,
            testFile,
            "TempClass");

        Assert.Contains("Successfully moved type", result);
        var sourceContent = await File.ReadAllTextAsync(testFile);
        Assert.True(string.IsNullOrWhiteSpace(sourceContent));
        var targetFile = Path.Combine(TestOutputPath, "TempClass.cs");
        var targetContent = await File.ReadAllTextAsync(targetFile);
        Assert.Equal(expectedTarget, targetContent.Replace("\r\n", "\n"));
    }
}
