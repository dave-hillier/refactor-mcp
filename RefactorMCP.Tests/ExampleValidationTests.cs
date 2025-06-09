using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

/// <summary>
/// Tests that validate all examples in EXAMPLES.md work correctly
/// These tests ensure our documentation is accurate and examples are functional
/// </summary>
public class ExampleValidationTests
{
    private static readonly string SolutionPath = GetSolutionPath();
    private const string TestOutputPath = "./RefactorMCP.Tests/TestOutput/Examples";

    public ExampleValidationTests()
    {
        Directory.CreateDirectory(TestOutputPath);
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
            SolutionPath,
            testFile,
            "22:9-25:10", // From documentation: validation block
            "ValidateInputs"
        );

        // Assert
        Assert.Contains("Successfully extracted method", result);
        Assert.Contains("ValidateInputs", result);

        // Verify the transformation matches documentation expectations
        var modifiedContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("ValidateInputs();", modifiedContent);
        Assert.Contains("private void ValidateInputs()", modifiedContent);
        Assert.Contains("if (a < 0 || b < 0)", modifiedContent);
        Assert.Contains("throw new ArgumentException", modifiedContent);
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
            SolutionPath,
            testFile,
            "35:16-35:58", // From documentation: Sum() / Count expression
            "_averageValue",
            "private"
        );

        // Assert
        Assert.Contains("Successfully introduced private field", result);
        Assert.Contains("_averageValue", result);

        // Verify the transformation matches documentation expectations
        var modifiedContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("private", modifiedContent);
        Assert.Contains("_averageValue", modifiedContent);
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
            SolutionPath,
            testFile,
            "41:50-41:65", // From documentation: value * 2 + 10 expression
            "processedValue"
        );

        // Assert
        Assert.Contains("Successfully introduced variable", result);
        Assert.Contains("processedValue", result);

        // Verify the transformation matches documentation expectations
        var modifiedContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("var processedValue", modifiedContent);
        Assert.Contains("value * 2 + 10", modifiedContent);
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
            SolutionPath,
            testFile,
            50 // From documentation: line with format field
        );

        // Assert
        Assert.Contains("Successfully made field readonly", result);

        // Verify the transformation matches documentation expectations
        var modifiedContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("readonly", modifiedContent);
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
            SolutionPath,
            testFile,
            "22:9-25:10",
            "ValidateInputs"
        );

        Assert.Contains("Successfully extracted method", result);
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
            SolutionPath,
            testFile,
            "35:16-35:58",
            "_averageValue",
            "private"
        );

        Assert.Contains("Successfully introduced private field", result);
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
            SolutionPath,
            testFile,
            "35:16-35:58",
            $"_{accessModifier}Field",
            accessModifier
        );

        Assert.Contains($"Successfully introduced {accessModifier} field", result);
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
            SolutionPath,
            testFile,
            "3:5-3:25", // From documentation example
            "TestMethod"
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
            SolutionPath,
            "./NonExistent.cs",
            "1:1-2:2",
            "TestMethod"
        );
        Assert.Contains("Error: File", fileNotFoundResult);

        var invalidRangeResult = await RefactoringTools.ExtractMethod(
            SolutionPath,
            "./RefactorMCP.Tests/ExampleCode.cs",
            "invalid-range",
            "TestMethod"
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
        return """
using System;
using System.Collections.Generic;
using System.Linq;

namespace RefactorMCP.Tests.Examples
{
    public class Calculator
    {
        private List<int> numbers = new List<int>();
        private readonly string operatorSymbol;

        public Calculator(string op)
        {
            operatorSymbol = op;
        }

        // Example for Extract Method refactoring
        public int Calculate(int a, int b)
        {
            // This code block can be extracted into a method
            if (a < 0 || b < 0)
            {
                throw new ArgumentException("Negative numbers not allowed");
            }
            
            var result = a + b;
            numbers.Add(result);
            Console.WriteLine($"Result: {result}");
            return result;
        }
    }
}
""";
    }

    // Exact code from our ExampleCode.cs for Introduce Field
    private static string GetCalculatorCodeForIntroduceField()
    {
        return """
using System;
using System.Collections.Generic;
using System.Linq;

namespace RefactorMCP.Tests.Examples
{
    public class Calculator
    {
        private List<int> numbers = new List<int>();

        // Example for Introduce Field refactoring
        public double GetAverage()
        {
            return numbers.Sum() / (double)numbers.Count; // This expression can become a field
        }
    }
}
""";
    }

    // Exact code from our ExampleCode.cs for Introduce Variable
    private static string GetCalculatorCodeForIntroduceVariable()
    {
        return """
using System;
using System.Collections.Generic;
using System.Linq;

namespace RefactorMCP.Tests.Examples
{
    public class Calculator
    {
        // Example for Introduce Variable refactoring
        public string FormatResult(int value)
        {
            return $"The calculation result is: {value * 2 + 10}"; // Complex expression can become a variable
        }
    }
}
""";
    }

    // Exact code from our ExampleCode.cs for Make Field Readonly
    private static string GetCalculatorCodeForMakeFieldReadonly()
    {
        return """
using System;
using System.Collections.Generic;
using System.Linq;

namespace RefactorMCP.Tests.Examples
{
    public class Calculator
    {
        private readonly string operatorSymbol;

        public Calculator(string op)
        {
            operatorSymbol = op;
        }

        // Example for Make Field Readonly refactoring
        private string format = "Currency"; // This field can be made readonly

        public void SetFormat(string newFormat)
        {
            format = newFormat; // This assignment would move to constructor
        }
    }
}
""";
    }
} 