using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
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

    /// <summary>
    /// The reference count comes from SymbolFinder, which reports uses and not
    /// the declaration. A symbol used exactly once therefore has to read as one
    /// reference, not zero.
    /// </summary>
    [Fact]
    public async Task SafeDeleteField_FieldReferencedOnce_IsRefused()
    {
        const string initialCode = """
public class Sample
{
    private int _timeout = 30;

    public int GetTimeout()
    {
        return _timeout;
    }
}
""";

        var testFile = await RegisterTestFile("SafeDeleteReferencedField.cs", initialCode);

        var error = await Assert.ThrowsAsync<McpException>(() =>
            SafeDeleteTool.SafeDeleteField(SolutionPath, testFile, "_timeout"));

        Assert.Contains("referenced", error.Message);
        Assert.Contains("_timeout", await File.ReadAllTextAsync(testFile));
    }

    [Fact]
    public async Task SafeDeleteMethod_MethodCalledOnceThroughThis_IsRefused()
    {
        const string initialCode = """
public class Sample
{
    public void Call()
    {
        this.Helper();
    }

    private void Helper()
    {
    }
}
""";

        var testFile = await RegisterTestFile("SafeDeleteReferencedMethod.cs", initialCode);

        var error = await Assert.ThrowsAsync<McpException>(() =>
            SafeDeleteTool.SafeDeleteMethod(SolutionPath, testFile, "Helper"));

        Assert.Contains("referenced", error.Message);
        Assert.Contains("this.Helper();", await File.ReadAllTextAsync(testFile));
    }

    /// <summary>
    /// Writes a fixture and puts it in the solution, so the call takes the
    /// solution path rather than the single-file fallback.
    /// </summary>
    private async Task<string> RegisterTestFile(string fileName, string code)
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, fileName);
        await TestUtilities.CreateTestFile(testFile, code);

        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        RefactoringHelpers.AddDocumentToProject(solution.Projects.First(), testFile);
        return testFile;
    }
}
