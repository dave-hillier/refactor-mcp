using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using Xunit;

namespace RefactorMCP.Tests.ToolsNew;

public class ConstructorInjectionToolTests : RefactorMCP.Tests.TestBase
{
    [Fact]
    public async Task ConstructorInjection_AddsField()
    {
        const string initialCode = "class C{ int M(int x){ return x+1; } void Call(){ M(1); } }";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "ConstructorInjection.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        var result = await ConstructorInjectionTool.ConvertToConstructorInjection(
            SolutionPath,
            testFile,
            new[] { new ConstructorInjectionTool.MethodParameterPair("M", "x") },
            false);

        Assert.Contains("Successfully injected", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("_x", fileContent);
    }
}
