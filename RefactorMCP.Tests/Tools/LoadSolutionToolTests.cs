using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using Xunit;

namespace RefactorMCP.Tests.Tools;

public class LoadSolutionToolTests : RefactorMCP.Tests.TestBase
{
    [Fact]
    public async Task LoadSolution_ValidPath_ReturnsSuccess()
    {
        var result = await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        Assert.Contains("Successfully loaded solution", result);
        Assert.Contains("RefactorMCP.ConsoleApp", result);
        Assert.Contains("RefactorMCP.Tests", result);
    }

    [Fact]
    public async Task UnloadSolution_RemovesCachedSolution()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var result = UnloadSolutionTool.UnloadSolution(SolutionPath);
        Assert.Contains("Unloaded solution", result);
    }

    [Fact]
    public async Task LoadSolution_InvalidPath_ReturnsError()
    {
        await Assert.ThrowsAsync<McpException>(async () =>
            await LoadSolutionTool.LoadSolution("./NonExistent.sln", null, CancellationToken.None));
    }

    [Fact]
    public void Version_ReturnsInfo()
    {
        var result = VersionTool.Version();
        Assert.Contains("Version:", result);
        Assert.Contains("Build", result);
    }

    [Fact]
    public async Task ClearSolutionCache_RemovesAllCachedSolutions()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var clearResult = UnloadSolutionTool.ClearSolutionCache();
        Assert.Contains("Cleared all cached solutions", clearResult);

        var unloadResult = UnloadSolutionTool.UnloadSolution(SolutionPath);
        Assert.Contains("was not loaded", unloadResult);
    }
}
