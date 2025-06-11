using ModelContextProtocol;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class ExtractMethodTests : TestBase
{
    [Fact]
    public async Task ExtractMethod_ValidSelection_ReturnsSuccess()
    {
        await RefactoringTools.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "ExtractMethodTest.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForExtractMethod());

        var result = await RefactoringTools.ExtractMethod(
            SolutionPath,
            testFile,
            "7:9-10:10",
            "ValidateInputs");

        Assert.Contains("Successfully extracted method", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("ValidateInputs();", fileContent);
    }

    [Fact]
    public async Task ExtractMethod_InvalidRange_ReturnsError()
    {
        await RefactoringTools.LoadSolution(SolutionPath);
        await Assert.ThrowsAsync<McpException>(async () =>
            await RefactoringTools.ExtractMethod(
                SolutionPath,
                ExampleFilePath,
                "invalid-range",
                "TestMethod"));
    }

    [Fact]
    public async Task RefactoringTools_FileNotInSolution_ReturnsError()
    {
        await RefactoringTools.LoadSolution(SolutionPath);
        await Assert.ThrowsAsync<McpException>(async () =>
            await RefactoringTools.ExtractMethod(
                SolutionPath,
                "./NonExistent.cs",
                "1:1-2:2",
                "TestMethod"));
    }

    [Theory]
    [InlineData("1:1-", "TestMethod")]
    [InlineData("1-2:2", "TestMethod")]
    [InlineData("abc:def-ghi:jkl", "TestMethod")]
    [InlineData("1:1-2", "TestMethod")]
    public async Task ExtractMethod_InvalidRangeFormats_ReturnsError(string range, string methodName)
    {
        await RefactoringTools.LoadSolution(SolutionPath);
        await Assert.ThrowsAsync<McpException>(async () =>
            await RefactoringTools.ExtractMethod(
                SolutionPath,
                ExampleFilePath,
                range,
                methodName));
    }
}
