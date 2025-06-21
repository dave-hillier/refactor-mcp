using ModelContextProtocol;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class ExtractDecoratorTests : TestBase
{
    [Fact]
    public async Task ExtractDecorator_AddsClass()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "Decorator.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForDecorator());

        var result = await ExtractDecoratorTool.ExtractDecorator(
            SolutionPath,
            testFile,
            "Greeter",
            "Greet");

        Assert.Contains("Created decorator", result);
        var text = await File.ReadAllTextAsync(testFile);
        Assert.Contains("GreeterDecorator", text);
    }
}
