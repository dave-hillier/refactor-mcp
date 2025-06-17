using ModelContextProtocol;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class MoveMultipleMethodsBugTests : TestBase
{
    [Fact]
    public async Task MoveMultipleMethods_NestedClassGenerics_Succeeds()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.GetFullPath(Path.Combine(TestOutputPath, "NestedGeneric.cs"));
        var code = @"using System.Collections.Generic;
public class Outer
{
    public class Inner { }
    public List<Inner> MakeList() => new List<Inner>();
    public int CountList(List<Inner> items) => items.Count;
}
public class Target { }";
        await File.WriteAllTextAsync(testFile, code);
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        var result = await MoveMultipleMethodsTool.MoveMultipleMethods(
            SolutionPath,
            testFile,
            "Outer",
            new[] { "MakeList", "CountList" },
            "Target");

        Assert.Contains("Successfully moved", result);
    }
}
