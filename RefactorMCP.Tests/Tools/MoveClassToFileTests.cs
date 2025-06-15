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
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.GetFullPath(Path.Combine(TestOutputPath, "MoveClassToFile.cs"));
        await TestUtilities.CreateTestFile(testFile, "public class TempClass { }");

        var result = await MoveClassToFileTool.MoveToSeparateFile(
            SolutionPath,
            testFile,
            "TempClass");

        Assert.Contains("Successfully moved class", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.DoesNotContain("class TempClass", fileContent);
        var newFile = Path.Combine(Path.GetDirectoryName(testFile)!, "TempClass.cs");
        Assert.True(File.Exists(newFile));
        var newFileContent = await File.ReadAllTextAsync(newFile);
        Assert.Contains("class TempClass", newFileContent);
    }

    [Fact]
    public async Task MoveClassToFile_FailsWhenClassExistsInAnotherFile()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var duplicatePath = Path.Combine(Path.GetDirectoryName(SolutionPath)!, "RefactorMCP.Tests", "DuplicateTempClass.cs");
        await File.WriteAllTextAsync(duplicatePath, "public class TempClass { }");

        try
        {
            await LoadSolutionTool.LoadSolution(SolutionPath);
            var testFile = Path.GetFullPath(Path.Combine(TestOutputPath, "MoveClassToFile_Duplicate.cs"));
            await TestUtilities.CreateTestFile(testFile, "public class TempClass { }");

            await Assert.ThrowsAsync<McpException>(() =>
                MoveClassToFileTool.MoveToSeparateFile(
                    SolutionPath,
                    testFile,
                    "TempClass"));
        }
        finally
        {
            if (File.Exists(duplicatePath))
                File.Delete(duplicatePath);
        }
    }
}
