using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RefactorMCP.ConsoleApp.Tools;
using Xunit;

namespace RefactorMCP.Tests.Examples;

/// <summary>
/// Tests that verify the refactoring examples actually work.
/// These tests use the source files in Examples/SourceFiles/Before/ and run actual refactorings.
/// </summary>
public class ExampleVerificationTests : TestBase
{
    private static readonly string ExamplesRoot = Path.Combine(
        Path.GetDirectoryName(TestUtilities.GetSolutionPath())!,
        "Examples", "SourceFiles", "Before");

    [Fact]
    public async Task ExtractMethodExample_Compiles()
    {
        var sourceFile = Path.Combine(ExamplesRoot, "ExtractMethodExample.cs");
        Assert.True(File.Exists(sourceFile), $"Example file not found: {sourceFile}");

        var code = await File.ReadAllTextAsync(sourceFile);
        var compilation = CreateCompilation(code);
        var diagnostics = compilation.GetDiagnostics();

        // Should compile without errors (warnings are OK)
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public async Task ExtractMethodExample_RefactoringWorks()
    {
        var sourceFile = Path.Combine(ExamplesRoot, "ExtractMethodExample.cs");
        var code = await File.ReadAllTextAsync(sourceFile);

        // Copy to test output directory
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "ExtractMethodExample.cs");
        await File.WriteAllTextAsync(testFile, code);

        // Extract the validation block (lines 26-49 in the original)
        var result = await ExtractMethodTool.ExtractMethod(
            SolutionPath,
            testFile,
            "26:9-49:10",  // The validation block
            "ValidateOrderAsync");

        Assert.Contains("Successfully extracted method", result);

        // Verify the result still compiles
        var refactoredCode = await File.ReadAllTextAsync(testFile);
        var compilation = CreateCompilation(refactoredCode);
        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.Empty(errors);

        // Verify the new method was created
        Assert.Contains("ValidateOrderAsync", refactoredCode);
    }

    [Fact]
    public async Task IntroduceVariableExample_Compiles()
    {
        var sourceFile = Path.Combine(ExamplesRoot, "IntroduceVariableExample.cs");
        Assert.True(File.Exists(sourceFile), $"Example file not found: {sourceFile}");

        var code = await File.ReadAllTextAsync(sourceFile);
        var compilation = CreateCompilation(code);
        var diagnostics = compilation.GetDiagnostics();

        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public async Task IntroduceVariableExample_RefactoringWorks()
    {
        var sourceFile = Path.Combine(ExamplesRoot, "IntroduceVariableExample.cs");
        var code = await File.ReadAllTextAsync(sourceFile);

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "IntroduceVariableExample.cs");
        await File.WriteAllTextAsync(testFile, code);

        // Introduce variable for the filtered transactions expression (using range format)
        var result = await IntroduceVariableTool.IntroduceVariable(
            SolutionPath,
            testFile,
            "19:30-19:97",  // The .Where expression
            "monthlyTransactions");

        Assert.Contains("Successfully introduced variable", result);

        var refactoredCode = await File.ReadAllTextAsync(testFile);
        Assert.Contains("monthlyTransactions", refactoredCode);
    }

    [Fact]
    public async Task MoveInstanceMethodExample_Compiles()
    {
        var sourceFile = Path.Combine(ExamplesRoot, "MoveInstanceMethodExample.cs");
        Assert.True(File.Exists(sourceFile), $"Example file not found: {sourceFile}");

        var code = await File.ReadAllTextAsync(sourceFile);
        var compilation = CreateCompilation(code);
        var diagnostics = compilation.GetDiagnostics();

        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public async Task MoveInstanceMethodExample_RefactoringWorks()
    {
        var sourceFile = Path.Combine(ExamplesRoot, "MoveInstanceMethodExample.cs");
        var code = await File.ReadAllTextAsync(sourceFile);

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "MoveInstanceMethodExample.cs");
        await File.WriteAllTextAsync(testFile, code);

        var targetFile = Path.Combine(TestOutputPath, "PricingCalculator.cs");

        // Move the CalculateSubtotal method
        var result = await MoveMethodTool.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "OrderService",
            new[] { "CalculateSubtotal" },
            "PricingCalculator",
            targetFile,
            Array.Empty<string>(),
            Array.Empty<string>());

        Assert.Contains("Successfully moved", result);

