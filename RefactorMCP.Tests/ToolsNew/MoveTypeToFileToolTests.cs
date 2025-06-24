using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using Xunit;

namespace RefactorMCP.Tests.ToolsNew;

public class MoveTypeToFileToolTests : RefactorMCP.Tests.TestBase
{
    [Theory]
    [InlineData("public class TempClass { }", "TempClass")]
    [InlineData("public interface ITemp { }", "ITemp")]
    [InlineData("public struct TempStruct { }", "TempStruct")]
    [InlineData("public enum TempEnum { A, B }", "TempEnum")]
    [InlineData("public record TempRecord(int X);", "TempRecord")]
    [InlineData("public readonly record struct TempRecordStruct(int X);", "TempRecordStruct")]
    [InlineData("public delegate void TempDelegate();", "TempDelegate")]
    public async Task MoveTypeToFile_CreatesNewFile(string code, string typeName)
    {
        var testFile = Path.Combine(TestOutputPath, $"MoveType_{typeName}.cs");
        await TestUtilities.CreateTestFile(testFile, code);

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var result = await MoveTypeToFileTool.MoveToSeparateFile(
            SolutionPath,
            testFile,
            typeName);

        Assert.Contains("Successfully moved type", result);
        var sourceContent = await File.ReadAllTextAsync(testFile);
        Assert.True(string.IsNullOrWhiteSpace(sourceContent));
        var targetFile = Path.Combine(TestOutputPath, $"{typeName}.cs");
        var targetContent = await File.ReadAllTextAsync(targetFile);
        Assert.Equal(
            code.Replace("\r\n", "\n").TrimEnd(),
            targetContent.Replace("\r\n", "\n").TrimEnd());
    }

    [Fact]
    public async Task MoveTypeToFile_FailsWhenTypeExistsInAnotherFile()
    {
        var duplicatePath = Path.Combine(Path.GetDirectoryName(SolutionPath)!, "RefactorMCP.Tests", "DuplicateTempType.cs");
        await File.WriteAllTextAsync(duplicatePath, "public interface ITemp { }");

        try
        {
            await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
            var testFile = Path.Combine(TestOutputPath, "MoveType_Duplicate.cs");
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
