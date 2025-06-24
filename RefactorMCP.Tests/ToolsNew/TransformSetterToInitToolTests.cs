using ModelContextProtocol;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests.ToolsNew;

public class TransformSetterToInitToolTests : RefactorMCP.Tests.TestBase
{
    [Fact]
    public async Task TransformSetter_ConvertsToInit()
    {
        const string initialCode = """
public class Sample
{
    public string Name { get; set; } = "";
}
""";

        const string expectedCode = """
public class Sample
{
    public string Name { get; init; } = "";
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "SetterToInit.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        var result = await TransformSetterToInitTool.TransformSetterToInit(
            SolutionPath,
            testFile,
            "Name");

        Assert.Contains("Successfully converted setter", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Equal(expectedCode, fileContent.Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task TransformSetter_InvalidProperty_ReturnsError()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        await Assert.ThrowsAsync<McpException>(async () =>
            await TransformSetterToInitTool.TransformSetterToInit(
                SolutionPath,
                ExampleFilePath,
                "Nonexistent"));
    }
}