        // Verify target file was created
        Assert.True(File.Exists(targetFile));
        var targetCode = await File.ReadAllTextAsync(targetFile);
        Assert.Contains("class PricingCalculator", targetCode);
        Assert.Contains("CalculateSubtotal", targetCode);
    }

    [Fact]
    public async Task ConvertToStaticExample_Compiles()
    {
        var sourceFile = Path.Combine(ExamplesRoot, "ConvertToStaticExample.cs");
        Assert.True(File.Exists(sourceFile), $"Example file not found: {sourceFile}");

        var code = await File.ReadAllTextAsync(sourceFile);
        var compilation = CreateCompilation(code);
        var diagnostics = compilation.GetDiagnostics();

        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public async Task ConvertToStaticExample_RefactoringWorks()
    {
        var sourceFile = Path.Combine(ExamplesRoot, "ConvertToStaticExample.cs");
        var code = await File.ReadAllTextAsync(sourceFile);

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "ConvertToStaticExample.cs");
        await File.WriteAllTextAsync(testFile, code);

        var result = await ConvertToStaticWithInstanceTool.ConvertToStaticWithInstance(
            SolutionPath,
            testFile,
            "GenerateSummary");

        Assert.Contains("Successfully converted", result);

        var refactoredCode = await File.ReadAllTextAsync(testFile);
        Assert.Contains("static", refactoredCode);
    }

    [Fact]
    public async Task ExtractInterfaceExample_Compiles()
    {
        var sourceFile = Path.Combine(ExamplesRoot, "ExtractInterfaceExample.cs");
        Assert.True(File.Exists(sourceFile), $"Example file not found: {sourceFile}");

        var code = await File.ReadAllTextAsync(sourceFile);
        var compilation = CreateCompilation(code);
        var diagnostics = compilation.GetDiagnostics();

        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public async Task ExtractInterfaceExample_RefactoringWorks()
    {
        var sourceFile = Path.Combine(ExamplesRoot, "ExtractInterfaceExample.cs");
        var code = await File.ReadAllTextAsync(sourceFile);

        UnloadSolutionTool.ClearSolutionCache();
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "ExtractInterfaceExample.cs");
        await File.WriteAllTextAsync(testFile, code);

        // Register the test file in the solution
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        var interfaceFile = Path.Combine(TestOutputPath, "IWeatherService.cs");

        var result = await ExtractInterfaceTool.ExtractInterface(
            SolutionPath,
            testFile,
            "WeatherService",
            "GetCurrentWeatherAsync,GetForecastAsync,GetAlertsAsync",
            interfaceFile);

        Assert.Contains("Successfully extracted interface", result);

        // Verify interface file was created
        Assert.True(File.Exists(interfaceFile));
        var interfaceCode = await File.ReadAllTextAsync(interfaceFile);
        Assert.Contains("interface IWeatherService", interfaceCode);
        Assert.Contains("GetCurrentWeatherAsync", interfaceCode);

        // Verify the original class now implements the interface
        var refactoredCode = await File.ReadAllTextAsync(testFile);
        Assert.Contains(": IWeatherService", refactoredCode);
    }

    [Fact]
    public async Task SafeDeleteExample_Compiles()
    {
        var sourceFile = Path.Combine(ExamplesRoot, "SafeDeleteExample.cs");
        Assert.True(File.Exists(sourceFile), $"Example file not found: {sourceFile}");

        var code = await File.ReadAllTextAsync(sourceFile);
        var compilation = CreateCompilation(code);
        var diagnostics = compilation.GetDiagnostics();

        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public async Task SafeDeleteExample_UnusedMethodDeleted()
    {
        var sourceFile = Path.Combine(ExamplesRoot, "SafeDeleteExample.cs");
        var code = await File.ReadAllTextAsync(sourceFile);

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "SafeDeleteExample.cs");
        await File.WriteAllTextAsync(testFile, code);

        var result = await SafeDeleteTool.SafeDeleteMethod(
            SolutionPath,
            testFile,
            "FormatUserLegacy");

        Assert.Contains("Successfully deleted", result);

        var refactoredCode = await File.ReadAllTextAsync(testFile);
        Assert.DoesNotContain("FormatUserLegacy", refactoredCode);

        var compilation = CreateCompilation(refactoredCode);
        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.Empty(errors);
    }

    [Fact]
    public async Task ConstructorInjectionExample_Compiles()
    {
        var sourceFile = Path.Combine(ExamplesRoot, "ConstructorInjectionExample.cs");
        Assert.True(File.Exists(sourceFile), $"Example file not found: {sourceFile}");

        var code = await File.ReadAllTextAsync(sourceFile);
        var compilation = CreateCompilation(code);
        var diagnostics = compilation.GetDiagnostics();

        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.Empty(errors);
    }

    private static Compilation CreateCompilation(string code)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code);

        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
            MetadataReference.CreateFromFile(
                Path.Combine(
                    Path.GetDirectoryName(typeof(object).Assembly.Location)!,
                    "System.Runtime.dll")),
            MetadataReference.CreateFromFile(
                Path.Combine(
                    Path.GetDirectoryName(typeof(object).Assembly.Location)!,
                    "System.Collections.dll"))
        };

        return CSharpCompilation.Create(
            "TestCompilation",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
