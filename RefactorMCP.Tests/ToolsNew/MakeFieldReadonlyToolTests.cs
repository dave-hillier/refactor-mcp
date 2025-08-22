using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using Xunit;

namespace RefactorMCP.Tests.ToolsNew;

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
        var expected = expectedCode.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
        fileContent = fileContent.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
        Assert.Equal(expected, fileContent);
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
        var expected = expectedCode.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
        fileContent = fileContent.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
        Assert.Equal(expected, fileContent);
    }

    [Fact]
    public async Task MakeFieldReadonly_StaticFieldWithInitializer_KeepsInitializerInline()
    {
        const string initialCode = """
public class Sample
{
    private static string defaultName = "Default";
    public Sample() { }
}
""";

        const string expectedCode = """
public class Sample
{
    private static readonly string defaultName = "Default";
    public Sample() { }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "StaticReadonly.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        var result = await MakeFieldReadonlyTool.MakeFieldReadonly(
            SolutionPath,
            testFile,
            "defaultName");

        Assert.Contains("Successfully made static field 'defaultName' readonly (initialization kept inline)", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        var expected = expectedCode.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
        fileContent = fileContent.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
        Assert.Equal(expected, fileContent);
    }

    [Fact]
    public async Task MakeFieldReadonly_StaticFieldWithoutInitializer_AddsReadonly()
    {
        const string initialCode = """
public class Sample
{
    private static string defaultDescription;
}
""";

        const string expectedCode = """
public class Sample
{
    private static readonly string defaultDescription;
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "StaticReadonlyNoInit.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        var result = await MakeFieldReadonlyTool.MakeFieldReadonly(
            SolutionPath,
            testFile,
            "defaultDescription");

        Assert.Contains("Successfully made static field 'defaultDescription' readonly", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        var expected = expectedCode.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
        fileContent = fileContent.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
        Assert.Equal(expected, fileContent);
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
