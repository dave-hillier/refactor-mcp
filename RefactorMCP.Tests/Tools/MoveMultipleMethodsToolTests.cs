using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using Xunit;

namespace RefactorMCP.Tests.Tools;

public class MoveMultipleMethodsToolTests : RefactorMCP.Tests.TestBase
{
    [Fact]
    public async Task MoveMultipleMethods_BasicMethods_Success()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.Combine(TestOutputPath, "MoveMultipleBasic.cs");
        await TestUtilities.CreateTestFile(testFile, File.ReadAllText(ExampleFilePath));
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        var result = await MoveMultipleMethodsTool.MoveMultipleMethodsStatic(
            SolutionPath,
            testFile,
            "Calculator",
            new[] { "FormatCurrency", "LogOperation" },
            "MathUtilities");

        Assert.Contains("Successfully moved", result);
    }

    [Fact]
    public async Task MoveMultipleMethods_NestedClassGenerics_Success()
    {
        const string initialCode = """
using System.Collections.Generic;

public class Outer
{
    public class Inner { }
    
    public List<Inner> MakeList() => new List<Inner>();
    
    public int CountList(List<Inner> items) => items.Count;
}

public class Target { }
""";

        UnloadSolutionTool.ClearSolutionCache();
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "NestedGenerics.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);
        
        var result = await MoveMultipleMethodsTool.MoveMultipleMethodsStatic(
            SolutionPath,
            testFile,
            "Outer",
            new[] { "MakeList", "CountList" },
            "Target");

        Assert.Contains("Successfully moved", result);
    }

    [Fact]
    public async Task MoveMultipleMethods_FailureDoesNotRecordHistory()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.Combine(TestOutputPath, "MoveMultiFailHistory.cs");
        await TestUtilities.CreateTestFile(testFile, File.ReadAllText(ExampleFilePath));
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        var error = await MoveMultipleMethodsTool.MoveMultipleMethodsStatic(
            SolutionPath,
            testFile,
            "Calculator",
            new[] { "FormatCurrency", "Wrong" },
            "MathUtilities");
        Assert.Contains("Error:", error);

        var result = await MoveMultipleMethodsTool.MoveMultipleMethodsStatic(
            SolutionPath,
            testFile,
            "Calculator",
            new[] { "FormatCurrency", "LogOperation" },
            "MathUtilities");

        Assert.Contains("Successfully moved", result);
    }
}