using ModelContextProtocol;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class LoadSolutionTests : TestBase
{
    [Fact]
    public async Task LoadSolution_ValidPath_ReturnsSuccess()
    {
        var result = await RefactoringTools.LoadSolution(SolutionPath);
        Assert.Contains("Successfully loaded solution", result);
        Assert.Contains("RefactorMCP.ConsoleApp", result);
        Assert.Contains("RefactorMCP.Tests", result);
    }

    [Fact]
    public async Task UnloadSolution_RemovesCachedSolution()
    {
        await RefactoringTools.LoadSolution(SolutionPath);
        var result = RefactoringTools.UnloadSolution(SolutionPath);
        Assert.Contains("Unloaded solution", result);
    }

    [Fact]
    public async Task LoadSolution_InvalidPath_ReturnsError()
    {
        await Assert.ThrowsAsync<McpException>(async () =>
            await RefactoringTools.LoadSolution("./NonExistent.sln"));
    }

    [Fact]
    public void Version_ReturnsInfo()
    {
        var result = RefactoringTools.Version();
        Assert.Contains("Version:", result);
        Assert.Contains("Build", result);
    }
}
