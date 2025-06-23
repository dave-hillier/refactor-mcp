using ModelContextProtocol;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class MoveTypeToFileTests : TestBase
{
    [Theory]
    [InlineData("public class TempClass { }", "TempClass")]
    [InlineData("public interface ITemp { }", "ITemp")]
    [InlineData("public struct TempStruct { }", "TempStruct")]
    [InlineData("public enum TempEnum { A, B }", "TempEnum")]
    [InlineData("public record TempRecord(int X);", "TempRecord")]
    [InlineData("public readonly record struct TempRecordStruct(int X);", "TempRecordStruct")]
    [InlineData("public delegate void TempDelegate();", "TempDelegate")]
    public async Task MoveTypeToFile_MovesTypeAndCreatesFile(string code, string typeName)
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.GetFullPath(Path.Combine(TestOutputPath, $"MoveType_{typeName}.cs"));
        await TestUtilities.CreateTestFile(testFile, code);

        var result = await MoveTypeToFileTool.MoveToSeparateFile(
            SolutionPath,
            testFile,
            typeName);

        Assert.Contains("Successfully moved type", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.True(string.IsNullOrWhiteSpace(fileContent));
        var newFile = Path.Combine(Path.GetDirectoryName(testFile)!, $"{typeName}.cs");
        Assert.True(File.Exists(newFile));
        var newFileContent = await File.ReadAllTextAsync(newFile);
        Assert.Contains(typeName, newFileContent);
    }

    [Fact]
    public async Task MoveTypeToFile_FailsWhenTypeExistsInAnotherFile()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var duplicatePath = Path.Combine(Path.GetDirectoryName(SolutionPath)!, "RefactorMCP.Tests", "DuplicateTempType.cs");
        await File.WriteAllTextAsync(duplicatePath, "public interface ITemp { }");

        try
        {
            await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
            var testFile = Path.GetFullPath(Path.Combine(TestOutputPath, "MoveTypeToFile_Duplicate.cs"));
            await TestUtilities.CreateTestFile(testFile, "public interface ITemp { }");

            await Assert.ThrowsAsync<McpException>(() =>
                MoveTypeToFileTool.MoveToSeparateFile(
                    SolutionPath,
                    testFile,
                    "ITemp"));
        }
        finally
        {
            if (File.Exists(duplicatePath))
                File.Delete(duplicatePath);
        }
    }
}
