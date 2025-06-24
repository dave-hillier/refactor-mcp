using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using Xunit;

namespace RefactorMCP.Tests.ToolsNew;

public class MoveMultipleMethodsToolToolTests : RefactorMCP.Tests.TestBase
{
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

        var error = await MoveMultipleMethodsTool.MoveMultipleMethods(
            SolutionPath,
            testFile,
            "Calculator",
            new[] { "FormatCurrency", "Wrong" },
            "MathUtilities");
        Assert.Contains("Error:", error);

        var result = await MoveMultipleMethodsTool.MoveMultipleMethods(
            SolutionPath,
            testFile,
            "Calculator",
            new[] { "FormatCurrency", "LogOperation" },
            "MathUtilities");

        Assert.Contains("Successfully moved", result);
    }
}
