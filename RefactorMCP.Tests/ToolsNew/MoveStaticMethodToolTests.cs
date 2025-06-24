using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RefactorMCP.ConsoleApp.Move;
using Xunit;

namespace RefactorMCP.Tests.ToolsNew;

public class MoveStaticMethodToolTests : RefactorMCP.Tests.TestBase
{
    [Fact]
    public async Task MoveStaticMethod_CreatesTargetFile()
    {
        const string initialCode = """
public class SourceClass
{
    public static int Foo() { return 1; }
}
""";

        const string expectedSource = """
public class SourceClass
{
    public static int Foo()
    {
        return TargetClass.Foo();
    }
}
""";

        const string expectedTarget = """
public class TargetClass
{
    public static int Foo()
    {
        return 1;
    }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "MoveStatic.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        var result = await MoveMethodTool.MoveStaticMethod(
            SolutionPath,
            testFile,
            "Foo",
            "TargetClass");

        Assert.Contains("Successfully moved static method", result);
        var sourceContent = await File.ReadAllTextAsync(testFile);
        Assert.DoesNotContain("Foo() { return 1; }", sourceContent);
        var targetFile = Path.Combine(TestOutputPath, "TargetClass.cs");
        var targetContent = await File.ReadAllTextAsync(targetFile);
        Assert.Contains("static int Foo", targetContent);
    }
}
