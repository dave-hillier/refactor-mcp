using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class RenameSymbolTests : TestBase
{
    [Fact]
    public async Task RenameSymbol_Field_RenamesAllReferences()
    {
        UnloadSolutionTool.ClearSolutionCache();
        await LoadSolutionTool.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "RenameSymbol.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForRenameSymbol());
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        var result = await RenameSymbolTool.RenameSymbol(
            SolutionPath,
            testFile,
            "numbers",
            "values");

        Assert.Contains("Successfully renamed", result);
        var content = await File.ReadAllTextAsync(testFile);
        Assert.DoesNotContain("List<int> numbers", content);
        Assert.DoesNotContain("numbers.Add", content);
        Assert.Contains("List<int> values", content);
        Assert.Contains("values.Add", content);
    }

    [Fact]
    public async Task RenameSymbol_InvalidName_ReturnsError()
    {
        UnloadSolutionTool.ClearSolutionCache();
        await LoadSolutionTool.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "RenameInvalid.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForRenameSymbol());
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        await Assert.ThrowsAsync<McpException>(() =>
            RenameSymbolTool.RenameSymbol(
                SolutionPath,
                testFile,
                "missing",
                "newName"));
    }
}
