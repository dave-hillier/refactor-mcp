using System.IO;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using ModelContextProtocol;
using Xunit;

namespace RefactorMCP.Tests.ToolsNew;

public class RenameSymbolToolTests : RefactorMCP.Tests.TestBase
{
    [Fact]
    public async Task RenameSymbol_Field_RenamesReferences()
    {
        const string initialCode = """
using System.Collections.Generic;
using System.Linq;

public class Sample
{
    private List<int> numbers = new();
    public int Sum() => numbers.Sum();
}
""";

        const string expectedCode = """
using System.Collections.Generic;
using System.Linq;

public class Sample
{
    private List<int> values = new();
    public int Sum() => values.Sum();
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "Rename.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        var result = await RenameSymbolTool.RenameSymbol(
            SolutionPath,
            testFile,
            "numbers",
            "values");

        Assert.Contains("Successfully renamed", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Equal(expectedCode, fileContent.Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task RenameSymbol_InvalidName_ThrowsMcpException()
    {
        const string initialCode = """
using System.Collections.Generic;
using System.Linq;

public class Sample
{
    private List<int> numbers = new();
    public int Sum() => numbers.Sum();
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "RenameInvalid.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        await Assert.ThrowsAsync<McpException>(() =>
            RenameSymbolTool.RenameSymbol(
                SolutionPath,
                testFile,
                "missing",
                "newName"));
    }
}
