using ModelContextProtocol;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace RefactorMCP.Tests;

public class MoveInstanceMethodTests : TestBase
{
    [Fact]
    public async Task MoveInstanceMethod_ReturnsSuccess()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.GetFullPath(Path.Combine(TestOutputPath, "MoveInstanceMethod.cs"));
        await TestUtilities.CreateTestFile(testFile, "public class A { public void Do(){} } public class B { }");
        await LoadSolutionTool.LoadSolution(SolutionPath);

        var result = await MoveMethodsTool.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "A",
            "Do",
            "B",
            "field");

        Assert.Contains("Successfully moved", result);
        Assert.Contains("A.Do", result);
        Assert.Contains("B", result);
    }

    [Fact]
    public async Task MoveInstanceMethod_FailsWhenTargetClassIsStatic()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.GetFullPath(Path.Combine(TestOutputPath, "MoveInstanceMethodStatic.cs"));
        await TestUtilities.CreateTestFile(testFile, "public class A { public void Do(){} } public static class B { }");
        await LoadSolutionTool.LoadSolution(SolutionPath);

        await Assert.ThrowsAsync<McpException>(() =>
            MoveMethodsTool.MoveInstanceMethod(
                SolutionPath,
                testFile,
                "A",
                "Do",
                "B",
                "field"));
    }

    [Fact]
    public async Task MoveInstanceMethod_FailsOnSecondMove()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.GetFullPath(Path.Combine(TestOutputPath, "MoveInstanceMethodTwice.cs"));
        await TestUtilities.CreateTestFile(testFile, "public class A { public void Do(){} } public class B { }");
        await LoadSolutionTool.LoadSolution(SolutionPath);

        var result = await MoveMethodsTool.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "A",
            "Do",
            "B",
            "field");
        Assert.Contains("Successfully moved", result);

        await Assert.ThrowsAsync<McpException>(() =>
            MoveMethodsTool.MoveInstanceMethod(
                SolutionPath,
                testFile,
                "A",
                "Do",
                "B",
                "field"));
    }
}
