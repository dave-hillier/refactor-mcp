using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using Xunit;

namespace RefactorMCP.Tests.Tools;

public class CreateAdapterToolTests : RefactorMCP.Tests.TestBase
{
    private const string LoggerCode = """
public interface ILogger
{
    void Log(string message);
}

public class LegacyLogger
{
    public void Write(string message) { System.Console.WriteLine(message); }
}
""";

    [Fact]
    public async Task CreateAdapter_ImplementsTheInterfaceOverTheClass()
    {
        var testFile = await AddTestFileAsync("Adapter.cs", LoggerCode);

        var result = await CreateAdapterTool.CreateAdapter(SolutionPath, testFile, "LegacyLogger", "ILogger", "LoggerAdapter", "Log:Write");

        Assert.Contains("Created adapter LoggerAdapter", result);
        var adapter = await File.ReadAllTextAsync(Path.Combine(TestOutputPath, "LoggerAdapter.cs"));
        Assert.Contains("public class LoggerAdapter : ILogger", adapter);
        Assert.Contains("public LoggerAdapter(LegacyLogger adaptee)", adapter);
        Assert.Contains("public void Log(string message) => _adaptee.Write(message);", adapter);
    }

    [Fact]
    public async Task CreateAdapter_MappedMemberMissing_Throws()
    {
        var testFile = await AddTestFileAsync("MissingAdapter.cs", LoggerCode);

        var ex = await Assert.ThrowsAsync<McpException>(() =>
            CreateAdapterTool.CreateAdapter(SolutionPath, testFile, "LegacyLogger", "ILogger", "LoggerAdapter", "Log:Print"));
        Assert.Contains("has no member named 'Print'", ex.Message);
    }

    private async Task<string> AddTestFileAsync(string name, string code)
    {
        UnloadSolutionTool.ClearSolutionCache();
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, name);
        await TestUtilities.CreateTestFile(testFile, code);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        RefactoringHelpers.AddDocumentToProject(solution.Projects.First(), testFile);
        return testFile;
    }
}
