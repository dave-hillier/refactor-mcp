using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using Xunit;

namespace RefactorMCP.Tests.ToolsNew;

public class AddObserverToolTests : RefactorMCP.Tests.TestBase
{
    [Fact]
    public async Task AddObserver_AddsEventAndInvocation()
    {
        const string initialCode = """
public class Counter
{
    public void Update() { }
}
""";

        const string expectedCode = """
public class Counter
{
    public event System.Action? Updated;

    public void Update() { }

    protected void OnUpdated()
    {
        Updated?.Invoke();
    }
}
""";


        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "Observer.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        var result = await AddObserverTool.AddObserver(
            SolutionPath,
            testFile,
            "Counter",
            "Update",
            "Updated");

        Assert.Contains("Added observer", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("public event", fileContent);
        Assert.Contains("Updated?.Invoke()", fileContent);
    }

    [Fact]
    public async Task AddObserver_InvalidClassName_ThrowsMcpException()
    {
        const string initialCode = """
public class Counter
{
    public void Update() { }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "Observer.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

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
        const string initialCode = """
public class Counter
{
    public void Update() { }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "Observer.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        McpException ex = await Assert.ThrowsAsync<McpException>(() => AddObserverTool.AddObserver(
            SolutionPath,
            testFile,
            "Counter",
            "WrongMethod",
            "Updated"));

        Assert.Equal("Error adding observer: Error: Method 'WrongMethod' not found", ex.Message);
    }
}
