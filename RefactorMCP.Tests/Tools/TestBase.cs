using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RefactorMCP.Tests;

public abstract class TestBase : IDisposable
{
    protected static readonly string SolutionPath = TestUtilities.GetSolutionPath();
    protected static readonly string ExampleFilePath = Path.Combine(Path.GetDirectoryName(SolutionPath)!, "RefactorMCP.Tests", "ExampleCode.cs");
    private static readonly string TestOutputRoot =
        Path.Combine(Path.GetDirectoryName(SolutionPath)!, "RefactorMCP.Tests", "TestOutput");

    protected string TestOutputPath { get; }

    protected TestBase()
    {
        Directory.CreateDirectory(TestOutputRoot);
        TestOutputPath = Path.Combine(TestOutputRoot, Guid.NewGuid().ToString());
        Directory.CreateDirectory(TestOutputPath);
    }

    /// <summary>
    /// Writes a fixture and adds it to the loaded solution, which is the only
    /// place the refactoring tools look for files.
    /// </summary>
    protected static async Task AddToSolutionAsync(string filePath, string code)
    {
        await TestUtilities.CreateTestFile(filePath, code);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        RefactoringHelpers.AddDocumentToProject(solution.Projects.First(), filePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(TestOutputPath))
            Directory.Delete(TestOutputPath, true);
    }
}
