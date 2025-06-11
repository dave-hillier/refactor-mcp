using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class AnalyzeRefactoringOpportunitiesTests : IDisposable
{
    private static readonly string SolutionPath = GetSolutionPath();
    private static readonly string ExampleFilePath = Path.Combine(Path.GetDirectoryName(SolutionPath)!, "RefactorMCP.Tests", "ExampleCode.cs");
    private readonly string _originalDir = Directory.GetCurrentDirectory();

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDir);
    }

    private static string GetSolutionPath()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var dir = new DirectoryInfo(currentDir);
        while (dir != null)
        {
            var sln = Path.Combine(dir.FullName, "RefactorMCP.sln");
            if (File.Exists(sln)) return sln;
            dir = dir.Parent;
        }
        return "./RefactorMCP.sln";
    }

    [Fact]
    public async Task AnalyzeExampleCode_ReturnsSuggestions()
    {
        await RefactoringTools.LoadSolution(SolutionPath);
        var result = await RefactoringTools.AnalyzeRefactoringOpportunities(ExampleFilePath, SolutionPath);
        Assert.Contains("safe-delete-field", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("safe-delete-method", result, StringComparison.OrdinalIgnoreCase);
    }
}
