using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using Xunit;

namespace RefactorMCP.Tests.Tools;

public class ExtractDecoratorToolTests : RefactorMCP.Tests.TestBase
{
    [Fact]
    public async Task ExtractDecorator_CreatesDecoratorImplementingTheInterface()
    {
        const string initialCode = """
public interface IGreeter
{
    void Greet(string name);
}

public class Greeter : IGreeter
{
    public void Greet(string name) { System.Console.WriteLine("Hello " + name); }
}
""";

        var testFile = await AddTestFileAsync("Decorator.cs", initialCode);

        var result = await ExtractDecoratorTool.ExtractDecorator(SolutionPath, testFile, "Greeter");

        Assert.Contains("Created decorator GreeterDecorator", result);
        var decorator = await File.ReadAllTextAsync(Path.Combine(TestOutputPath, "GreeterDecorator.cs"));
        Assert.Contains("public class GreeterDecorator : IGreeter", decorator);
        Assert.Contains("public GreeterDecorator(IGreeter inner)", decorator);
        Assert.Contains("public void Greet(string name) => _inner.Greet(name);", decorator);
    }

    [Fact]
    public async Task ExtractDecorator_ClassWithoutInterface_Throws()
    {
        const string initialCode = "public class Plain { public void Run() { } }";

        var testFile = await AddTestFileAsync("PlainDecorator.cs", initialCode);

        var ex = await Assert.ThrowsAsync<McpException>(() => ExtractDecoratorTool.ExtractDecorator(SolutionPath, testFile, "Plain"));
        Assert.Contains("implements no interface", ex.Message);
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
