using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RefactorMCP.ConsoleApp.Move;
using Xunit;

namespace RefactorMCP.Tests.ToolsNew;

public class MoveInstanceMethodToolTests : RefactorMCP.Tests.TestBase
{
    [Fact]
    public async Task MoveInstanceMethod_CreatesTargetFile()
    {
        const string initialCode = """
public class A
{
    public int Bar() { return 1; }
}
""";

        const string expectedSource = """
public class A
{
    public int Bar()
    {
        return B.Bar();
    }
}
""";

        const string expectedTarget = """
public class B
{
    public static int Bar()
    {
        return 1;
    }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "MoveInstance.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        var targetFile = Path.Combine(TestOutputPath, "B.cs");
        var result = await MoveMethodTool.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "A",
            new[] { "Bar" },
            "B",
            targetFile,
            Array.Empty<string>(),
            Array.Empty<string>());

        Assert.Contains("Successfully moved", result);
        var sourceContent = await File.ReadAllTextAsync(testFile);
        Assert.DoesNotContain("int Bar() { return 1; }", sourceContent);
        var targetContent = await File.ReadAllTextAsync(targetFile);
        Assert.Contains("static int Bar", targetContent);
    }
}
