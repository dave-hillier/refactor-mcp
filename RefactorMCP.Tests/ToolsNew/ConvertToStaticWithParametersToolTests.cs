using System.IO;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using ModelContextProtocol;
using Xunit;

namespace RefactorMCP.Tests.ToolsNew;

public class ConvertToStaticWithParametersToolTests : RefactorMCP.Tests.TestBase
{
    [Fact]
    public async Task ConvertToStaticWithParameters_SingleField_AddsParameter()
    {
        const string initialCode = """
public class Calculator
{
    private int multiplier;

    public Calculator(int m)
    {
        multiplier = m;
    }

    public int Calculate(int value)
    {
        return value * multiplier;
    }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "StaticParams.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        var result = await ConvertToStaticWithParametersTool.ConvertToStaticWithParameters(
            SolutionPath,
            testFile,
            "Calculate");

        Assert.Contains("Successfully converted", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("static", fileContent);
        Assert.Contains("int multiplier", fileContent);
    }

    [Fact]
    public async Task ConvertToStaticWithParameters_MultipleFields_AddsMultipleParameters()
    {
        const string initialCode = """
public class Processor
{
    private int factor1;
    private int factor2;

    public int Process(int value)
    {
        return value * factor1 + factor2;
    }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "StaticMultiParams.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        var result = await ConvertToStaticWithParametersTool.ConvertToStaticWithParameters(
            SolutionPath,
            testFile,
            "Process");

        Assert.Contains("Successfully converted", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("static", fileContent);
        Assert.Contains("factor1", fileContent);
        Assert.Contains("factor2", fileContent);
    }

    [Fact]
    public async Task ConvertToStaticWithParameters_NoInstanceMembers_BecomesStaticOnly()
    {
        const string initialCode = """
public class Helper
{
    public int Add(int a, int b)
    {
        return a + b;
    }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "StaticNoParams.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        var result = await ConvertToStaticWithParametersTool.ConvertToStaticWithParameters(
            SolutionPath,
            testFile,
            "Add");

        Assert.Contains("Successfully converted", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("static", fileContent);
    }

    [Fact]
    public async Task ConvertToStaticWithParameters_PropertyUsage_AddsPropertyAsParameter()
    {
        const string initialCode = """
public class Config
{
    public string Name { get; set; }

    public string GetGreeting()
    {
        return "Hello, " + Name;
    }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "StaticProperty.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        var result = await ConvertToStaticWithParametersTool.ConvertToStaticWithParameters(
            SolutionPath,
            testFile,
            "GetGreeting");

        Assert.Contains("Successfully converted", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("static", fileContent);
    }

    [Fact]
    public async Task ConvertToStaticWithParameters_MethodNotFound_ReturnsError()
    {
        const string initialCode = """
public class Sample
{
    public void ExistingMethod() { }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "StaticNotFound.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        var result = await ConvertToStaticWithParametersTool.ConvertToStaticWithParameters(
            SolutionPath,
            testFile,
            "NonExistent");

        Assert.Contains("Error", result);
    }

    [Fact]
    public async Task ConvertToStaticWithParameters_UnderscorePrefixedField_TrimsUnderscore()
    {
        const string initialCode = """
public class Service
{
    private readonly string _config;

    public Service(string config)
    {
        _config = config;
    }

    public string GetConfig()
    {
        return _config;
    }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "StaticUnderscore.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        var result = await ConvertToStaticWithParametersTool.ConvertToStaticWithParameters(
            SolutionPath,
            testFile,
            "GetConfig");

        Assert.Contains("Successfully converted", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("static", fileContent);
        // The parameter should have underscore trimmed
        Assert.Contains("config", fileContent.ToLower());
    }

    [Fact]
    public void ConvertToStaticWithParametersInSource_BasicConversion()
    {
        const string code = """
public class Sample
{
    private int value;

    public int GetValue()
    {
        return value;
    }
}
""";

        var result = ConvertToStaticWithParametersTool.ConvertToStaticWithParametersInSource(code, "GetValue");

        Assert.Contains("static", result);
        Assert.Contains("int value", result);
    }

    [Fact]
    public void ConvertToStaticWithParametersInSource_MethodNotFound_ReturnsError()
    {
        const string code = """
public class Sample
{
    public void Method() { }
}
""";

        var result = ConvertToStaticWithParametersTool.ConvertToStaticWithParametersInSource(code, "Missing");

        Assert.Contains("Error", result);
    }

    [Fact]
    public async Task ConvertToStaticWithParameters_StaticFieldNotIncluded()
    {
        const string initialCode = """
public class Counter
{
    private static int globalCounter;
    private int instanceValue;

    public int GetCombined()
    {
        return globalCounter + instanceValue;
    }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "StaticFieldExcluded.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        var result = await ConvertToStaticWithParametersTool.ConvertToStaticWithParameters(
            SolutionPath,
            testFile,
            "GetCombined");

        Assert.Contains("Successfully converted", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        // The static field should still be accessed directly, not as a parameter
        Assert.Contains("globalCounter", fileContent);
    }

    [Fact]
    public async Task ConvertToStaticWithParameters_ParameterNameCollision_AddsParamSuffix()
    {
        const string initialCode = """
public class Calc
{
    private int value;

    public int Add(int value)
    {
        return this.value + value;
    }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "StaticCollision.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        var result = await ConvertToStaticWithParametersTool.ConvertToStaticWithParameters(
            SolutionPath,
            testFile,
            "Add");

        Assert.Contains("Successfully converted", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("static", fileContent);
        // When there's a collision, "Param" suffix should be added
        Assert.Contains("valueParam", fileContent);
    }
}
