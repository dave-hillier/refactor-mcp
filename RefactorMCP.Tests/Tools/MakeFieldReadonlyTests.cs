using ModelContextProtocol;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class MakeFieldReadonlyTests : TestBase
{
    [Fact]
    public async Task MakeFieldReadonly_FieldWithInitializer_ReturnsSuccess()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "MakeFieldReadonlyTest.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForMakeFieldReadonly());

        var result = await MakeFieldReadonlyTool.MakeFieldReadonly(
            SolutionPath,
            testFile,
            "format");

        Assert.Contains("Successfully made field 'format' readonly", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("readonly string format", fileContent);
    }

    [Fact]
    public async Task MakeFieldReadonly_FieldWithoutInitializer_ReturnsSuccess()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "MakeFieldReadonlyNoInitTest.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForMakeFieldReadonlyNoInit());

        var result = await MakeFieldReadonlyTool.MakeFieldReadonly(
            SolutionPath,
            testFile,
            "description");

        Assert.Contains("Successfully made field 'description' readonly", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("readonly string description", fileContent);
    }

    [Fact]
    public async Task MakeFieldReadonly_InvalidLine_ReturnsError()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        await Assert.ThrowsAsync<McpException>(async () =>
            await MakeFieldReadonlyTool.MakeFieldReadonly(
                SolutionPath,
                ExampleFilePath,
                "nonexistent"));
    }
}
