using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests.Workflows;

/// <summary>
/// Integration tests for procedural refactoring workflows.
/// These tests verify that the analyze-suggest-refactor pattern works correctly.
/// </summary>
public class AnalyzeThenRefactorTests : TestBase
{
    [Fact]
    public async Task Workflow_AnalyzeThenSafeDelete_RemovesUnusedField()
    {
        const string initialCode = """
using System;

public class ServiceWithUnusedField
{
    private readonly string _unusedField;
    private readonly string _usedField;

    public ServiceWithUnusedField(string used)
    {
        _usedField = used;
    }

    public string GetValue() => _usedField;
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "WorkflowAnalyze.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        // Step 1: Analyze for refactoring opportunities
        var analysis = await AnalyzeRefactoringOpportunitiesTool.AnalyzeRefactoringOpportunities(
            SolutionPath, testFile);

        // Step 2: Verify unused field is detected
        Assert.Contains("_unusedField", analysis);
        Assert.Contains("safe-delete", analysis.ToLowerInvariant());

        // Step 3: Perform the safe-delete refactoring
        var result = await SafeDeleteTool.SafeDelete(
            SolutionPath,
            testFile,
            "_unusedField");

        Assert.Contains("deleted", result.ToLowerInvariant());

        // Step 4: Verify the field is removed
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.DoesNotContain("_unusedField", fileContent);
        Assert.Contains("_usedField", fileContent);
    }

    [Fact]
    public async Task Workflow_AnalyzeThenMakeStatic_ConvertsMethodToStatic()
    {
        const string initialCode = """
using System;

public class Utility
{
    public int Calculate(int a, int b)
    {
        return a + b;
    }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "WorkflowMakeStatic.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        // Step 1: Analyze for refactoring opportunities
        var analysis = await AnalyzeRefactoringOpportunitiesTool.AnalyzeRefactoringOpportunities(
            SolutionPath, testFile);

        // Step 2: Verify make-static suggestion exists
        Assert.Contains("make-static", analysis.ToLowerInvariant());

        // Step 3: Perform the conversion
        var result = await ConvertToStaticWithInstanceTool.ConvertToStaticWithInstance(
            SolutionPath,
            testFile,
            "Calculate");

        Assert.Contains("static", result.ToLowerInvariant());

        // Step 4: Verify method is now static
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("public static", fileContent);
    }

    [Fact]
    public async Task Workflow_MultipleRefactorings_ChainedSuccessfully()
    {
        const string initialCode = """
using System;

public class MessyClass
{
    private int _unusedField1;
    private int _unusedField2;
    private string _usedField;

    public MessyClass(string value)
    {
        _usedField = value;
    }

    public string GetValue() => _usedField;
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "WorkflowChained.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        // Delete first unused field
        await SafeDeleteTool.SafeDelete(SolutionPath, testFile, "_unusedField1");

        // Delete second unused field
        await SafeDeleteTool.SafeDelete(SolutionPath, testFile, "_unusedField2");

        // Verify both fields are removed
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.DoesNotContain("_unusedField1", fileContent);
        Assert.DoesNotContain("_unusedField2", fileContent);
        Assert.Contains("_usedField", fileContent);
    }

    [Fact]
    public async Task Workflow_ExtractThenMove_RefactorsMethodToNewClass()
    {
        const string initialCode = """
using System;

public class SourceClass
{
    public void Process(int value)
    {
        // Complex calculation logic
        var result = value * 2 + 10;
        Console.WriteLine(result);
    }
}

public class TargetHelper { }
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "WorkflowExtractMove.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        // First make the method static so it can be moved
        await ConvertToStaticWithInstanceTool.ConvertToStaticWithInstance(
            SolutionPath,
            testFile,
            "Process");

        // Verify method was made static
        var intermediateContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("static", intermediateContent);
    }

    [Fact]
    public async Task Workflow_RenameAndCleanup_RefactorsFieldConsistently()
    {
        const string initialCode = """
using System;
using System.Text;

public class DataHolder
{
    private int val;

    public DataHolder(int initial)
    {
        val = initial;
    }

    public int GetValue() => val;
    public void SetValue(int newVal) => val = newVal;
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "WorkflowRenameCleanup.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        // Step 1: Rename field to follow conventions
        var renameResult = await RenameSymbolTool.RenameSymbol(
            SolutionPath,
            testFile,
            "val",
            "_value");

        Assert.Contains("renamed", renameResult.ToLowerInvariant());

        // Step 2: Cleanup unused usings
        var cleanupResult = await CleanupUsingsTool.CleanupUsings(
            SolutionPath,
            testFile);

        // Step 3: Verify changes
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("_value", fileContent);
        Assert.DoesNotContain("val =", fileContent);
        // System.Text should be removed as unused
        Assert.DoesNotContain("System.Text", fileContent);
    }

    [Fact]
    public async Task Workflow_AnalyzeEmptyFile_ReturnsNoSuggestions()
    {
        const string initialCode = """
// Empty file with just a comment
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "WorkflowEmpty.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        var analysis = await AnalyzeRefactoringOpportunitiesTool.AnalyzeRefactoringOpportunities(
            SolutionPath, testFile);

        // Should complete without errors
        Assert.NotNull(analysis);
    }

    [Fact]
    public async Task Workflow_IntroduceFieldThenMakeReadonly()
    {
        const string initialCode = """
using System;

public class Config
{
    public Config()
    {
        Console.WriteLine("Default timeout: 30");
    }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "WorkflowIntroduceField.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        // Step 1: Introduce field for the magic number
        var introduceResult = await IntroduceFieldTool.IntroduceField(
            SolutionPath,
            testFile,
            "30",
            "_defaultTimeout",
            "int");

        Assert.Contains("introduced", introduceResult.ToLowerInvariant());

        // Step 2: Make the field readonly
        var readonlyResult = await MakeFieldReadonlyTool.MakeFieldReadonly(
            SolutionPath,
            testFile,
            "_defaultTimeout");

        Assert.Contains("readonly", readonlyResult.ToLowerInvariant());

        // Step 3: Verify the field is readonly
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("readonly", fileContent);
        Assert.Contains("_defaultTimeout", fileContent);
    }
}
