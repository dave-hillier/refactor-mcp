using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class RefactoringToolsTests
{
    private static readonly string SolutionPath = GetSolutionPath();
    private const string ExampleFilePath = "./RefactorMCP.Tests/ExampleCode.cs";
    private const string TestOutputPath = "./RefactorMCP.Tests/TestOutput";

    public RefactoringToolsTests()
    {
        // Ensure test output directory exists
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
    public async Task LoadSolution_ValidPath_ReturnsSuccess()
    {
        // Act
        var result = await RefactoringTools.LoadSolution(SolutionPath);

        // Assert
        Assert.Contains("Successfully loaded solution", result);
        Assert.Contains("RefactorMCP.ConsoleApp", result);
        Assert.Contains("RefactorMCP.Tests", result);
    }

    [Fact]
    public async Task LoadSolution_InvalidPath_ReturnsError()
    {
        // Act
        var result = await RefactoringTools.LoadSolution("./NonExistent.sln");

        // Assert
        Assert.Contains("Error: Solution file not found", result);
    }

    [Fact]
    public async Task ExtractMethod_ValidSelection_ReturnsSuccess()
    {
        // Arrange
        await RefactoringTools.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "ExtractMethodTest.cs");
        await CreateTestFile(testFile, GetSampleCodeForExtractMethod());

        // Act
        var result = await RefactoringTools.ExtractMethod(
            SolutionPath,
            testFile,
            "5:9-8:10", // The validation block in the test method
            "ValidateInputs"
        );

        // Assert
        Assert.Contains("Successfully extracted method", result);
        Assert.Contains("ValidateInputs", result);

        // Verify the file was modified
        var modifiedContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("ValidateInputs();", modifiedContent);
        Assert.Contains("private void ValidateInputs()", modifiedContent);
    }

    [Fact]
    public async Task ExtractMethod_InvalidRange_ReturnsError()
    {
        // Arrange
        await RefactoringTools.LoadSolution(SolutionPath);

        // Act
        var result = await RefactoringTools.ExtractMethod(
            SolutionPath,
            ExampleFilePath,
            "invalid-range",
            "TestMethod"
        );

        // Assert
        Assert.Contains("Error: Invalid selection range format", result);
    }

    [Fact]
    public async Task IntroduceField_ValidExpression_ReturnsSuccess()
    {
        // Arrange
        await RefactoringTools.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "IntroduceFieldTest.cs");
        await CreateTestFile(testFile, GetSampleCodeForIntroduceField());

        // Act
        var result = await RefactoringTools.IntroduceField(
            SolutionPath,
            testFile,
            "4:16-4:58", // The Sum() / Count expression
            "_averageValue",
            "private"
        );

        // Assert
        Assert.Contains("Successfully introduced", result);
        Assert.Contains("_averageValue", result);

        // Verify the file was modified
        var modifiedContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("private", modifiedContent);
        Assert.Contains("_averageValue", modifiedContent);
    }

    [Fact]
    public async Task IntroduceField_WithPublicModifier_ReturnsSuccess()
    {
        // Arrange
        await RefactoringTools.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "IntroduceFieldPublicTest.cs");
        await CreateTestFile(testFile, GetSampleCodeForIntroduceField());

        // Act
        var result = await RefactoringTools.IntroduceField(
            SolutionPath,
            testFile,
            "4:16-4:58",
            "_publicField",
            "public"
        );

        // Assert
        Assert.Contains("Successfully introduced public field", result);
    }

    [Fact]
    public async Task IntroduceVariable_ValidExpression_ReturnsSuccess()
    {
        // Arrange
        await RefactoringTools.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "IntroduceVariableTest.cs");
        await CreateTestFile(testFile, GetSampleCodeForIntroduceVariable());

        // Act
        var result = await RefactoringTools.IntroduceVariable(
            SolutionPath,
            testFile,
            "4:50-4:65", // The value * 2 + 10 expression
            "processedValue"
        );

        // Assert
        Assert.Contains("Successfully introduced variable", result);
        Assert.Contains("processedValue", result);

        // Verify the file was modified
        var modifiedContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("var processedValue", modifiedContent);
    }

    [Fact]
    public async Task MakeFieldReadonly_FieldWithInitializer_ReturnsSuccess()
    {
        // Arrange
        await RefactoringTools.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "MakeFieldReadonlyTest.cs");
        await CreateTestFile(testFile, GetSampleCodeForMakeFieldReadonly());

        // Act
        var result = await RefactoringTools.MakeFieldReadonly(
            SolutionPath,
            testFile,
            4 // Line with the field declaration
        );

        // Assert
        Assert.Contains("Successfully made field readonly", result);

        // Verify the file was modified
        var modifiedContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("readonly", modifiedContent);
    }

    [Fact]
    public async Task MakeFieldReadonly_FieldWithoutInitializer_ReturnsSuccess()
    {
        // Arrange
        await RefactoringTools.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "MakeFieldReadonlyNoInitTest.cs");
        await CreateTestFile(testFile, GetSampleCodeForMakeFieldReadonlyNoInit());

        // Act
        var result = await RefactoringTools.MakeFieldReadonly(
            SolutionPath,
            testFile,
            4 // Line with the field declaration
        );

        // Assert
        Assert.Contains("Successfully made field readonly", result);
    }

    [Fact]
    public async Task MakeFieldReadonly_InvalidLine_ReturnsError()
    {
        // Arrange
        await RefactoringTools.LoadSolution(SolutionPath);

        // Act
        var result = await RefactoringTools.MakeFieldReadonly(
            SolutionPath,
            ExampleFilePath,
            999 // Non-existent line
        );

        // Assert
        Assert.Contains("Error:", result);
    }

    [Fact]
    public async Task RefactoringTools_FileNotInSolution_ReturnsError()
    {
        // Arrange
        await RefactoringTools.LoadSolution(SolutionPath);

        // Act
        var result = await RefactoringTools.ExtractMethod(
            SolutionPath,
            "./NonExistent.cs",
            "1:1-2:2",
            "TestMethod"
        );

        // Assert
        Assert.Contains("Error: File", result);
        Assert.Contains("not found in solution", result);
    }

    [Theory]
    [InlineData("1:1-", "TestMethod")]
    [InlineData("1-2:2", "TestMethod")]
    [InlineData("abc:def-ghi:jkl", "TestMethod")]
    [InlineData("1:1-2", "TestMethod")]
    public async Task ExtractMethod_InvalidRangeFormats_ReturnsError(string range, string methodName)
    {
        // Arrange
        await RefactoringTools.LoadSolution(SolutionPath);

        // Act
        var result = await RefactoringTools.ExtractMethod(
            SolutionPath,
            ExampleFilePath,
            range,
            methodName
        );

        // Assert
        Assert.Contains("Error: Invalid selection range format", result);
    }

    [Fact]
    public async Task IntroduceField_DifferentAccessModifiers_ReturnsCorrectModifier()
    {
        // Arrange
        await RefactoringTools.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "AccessModifierTest.cs");

        var accessModifiers = new[] { "private", "public", "protected", "internal" };

        foreach (var modifier in accessModifiers)
        {
            // Create a fresh test file for each modifier
            var modifierTestFile = testFile.Replace(".cs", $"_{modifier}.cs");
            await CreateTestFile(modifierTestFile, GetSampleCodeForIntroduceField());

            // Act
            var result = await RefactoringTools.IntroduceField(
                SolutionPath,
                modifierTestFile,
                "4:16-4:58",
                $"_{modifier}Field",
                modifier
            );

            // Assert
            Assert.Contains($"Successfully introduced {modifier} field", result);
        }
    }

    // Helper methods to create test files
    private static async Task CreateTestFile(string filePath, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(filePath, content);
    }

    private static string GetSampleCodeForExtractMethod()
    {
        return """
using System;
public class TestClass
{
    public int Calculate(int a, int b)
    {
        if (a < 0 || b < 0)
        {
            throw new ArgumentException("Negative numbers not allowed");
        }
        
        var result = a + b;
        return result;
    }
}
""";
    }

    private static string GetSampleCodeForIntroduceField()
    {
        return """
using System.Linq;
public class TestClass
{
    private int[] numbers = {1, 2, 3, 4, 5};
    public double GetAverage() => numbers.Sum() / (double)numbers.Length;
}
""";
    }

    private static string GetSampleCodeForIntroduceVariable()
    {
        return """
using System;
public class TestClass
{
    public string FormatResult(int value)
    {
        return $"The calculation result is: {value * 2 + 10}";
    }
}
""";
    }

    private static string GetSampleCodeForMakeFieldReadonly()
    {
        return """
using System;
public class TestClass
{
    private string format = "Currency";
    
    public TestClass()
    {
        // Constructor for readonly field initialization
    }
}
""";
    }

    private static string GetSampleCodeForMakeFieldReadonlyNoInit()
    {
        return """
using System;
public class TestClass
{
    private string format;
    
    public TestClass()
    {
        // Constructor exists
    }
}
""";
    }
}

// Integration tests for the CLI test mode
public class CliIntegrationTests
{
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
    public async Task CliTestMode_LoadSolution_WorksCorrectly()
    {
        // This would be tested by running the actual CLI command
        // For now, we test the underlying functionality
        var result = await RefactoringTools.LoadSolution(GetSolutionPath());
        Assert.Contains("Successfully loaded solution", result);
    }

    [Fact]
    public async Task CliTestMode_AllToolsListed_ReturnsExpectedTools()
    {
        // Test that all expected tools are available
        var expectedTools = new[]
        {
            "load-solution",
            "extract-method", 
            "introduce-field",
            "introduce-variable",
            "make-field-readonly"
        };

        // Verify RefactoringTools class has all the expected methods
        var type = typeof(RefactoringTools);
        var methods = type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        foreach (var tool in expectedTools)
        {
            var methodName = tool.Replace("-", "")
                .Split('-')
                .Select(word => char.ToUpper(word[0]) + word[1..])
                .Aggregate((a, b) => a + b);

            var method = methods.FirstOrDefault(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(method);
        }
    }
}
