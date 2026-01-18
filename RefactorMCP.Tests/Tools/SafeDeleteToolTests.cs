using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests.Tools;

public class SafeDeleteToolTests : RefactorMCP.Tests.TestBase
{
    [Fact]
    public async Task SafeDeleteField_RemovesUnusedField()
    {
        const string initialCode = """
public class Sample
{
    private int unused;
}
""";

        const string expectedCode = """
public class Sample
{
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "SafeDelete.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        var result = await SafeDeleteTool.SafeDeleteField(
            SolutionPath,
            testFile,
            "unused");

        Assert.Contains("Successfully deleted field", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Equal(expectedCode, fileContent.Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task SafeDeleteMethod_RemovesUnusedMethod()
    {
        const string initialCode = """
public class Sample
{
    private void UnusedHelper()
    {
        int tempValue = 0;
    }
}
""";

        const string expectedCode = """
public class Sample
{
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "SafeDeleteMethod.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        var result = await SafeDeleteTool.SafeDeleteMethod(
            SolutionPath,
            testFile,
            "UnusedHelper");

        Assert.Contains("Successfully deleted method", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Equal(expectedCode, fileContent.Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task SafeDeleteVariable_RemovesUnusedLocal()
    {
        const string initialCode = """
public class Sample
{
    public void DoWork()
    {
        int tempValue = 0;
    }
}
""";

        const string expectedCode = """
public class Sample
{
    public void DoWork()
    {
    }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "SafeDeleteVariable.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        var result = await SafeDeleteTool.SafeDeleteVariable(
            SolutionPath,
            testFile,
            "5:9-5:26");

        Assert.Contains("Successfully deleted variable", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Equal(expectedCode, fileContent.Replace("\r\n", "\n"));
    }
}
