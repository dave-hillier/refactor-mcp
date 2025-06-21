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
}
