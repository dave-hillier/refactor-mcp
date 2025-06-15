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
        await LoadSolutionTool.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "ExtractMethodTest.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForExtractMethod());

        var result = await ExtractMethodTool.ExtractMethod(
            SolutionPath,
            testFile,
            "7:9-10:10",
            "ValidateInputs");

        Assert.Contains("Successfully extracted method", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("ValidateInputs();", fileContent);
    }

    [Fact]
    public async Task ExtractMethod_CreatesPrivateMethod()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "ExtractPrivate.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForExtractMethod());

        await ExtractMethodTool.ExtractMethod(
            SolutionPath,
            testFile,
            "7:9-10:10",
            "ValidateInputs");

        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("private void ValidateInputs()", fileContent);
    }

    [Fact]
    public async Task ExtractMethod_InvalidRange_ReturnsError()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath);
        await Assert.ThrowsAsync<McpException>(async () =>
            await ExtractMethodTool.ExtractMethod(
                SolutionPath,
                ExampleFilePath,
                "invalid-range",
                "TestMethod"));
    }

    [Fact]
    public async Task RefactoringTools_FileNotInSolution_ReturnsError()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath);
        await Assert.ThrowsAsync<McpException>(async () =>
            await ExtractMethodTool.ExtractMethod(
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
        await LoadSolutionTool.LoadSolution(SolutionPath);
        await Assert.ThrowsAsync<McpException>(async () =>
            await ExtractMethodTool.ExtractMethod(
                SolutionPath,
                ExampleFilePath,
                range,
                methodName));
    }

    [Theory]
    [InlineData("0:1-1:1", "TestMethod")]
    [InlineData("5:5-3:1", "TestMethod")]
    public async Task ExtractMethod_InvalidRangeValues_ReturnsError(string range, string methodName)
    {
        await LoadSolutionTool.LoadSolution(SolutionPath);
        await Assert.ThrowsAsync<McpException>(async () =>
            await ExtractMethodTool.ExtractMethod(
                SolutionPath,
                ExampleFilePath,
                range,
                methodName));
    }
}
