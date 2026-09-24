using System.IO;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using ModelContextProtocol;
using Xunit;

namespace RefactorMCP.Tests.Tools;

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

    [Fact]
    public async Task RenameSymbol_AmbiguousLocalVariable_ThrowsMcpException()
    {
        const string initialCode = """
public class Sample
{
    public int First()
    {
        var shared = 10;
        return shared;
    }

    public int Second()
    {
        var shared = 20;
        return shared;
    }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "RenameAmbiguousLocal.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            RenameSymbolTool.RenameSymbol(
                SolutionPath,
                testFile,
                "shared",
                "renamed"));

        Assert.Contains("Multiple", exception.Message);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("shared", fileContent);
    }

    [Fact]
    public async Task RenameSymbol_Interface_RenamesInterfaceAndImplementations()
    {
        const string initialCode = """
public interface IOldInterface
{
    void DoWork();
}

public class Implementation : IOldInterface
{
    public void DoWork() { }
}
""";

        const string expectedCode = """
public interface INewInterface
{
    void DoWork();
}

public class Implementation : INewInterface
{
    public void DoWork() { }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "RenameInterface.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        var result = await RenameSymbolTool.RenameSymbol(
            SolutionPath,
            testFile,
            "IOldInterface",
            "INewInterface");

        Assert.Contains("Successfully renamed", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Equal(expectedCode, fileContent.Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task RenameSymbol_Enum_RenamesEnumAndUsages()
    {
        const string initialCode = """
public enum OldStatus
{
    Active,
    Inactive
}

public class Sample
{
    public OldStatus Status { get; set; }
}
""";

        const string expectedCode = """
public enum NewStatus
{
    Active,
    Inactive
}

public class Sample
{
    public NewStatus Status { get; set; }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "RenameEnum.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        var result = await RenameSymbolTool.RenameSymbol(
            SolutionPath,
            testFile,
            "OldStatus",
            "NewStatus");

        Assert.Contains("Successfully renamed", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Equal(expectedCode, fileContent.Replace("\r\n", "\n"));
    }
}
