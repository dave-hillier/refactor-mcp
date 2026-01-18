using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using Xunit;

namespace RefactorMCP.Tests.Tools;

public class MakeFieldReadonlyToolTests : RefactorMCP.Tests.TestBase
{
    [Fact]
    public async Task MakeFieldReadonly_AddsReadonly()
    {
        const string initialCode = """
public class Sample
{
    private string name;
    public Sample() { }
}
""";

        const string expectedCode = """
public class Sample
{
    private readonly string name;
    public Sample() { }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "Readonly.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        var result = await MakeFieldReadonlyTool.MakeFieldReadonly(
            SolutionPath,
            testFile,
            "name");

        Assert.Contains("Successfully made field", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Equal(expectedCode, fileContent.Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task MakeFieldReadonly_FieldWithoutInitializer_AddsReadonly()
    {
        const string initialCode = """
public class Sample
{
    private string description;
}
""";

        const string expectedCode = """
public class Sample
{
    private readonly string description;
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "ReadonlyNoInit.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        var result = await MakeFieldReadonlyTool.MakeFieldReadonly(
            SolutionPath,
            testFile,
            "description");

        Assert.Contains("Successfully made field 'description' readonly", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Equal(expectedCode, fileContent.Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task MakeFieldReadonly_InvalidIdentifier_ReturnsError()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        McpException ex = await Assert.ThrowsAsync<McpException>(() => MakeFieldReadonlyTool.MakeFieldReadonly(
            SolutionPath,
            ExampleFilePath,
            "nonexistent"));

        Assert.Equal("Error: Error: No field named 'nonexistent' found", ex.Message);
    }
}
