using ModelContextProtocol;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class AddObserverTests : TestBase
{
    [Fact]
    public async Task AddObserver_AddsEvent()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "Observer.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForObserver());

        var result = await AddObserverTool.AddObserver(
            SolutionPath,
            testFile,
            "Counter",
            "Update",
            "Updated");

        Assert.Contains("Added observer", result);
        var text = await File.ReadAllTextAsync(testFile);
        Assert.Contains("event", text);
        Assert.Contains("Updated?.Invoke", text);
    }

    [Fact]
    public async Task AddObserver_InvalidClassName_ThrowsMcpException()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "Observer.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForObserver());

        McpException ex = await Assert.ThrowsAsync<McpException>(() => AddObserverTool.AddObserver(
            SolutionPath,
            testFile,
            "WrongClass",
            "Update",
            "Updated"));

        Assert.Equal("Error adding observer: Error: Class 'WrongClass' not found", ex.Message);
    }

    [Fact]
    public async Task AddObserver_InvalidMethodName_ThrowsMcpException()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "Observer.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForObserver());

        McpException ex = await Assert.ThrowsAsync<McpException>(() => AddObserverTool.AddObserver(
            SolutionPath,
            testFile,
            "Counter",
            "WrongMethod",
            "Updated"));

        Assert.Equal("Error adding observer: Error: Method 'WrongMethod' not found", ex.Message);
    }
}
