using ModelContextProtocol;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorMCP.ConsoleApp.Move;
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
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var result = await MoveMethodTool.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "A",
            "Do",
            "B",
            null,
            null,
            CancellationToken.None);

        Assert.Contains("Successfully moved", result);
        Assert.Contains("A.Do", result);
        Assert.Contains("B", result);
        Assert.Contains("made static", result);

        var newContent = await File.ReadAllTextAsync(testFile);
        var tree = CSharpSyntaxTree.ParseText(newContent);
        var root = await tree.GetRootAsync();
        var bClass = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == "B");
        var method = bClass.Members.OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.ValueText == "Do");
        Assert.True(method.Modifiers.Any(SyntaxKind.StaticKeyword));
    }

    [Fact]
    public async Task MoveInstanceMethod_AllowsStaticTargetWhenNoDependencies()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.GetFullPath(Path.Combine(TestOutputPath, "MoveInstanceMethodStatic.cs"));
        await TestUtilities.CreateTestFile(testFile, "public class A { public void Do(){} } public static class B { }");
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var result = await MoveMethodTool.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "A",
            "Do",
            "B",
            null,
            null,
            CancellationToken.None);

        Assert.Contains("Successfully moved", result);
    }

    [Fact]
    public async Task MoveInstanceMethod_FailsWhenMethodIsProtectedOverride()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.GetFullPath(Path.Combine(TestOutputPath, "MoveInstanceProtectedOverride.cs"));
        await TestUtilities.CreateTestFile(testFile, @"public class Base { protected virtual void Do(){} } public class A : Base { protected override void Do(){} } public class B { }");
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        await Assert.ThrowsAsync<McpException>(() =>
            MoveMethodTool.MoveInstanceMethod(
                SolutionPath,
                testFile,
                "A",
                "Do",
                "B",
                null,
                null,
                CancellationToken.None));
    }

    [Fact]
    public async Task MoveInstanceMethod_FailsOnSecondMove()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.GetFullPath(Path.Combine(TestOutputPath, "MoveInstanceMethodTwice.cs"));
        await TestUtilities.CreateTestFile(testFile, "public class A { public void Do(){} } public class B { }");
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var result = await MoveMethodTool.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "A",
            "Do",
            "B",
            null,
            null,
            CancellationToken.None);
        Assert.Contains("Successfully moved", result);

        await Assert.ThrowsAsync<McpException>(() =>
            MoveMethodTool.MoveInstanceMethod(
                SolutionPath,
                testFile,
                "A",
                "Do",
                "B",
                null,
                null,
                CancellationToken.None));
    }

    [Fact]
    public async Task ResetMoveHistory_AllowsRepeatMove()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.GetFullPath(Path.Combine(TestOutputPath, "ResetMoveHistory.cs"));
        await TestUtilities.CreateTestFile(testFile, "public class A { public void Do(){} } public class B { }");
        await LoadSolutionTool.LoadSolution(SolutionPath);

        var result1 = await MoveMethodTool.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "A",
            "Do",
            "B");
        Assert.Contains("Successfully moved", result1);

        // Clear move tracking and try again
        MoveMethodTool.ResetMoveHistory();

        var result2 = await MoveMethodTool.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "A",
            "Do",
            "B");

        Assert.Contains("Successfully moved", result2);
    }

    [Fact]
    public async Task LoadSolution_ResetsMoveHistory()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.GetFullPath(Path.Combine(TestOutputPath, "LoadSolutionReset.cs"));
        await TestUtilities.CreateTestFile(testFile, "public class A { public void Do(){} } public class B { }");
        await LoadSolutionTool.LoadSolution(SolutionPath);

        var result1 = await MoveMethodTool.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "A",
            "Do",
            "B");
        Assert.Contains("Successfully moved", result1);

        await LoadSolutionTool.LoadSolution(SolutionPath);

        var result2 = await MoveMethodTool.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "A",
            "Do",
            "B");

        Assert.Contains("Successfully moved", result2);
    }

    [Fact]
    public async Task MoveInstanceMethod_FailureDoesNotRecordHistory()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.GetFullPath(Path.Combine(TestOutputPath, "MoveFailHistory.cs"));
        await TestUtilities.CreateTestFile(testFile, "public class A { public void Do(){} } public class B { }");
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        await Assert.ThrowsAsync<McpException>(() =>
            MoveMethodTool.MoveInstanceMethod(
                SolutionPath,
                testFile,
                "Wrong",
                "Do",
                "B",
                null,
                null,
                CancellationToken.None));

        var result = await MoveMethodTool.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "A",
            "Do",
            "B",
            null,
            null,
            CancellationToken.None);

        Assert.Contains("Successfully moved", result);
    }
}
