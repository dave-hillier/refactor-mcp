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

    [Fact]
    public async Task RenameSymbol_Class_RenamesClassAndConstructor()
    {
        const string initialCode = """
public class OldName
{
    public OldName() { }
    public void DoWork() { }
}

public class Consumer
{
    public void Use()
    {
        var instance = new OldName();
    }
}
""";

        const string expectedCode = """
public class NewName
{
    public NewName() { }
    public void DoWork() { }
}

public class Consumer
{
    public void Use()
    {
        var instance = new NewName();
    }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "RenameClass.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        var result = await RenameSymbolTool.RenameSymbol(
            SolutionPath,
            testFile,
            "OldName",
            "NewName");

        Assert.Contains("Successfully renamed", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Equal(expectedCode, fileContent.Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task RenameSymbol_Method_RenamesMethodAndCalls()
    {
        const string initialCode = """
public class Sample
{
    public void OldMethod() { }

    public void Caller()
    {
        OldMethod();
        this.OldMethod();
    }
}
""";

        const string expectedCode = """
public class Sample
{
    public void NewMethod() { }

    public void Caller()
    {
        NewMethod();
        this.NewMethod();
    }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "RenameMethod.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        var result = await RenameSymbolTool.RenameSymbol(
            SolutionPath,
            testFile,
            "OldMethod",
            "NewMethod");

        Assert.Contains("Successfully renamed", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Equal(expectedCode, fileContent.Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task RenameSymbol_Property_RenamesPropertyAndReferences()
    {
        const string initialCode = """
public class Sample
{
    public string OldProperty { get; set; }

    public void Use()
    {
        OldProperty = "test";
        var x = OldProperty;
    }
}
""";

        const string expectedCode = """
public class Sample
{
    public string NewProperty { get; set; }

    public void Use()
    {
        NewProperty = "test";
        var x = NewProperty;
    }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "RenameProperty.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        var result = await RenameSymbolTool.RenameSymbol(
            SolutionPath,
            testFile,
            "OldProperty",
            "NewProperty");

        Assert.Contains("Successfully renamed", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Equal(expectedCode, fileContent.Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task RenameSymbol_Parameter_RenamesParameterAndUsages()
    {
        const string initialCode = """
public class Sample
{
    public int Calculate(int oldParam)
    {
        return oldParam * 2;
    }
}
""";

        const string expectedCode = """
public class Sample
{
    public int Calculate(int newParam)
    {
        return newParam * 2;
    }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "RenameParameter.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        var result = await RenameSymbolTool.RenameSymbol(
            SolutionPath,
            testFile,
            "oldParam",
            "newParam");

        Assert.Contains("Successfully renamed", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Equal(expectedCode, fileContent.Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task RenameSymbol_LocalVariable_RenamesVariableAndUsages()
    {
        const string initialCode = """
public class Sample
{
    public void Method()
    {
        var oldVar = 10;
        var result = oldVar + 5;
    }
}
""";

        const string expectedCode = """
public class Sample
{
    public void Method()
    {
        var newVar = 10;
        var result = newVar + 5;
    }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "RenameLocalVar.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        var result = await RenameSymbolTool.RenameSymbol(
            SolutionPath,
            testFile,
            "oldVar",
            "newVar");

        Assert.Contains("Successfully renamed", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Equal(expectedCode, fileContent.Replace("\r\n", "\n"));
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
    public async Task RenameSymbol_WithLineAndColumn_RenamesSpecificSymbol()
    {
        const string initialCode = """
public class Sample
{
    private int value;
    public int Value => value;
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "RenameWithPosition.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        // Line 3, column 17 should be the field 'value'
        var result = await RenameSymbolTool.RenameSymbol(
            SolutionPath,
            testFile,
            "value",
            "internalValue",
            line: 3,
            column: 17);

        Assert.Contains("Successfully renamed", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("internalValue", fileContent);
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
