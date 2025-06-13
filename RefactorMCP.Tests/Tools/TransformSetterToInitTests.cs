using ModelContextProtocol;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class TransformSetterToInitTests : TestBase
{
    [Fact]
    public async Task TransformSetterToInit_PropertyWithSetter_ReturnsSuccess()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "TransformSetterToInit.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForTransformSetter());

        var result = await TransformSetterToInitTool.TransformSetterToInit(
            SolutionPath,
            testFile,
            "Name");

        Assert.Contains("Successfully converted setter to init", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("init;", fileContent);
    }

    [Fact]
    public async Task TransformSetterToInit_InvalidProperty_ReturnsError()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath);
        await Assert.ThrowsAsync<McpException>(async () =>
            await TransformSetterToInitTool.TransformSetterToInit(
                SolutionPath,
                ExampleFilePath,
                "Nonexistent"));
    }
}
