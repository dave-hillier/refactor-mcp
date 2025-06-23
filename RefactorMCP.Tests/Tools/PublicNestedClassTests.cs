using ModelContextProtocol;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RefactorMCP.ConsoleApp.Move;
using Xunit;

namespace RefactorMCP.Tests;

public class PublicNestedClassTests : TestBase
{
    [Fact]
    public async Task MoveInstanceMethod_PublicNestedClass_QualifiesReturnType()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.Combine(TestOutputPath, "NestedReturn.cs");
        var code = @"public class A
{
    public class Nested { }

    public Nested GetNested()
    {
        return new Nested();
    }
}

public class B { }";
        await TestUtilities.CreateTestFile(testFile, code);
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var result = await MoveMethodTool.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "A",
            "GetNested",
            "B",
            null,
            Array.Empty<string>(),
            Array.Empty<string>(),
            null,
            CancellationToken.None);

        Assert.Contains("Successfully moved", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("A.Nested GetNested()", fileContent.Replace("\r", "").Replace("\n", " "));
    }
}
