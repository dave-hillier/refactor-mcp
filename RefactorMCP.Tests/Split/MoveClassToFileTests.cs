using ModelContextProtocol;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class MoveClassToFileTests : TestBase
{
    [Fact]
    public async Task MoveClassToFile_MovesClassAndCreatesFile()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "MoveClassToFile.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForMoveClassToFile());

        var result = await MoveClassToFileTool.MoveToSeparateFile(
            SolutionPath,
            testFile,
            "Logger");

        Assert.Contains("Successfully moved class", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.DoesNotContain("class Logger", fileContent);
        var newFile = Path.Combine(Path.GetDirectoryName(testFile)!, "Logger.cs");
        Assert.True(File.Exists(newFile));
        var newFileContent = await File.ReadAllTextAsync(newFile);
        Assert.Contains("class Logger", newFileContent);
    }
}
