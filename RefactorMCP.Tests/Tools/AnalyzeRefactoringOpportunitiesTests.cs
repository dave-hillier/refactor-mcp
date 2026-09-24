using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests.Tools;

public class AnalyzeRefactoringOpportunitiesTests : IDisposable
{
    private static readonly string SolutionPath = TestHelpers.GetSolutionPath();
    private static readonly string ExampleFilePath = Path.Combine(Path.GetDirectoryName(SolutionPath)!, "RefactorMCP.Tests", "ExampleCode.cs");
    private readonly string _originalDir = Directory.GetCurrentDirectory();

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDir);
    }


    [Fact]
    public async Task AnalyzeExampleCode_ReturnsSuggestions()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath);
        var result = await AnalyzeRefactoringOpportunitiesTool.AnalyzeRefactoringOpportunities(SolutionPath, ExampleFilePath);
        Assert.Matches(@"Field '\w+' appears unused -> safe-delete-member", result);
        Assert.Matches(@"Method '\w+' appears unused -> safe-delete-member", result);
        Assert.Contains("make-method-static", result, StringComparison.OrdinalIgnoreCase);
    }
}
