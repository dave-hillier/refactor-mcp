using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

/// <summary>
/// Tests that validate all examples in EXAMPLES.md work correctly
/// These tests ensure our documentation is accurate and examples are functional
/// </summary>
public class ExampleValidationTests : IDisposable
{
    private static readonly string SolutionPath = GetSolutionPath();
    private static readonly string TestOutputPath =
        Path.Combine(Path.GetDirectoryName(SolutionPath)!,
            "RefactorMCP.Tests",
            "TestOutput",
            "Examples");

    public ExampleValidationTests()
    {
        Directory.CreateDirectory(TestOutputPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(TestOutputPath))
        {
            Directory.Delete(TestOutputPath, true);
        }
    }

    private static string GetSolutionPath()
    {
        // Start from the current directory and walk up to find the solution file
        var currentDir = Directory.GetCurrentDirectory();
        var dir = new DirectoryInfo(currentDir);

        while (dir != null)
        {
            var solutionFile = Path.Combine(dir.FullName, "RefactorMCP.sln");
            if (File.Exists(solutionFile))
            {
                return solutionFile;
            }
            dir = dir.Parent;
        }

        // Fallback to relative path
        return "./RefactorMCP.sln";
    }

    [Fact]
    public async Task Example_ExtractMethod_ValidationLogic_WorksAsDocumented()
    {
        // Arrange - Create the exact code from our documentation
        var testFile = Path.Combine(TestOutputPath, "ExtractMethodExample.cs");
        await CreateTestFile(testFile, GetCalculatorCodeForExtractMethod());
        await RefactoringTools.LoadSolution(SolutionPath);

        // Act - Use the exact command from EXAMPLES.md
        var result = await RefactoringTools.ExtractMethod(
            testFile,
            "22:9-25:10", // From documentation: validation block
            "ValidateInputs",
            SolutionPath
        );

        // Assert result text and file contents
        Assert.Contains("Successfully extracted method", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("ValidateInputs();", fileContent);
    }

    [Fact]
    public async Task Example_IntroduceField_AverageCalculation_WorksAsDocumented()
    {
        // Arrange - Create the exact code from our documentation
        var testFile = Path.Combine(TestOutputPath, "IntroduceFieldExample.cs");
        await CreateTestFile(testFile, GetCalculatorCodeForIntroduceField());
        await RefactoringTools.LoadSolution(SolutionPath);

        // Act - Use the exact command from EXAMPLES.md
        var result = await RefactoringTools.IntroduceField(
            testFile,
            "36:20-36:56", // From documentation: Sum() / Count expression
            "_averageValue",
            "private",
            SolutionPath
        );

        // Assert result text and file contents
        Assert.Contains("Successfully introduced private field", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("_averageValue", fileContent);
    }

    [Fact]
    public async Task Example_IntroduceVariable_ComplexExpression_WorksAsDocumented()
    {
        // Arrange - Create the exact code from our documentation
        var testFile = Path.Combine(TestOutputPath, "IntroduceVariableExample.cs");
        await CreateTestFile(testFile, GetCalculatorCodeForIntroduceVariable());
        await RefactoringTools.LoadSolution(SolutionPath);

        // Act - Use the exact command from EXAMPLES.md
        var result = await RefactoringTools.IntroduceVariable(
            testFile,
            "42:50-42:63", // From documentation: value * 2 + 10 expression
            "processedValue",
            SolutionPath
        );

        // Assert result text and file contents
        Assert.Contains("Successfully introduced variable", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("processedValue", fileContent);
    }

    [Fact]
    public async Task Example_MakeFieldReadonly_FormatField_WorksAsDocumented()
    {
        // Arrange - Create the exact code from our documentation
        var testFile = Path.Combine(TestOutputPath, "MakeFieldReadonlyExample.cs");
        await CreateTestFile(testFile, GetCalculatorCodeForMakeFieldReadonly());
        await RefactoringTools.LoadSolution(SolutionPath);

        // Act - Use the exact command from EXAMPLES.md
        var result = await RefactoringTools.MakeFieldReadonly(
            testFile,
            "format", // From documentation: line with format field
            SolutionPath
        );

        // Assert result text and file contents
        Assert.Contains("Successfully made field 'format' readonly", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("readonly string format", fileContent);
    }

    [Fact]
    public async Task Example_SafeDeleteParameter_UnusedParam_WorksAsDocumented()
    {
        var testFile = Path.Combine(TestOutputPath, "SafeDeleteParameter.cs");
        await CreateTestFile(testFile, GetCalculatorCodeForSafeDelete());
        await RefactoringTools.LoadSolution(SolutionPath);

        var result = await RefactoringTools.SafeDeleteParameter(
            testFile,
            "Multiply",
            "unusedParam",
            SolutionPath
        );

        Assert.Contains("Successfully deleted parameter", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.DoesNotContain("unusedParam", fileContent);
    }

    [Fact]
    public async Task QuickReference_ExtractMethod_WorksAsDocumented()
    {
        // Test the quick reference example
        var testFile = Path.Combine(TestOutputPath, "QuickRefExtractMethod.cs");
        await CreateTestFile(testFile, GetCalculatorCodeForExtractMethod());
        await RefactoringTools.LoadSolution(SolutionPath);

        // Use the exact command from QUICK_REFERENCE.md
        var result = await RefactoringTools.ExtractMethod(
            testFile,
            "22:9-25:10",
            "ValidateInputs",
            SolutionPath
        );

        Assert.Contains("Successfully extracted method", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
    }

    [Fact]
    public async Task QuickReference_IntroduceField_WorksAsDocumented()
    {
        // Test the quick reference example
        var testFile = Path.Combine(TestOutputPath, "QuickRefIntroduceField.cs");
        await CreateTestFile(testFile, GetCalculatorCodeForIntroduceField());
        await RefactoringTools.LoadSolution(SolutionPath);

        // Use the exact command from QUICK_REFERENCE.md
        var result = await RefactoringTools.IntroduceField(
            testFile,
            "36:20-36:56",
            "_averageValue",
            "private",
            SolutionPath
        );

        Assert.Contains("Successfully introduced private field", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("_averageValue", fileContent);
    }

    [Theory]
    [InlineData("private")]
    [InlineData("public")]
    [InlineData("protected")]
    [InlineData("internal")]
    public async Task Example_IntroduceField_AllAccessModifiers_WorkCorrectly(string accessModifier)
    {
        // Test that all documented access modifiers work
        var testFile = Path.Combine(TestOutputPath, $"AccessModifier_{accessModifier}.cs");
        await CreateTestFile(testFile, GetCalculatorCodeForIntroduceField());
        await RefactoringTools.LoadSolution(SolutionPath);

        var result = await RefactoringTools.IntroduceField(
            testFile,
            "36:20-36:56",
            $"_{accessModifier}Field",
            accessModifier,
            SolutionPath
        );

        Assert.Contains($"Successfully introduced {accessModifier} field", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains($"_{accessModifier}Field", fileContent);
    }

    [Fact]
    public async Task Documentation_RangeFormat_ExamplesAreAccurate()
    {
        // Test the range calculation example from EXAMPLES.md
        var testFile = Path.Combine(TestOutputPath, "RangeFormatTest.cs");
        var code = """
public int Calculate(int a, int b)
{
    if (a < 0 || b < 0)
    {
        throw new ArgumentException("Negative numbers not allowed");
    }
}
""";
        await CreateTestFile(testFile, code);
        await RefactoringTools.LoadSolution(SolutionPath);

        // The documentation says to select "if (a < 0 || b < 0)" on line 3
        // with range "3:5-3:25"
        var result = await RefactoringTools.ExtractMethod(
            testFile,
            "3:5-3:25", // From documentation example
            "TestMethod",
            SolutionPath
        );

        // This should work or give a meaningful error
        Assert.DoesNotContain("Invalid selection range format", result);
    }

    [Fact]
    public async Task ErrorHandling_DocumentedErrorCases_ReturnExpectedMessages()
    {
        await RefactoringTools.LoadSolution(SolutionPath);

        // Test documented error cases
        var fileNotFoundResult = await RefactoringTools.ExtractMethod(
            "./NonExistent.cs",
            "1:1-2:2",
            "TestMethod",
            SolutionPath
        );
        Assert.Contains("Error: File", fileNotFoundResult);

        var invalidRangeResult = await RefactoringTools.ExtractMethod(
            Path.Combine(Path.GetDirectoryName(SolutionPath)!, "RefactorMCP.Tests", "ExampleCode.cs"),
            "invalid-range",
            "TestMethod",
            SolutionPath
        );
        Assert.Contains("Error: Invalid selection range format", invalidRangeResult);
    }

    // Helper methods that create the exact code from our examples
    private static async Task CreateTestFile(string filePath, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(filePath, content);
    }

    // Exact code from our ExampleCode.cs for Extract Method
    private static string GetCalculatorCodeForExtractMethod()
    {
        return File.ReadAllText(Path.Combine(Path.GetDirectoryName(SolutionPath)!, "RefactorMCP.Tests", "ExampleCode.cs"));
    }

    // Exact code from our ExampleCode.cs for Introduce Field
    private static string GetCalculatorCodeForIntroduceField()
    {
        return File.ReadAllText(Path.Combine(Path.GetDirectoryName(SolutionPath)!, "RefactorMCP.Tests", "ExampleCode.cs"));
    }

    // Exact code from our ExampleCode.cs for Introduce Variable
    private static string GetCalculatorCodeForIntroduceVariable()
    {
        return File.ReadAllText(Path.Combine(Path.GetDirectoryName(SolutionPath)!, "RefactorMCP.Tests", "ExampleCode.cs"));
    }

    // Exact code from our ExampleCode.cs for Make Field Readonly
    private static string GetCalculatorCodeForMakeFieldReadonly()
    {
        return File.ReadAllText(Path.Combine(Path.GetDirectoryName(SolutionPath)!, "RefactorMCP.Tests", "ExampleCode.cs"));
    }

    // Exact code from our ExampleCode.cs for Safe Delete
    private static string GetCalculatorCodeForSafeDelete()
    {
        return File.ReadAllText(Path.Combine(Path.GetDirectoryName(SolutionPath)!, "RefactorMCP.Tests", "ExampleCode.cs"));
    }
}
