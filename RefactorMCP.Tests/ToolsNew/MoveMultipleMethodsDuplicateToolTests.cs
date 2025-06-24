using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using Xunit;

namespace RefactorMCP.Tests.ToolsNew;

public class MoveMultipleMethodsDuplicateToolTests : RefactorMCP.Tests.TestBase
{
    [Fact]
    public async Task MoveMultipleMethods_DuplicateNames_Fails()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.Combine(TestOutputPath, "DupMethods.cs");
        var code = @"public class Source { public void A() { } } public class Target { }";
        await TestUtilities.CreateTestFile(testFile, code);
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        var result = await MoveMultipleMethodsTool.MoveMultipleMethods(
            SolutionPath,
            testFile,
            "Source",
            new[] { "A", "A" },
            "Target");

        Assert.Contains("Duplicate method names", result);
    }
}
