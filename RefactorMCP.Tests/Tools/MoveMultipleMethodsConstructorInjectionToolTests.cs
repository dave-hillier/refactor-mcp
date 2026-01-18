using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using Xunit;

namespace RefactorMCP.Tests.Tools;

public class MoveMultipleMethodsConstructorInjectionToolTests : RefactorMCP.Tests.TestBase
{
    [Fact]
    public async Task MoveMultipleMethods_ConstructorInjection_UsesThis()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.Combine(TestOutputPath, "MultiCtor.cs");
        var code = "public class cA{ public int Value=>1; public int Get(){ return Value; } public int Add(int x){ return x + Value; } } public class B{ }";
        await TestUtilities.CreateTestFile(testFile, code);
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        var result = await MoveMultipleMethodsTool.MoveMultipleMethodsInstance(
            SolutionPath,
            testFile,
            "cA",
            new[] { "Get", "Add" },
            "B");

        Assert.Contains("Successfully moved", result);
        var targetPath = Path.Combine(Path.GetDirectoryName(testFile)!, "B.cs");
        var content = await File.ReadAllTextAsync(targetPath);
        Assert.DoesNotContain("_a", content);
    }

    [Fact]
    public async Task MoveMultipleMethods_ParameterInjection_AddsParameter()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.Combine(TestOutputPath, "MultiParam.cs");
        var code = "public class cA{ public int Value=>1; public int Get(){ return Value; } public int Add(int x){ return x + Value; } } public class B{ }";
        await TestUtilities.CreateTestFile(testFile, code);
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        var result = await MoveMultipleMethodsTool.MoveMultipleMethodsStatic(
            SolutionPath,
            testFile,
            "cA",
            new[] { "Get", "Add" },
            "B");

        Assert.Contains("Successfully moved", result);
        var targetPath = Path.Combine(Path.GetDirectoryName(testFile)!, "B.cs");
        var content = await File.ReadAllTextAsync(targetPath);
        Assert.Contains("Get(cA", content);
        Assert.Contains("Add(cA", content);
    }
}
