using ModelContextProtocol;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class MoveMultipleMethodsToolTests : TestBase
{
    [Fact]
    public async Task MoveMultipleMethods_FailureDoesNotRecordHistory()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.Combine(TestOutputPath, "MoveMultiFailHistory.cs");
        File.Copy(ExampleFilePath, testFile, true);
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
