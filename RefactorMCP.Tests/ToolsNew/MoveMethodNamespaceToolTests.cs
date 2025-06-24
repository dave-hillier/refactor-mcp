using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RefactorMCP.ConsoleApp.Move;
using Xunit;

namespace RefactorMCP.Tests.ToolsNew;

public class MoveMethodNamespaceToolTests : RefactorMCP.Tests.TestBase
{
    [Fact]
    public async Task MoveInstanceMethod_PreservesNamespaceInNewFile()
    {
        UnloadSolutionTool.ClearSolutionCache();
        const string initialCode = "namespace Sample.Namespace { public class A { public void Foo() {} } }";
        var testFile = Path.Combine(TestOutputPath, "NamespaceSample.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var targetFile = Path.Combine(Path.GetDirectoryName(testFile)!, "B.cs");
        var result = await MoveMethodTool.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "A",
            new[] { "Foo" },
            "B",
            targetFile,
            Array.Empty<string>(),
            Array.Empty<string>(),
            null,
            CancellationToken.None);

        Assert.Contains("Successfully moved", result);
        var newContent = await File.ReadAllTextAsync(targetFile);
        Assert.Contains("namespace Sample.Namespace", newContent);
    }

    [Fact]
    public async Task MoveInstanceMethod_DoesNotAddNamespaceUsing()
    {
        UnloadSolutionTool.ClearSolutionCache();
        const string initialCode = "namespace Sample.Namespace { public class A { public void Foo() {} } }";
        var testFile = Path.Combine(TestOutputPath, "NamespaceUsingSample.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var targetFile = Path.Combine(Path.GetDirectoryName(testFile)!, "C.cs");
        var result = await MoveMethodTool.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "A",
            new[] { "Foo" },
            "C",
            targetFile,
            Array.Empty<string>(),
            Array.Empty<string>(),
            null,
            CancellationToken.None);

        Assert.Contains("Successfully moved", result);
        var newContent = await File.ReadAllTextAsync(targetFile);
        Assert.DoesNotContain("using Sample.Namespace;", newContent);
    }
}
