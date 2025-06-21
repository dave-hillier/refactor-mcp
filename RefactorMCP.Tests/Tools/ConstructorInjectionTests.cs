using ModelContextProtocol;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class ConstructorInjectionTests : TestBase
{
    [Fact]
    public async Task ConstructorInjection_Valid_ReturnsSuccess()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "ConstructorInjection.cs");
        await TestUtilities.CreateTestFile(testFile, "class C{ int M(int x){ return x+1; } void Call(){ M(1); } }");

        var result = await ConstructorInjectionTool.ConvertToConstructorInjection(
            SolutionPath,
            testFile,
            new[] { new ConstructorInjectionTool.MethodParameterPair("M", "x") },
            false);

        Assert.Contains("Successfully injected", result);
        var content = await File.ReadAllTextAsync(testFile);
        Assert.Contains("_x", content);
    }
}
