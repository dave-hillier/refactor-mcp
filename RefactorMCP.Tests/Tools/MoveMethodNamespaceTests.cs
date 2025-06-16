using ModelContextProtocol;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class MoveMethodNamespaceTests : TestBase
{
    [Fact]
    public async Task MoveInstanceMethod_PreservesNamespaceInNewFile()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.Combine(TestOutputPath, "NamespaceSample.cs");
        await TestUtilities.CreateTestFile(testFile, "namespace Sample.Namespace { public class A { public void Foo() {} } }");
        await LoadSolutionTool.LoadSolution(SolutionPath);

        var targetFile = Path.Combine(Path.GetDirectoryName(testFile)!, "B.cs");
        var result = await MoveMethodsTool.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "A",
            "Foo",
            "B",
            "_b",
            "field",
            targetFile);

        Assert.Contains("Successfully moved", result);
        var newContent = await File.ReadAllTextAsync(targetFile);
        Assert.Contains("namespace Sample.Namespace", newContent);
    }
}
