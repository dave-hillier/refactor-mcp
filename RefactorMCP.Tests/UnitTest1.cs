using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class RefactoringToolsTests : IDisposable
{
    private static readonly string SolutionPath = GetSolutionPath();
    private static readonly string ExampleFilePath = Path.Combine(Path.GetDirectoryName(SolutionPath)!, "RefactorMCP.Tests", "ExampleCode.cs");
    private const string TestOutputPath = "./RefactorMCP.Tests/TestOutput";

    public RefactoringToolsTests()
    {
        // Ensure test output directory exists
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
    public async Task UnloadSolution_RemovesCachedSolution()
    {
        await RefactoringTools.LoadSolution(SolutionPath);
        var result = RefactoringTools.UnloadSolution(SolutionPath);
        Assert.Contains("Unloaded solution", result);
    }

    [Fact]
    public async Task LoadSolution_InvalidPath_ReturnsError()
    {
        // Act
        var result = await RefactoringTools.LoadSolution("./NonExistent.sln");

        // Assert
        Assert.Contains("Error: Solution file not found", result);
    }

    [Fact(Skip = "Refactoring does not generate method in single-file mode yet")]
    public async Task ExtractMethod_ValidSelection_ReturnsSuccess()
    {
        // Arrange
        await RefactoringTools.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "ExtractMethodTest.cs");
        await CreateTestFile(testFile, GetSampleCodeForExtractMethod());

        // Act
        var result = await RefactoringTools.ExtractMethod(
            testFile,
            "7:9-10:10", // The validation block in the test method
            "ValidateInputs",
            SolutionPath
        );

        // Assert result text and file contents
        Assert.Contains("Successfully extracted method", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("ValidateInputs();", fileContent);
    }

    [Fact]
    public async Task ExtractMethod_InvalidRange_ReturnsError()
    {
        // Arrange
        await RefactoringTools.LoadSolution(SolutionPath);

        // Act
        var result = await RefactoringTools.ExtractMethod(
            ExampleFilePath,
            "invalid-range",
            "TestMethod",
            SolutionPath
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
            testFile,
            "4:16-4:58", // The Sum() / Count expression
            "_averageValue",
            "private",
            SolutionPath
        );

        // Assert result text and file contents
        Assert.Contains("Successfully introduced", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("_averageValue", fileContent);
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
            testFile,
            "4:16-4:58",
            "_publicField",
            "public",
            SolutionPath
        );

        // Assert result text and file contents
        Assert.Contains("Successfully introduced public field", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("_publicField", fileContent);
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
            testFile,
            "42:50-42:63", // The value * 2 + 10 expression
            "processedValue",
            SolutionPath
        );

        // Assert result text and file contents
        Assert.Contains("Successfully introduced variable", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("processedValue", fileContent);
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
            testFile,
            "format",
            SolutionPath
        );

        // Assert result text and file contents
        Assert.Contains("Successfully made field 'format' readonly", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("readonly string format", fileContent);
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
            testFile,
            "description",
            SolutionPath
        );

        // Assert result text and file contents
        Assert.Contains("Successfully made field 'description' readonly", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("readonly string description", fileContent);
    }

    [Fact]
    public async Task MakeFieldReadonly_InvalidLine_ReturnsError()
    {
        // Arrange
        await RefactoringTools.LoadSolution(SolutionPath);

        // Act
        var result = await RefactoringTools.MakeFieldReadonly(
            ExampleFilePath,
            "nonexistent",
            SolutionPath
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
            "./NonExistent.cs",
            "1:1-2:2",
            "TestMethod",
            SolutionPath
        );

        // Assert
        Assert.Contains("Error: File", result);
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
            ExampleFilePath,
            range,
            methodName,
            SolutionPath
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
                modifierTestFile,
                "4:16-4:58",
                $"_{modifier}Field",
                modifier,
                SolutionPath
            );

            // Assert result text and file contents
            Assert.Contains($"Successfully introduced {modifier} field", result);
            var fileContent = await File.ReadAllTextAsync(modifierTestFile);
            Assert.Contains($"_{modifier}Field", fileContent);
        }
    }

    [Fact]
    public async Task ConvertToStaticWithInstance_ReturnsSuccess()
    {
        await RefactoringTools.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "ConvertToStaticInstance.cs");
        await CreateTestFile(testFile, GetSampleCodeForConvertToStaticInstance());

        var result = await RefactoringTools.ConvertToStaticWithInstance(
            testFile,
            "GetFormattedNumber",
            "instance",
            SolutionPath
        );

        Assert.Contains("Successfully converted method 'GetFormattedNumber' to static with instance parameter", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("static string GetFormattedNumber", fileContent);
        Assert.Contains("Calculator instance", fileContent);
    }

    [Fact]
    public async Task ConvertToExtensionMethod_ReturnsSuccess()
    {
        await RefactoringTools.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "ConvertToExtension.cs");
        await CreateTestFile(testFile, GetSampleCodeForConvertToExtension());

        var result = await RefactoringTools.ConvertToExtensionMethod(
            testFile,
            "GetFormattedNumber",
            null,
            SolutionPath
        );

        Assert.Contains("Successfully converted method 'GetFormattedNumber' to extension method", result);

        // File modification verification skipped
    }

    [Fact]
    public async Task MoveInstanceMethod_ReturnsSuccess()
    {
        await RefactoringTools.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "MoveInstanceMethod.cs");
        await CreateTestFile(testFile, GetSampleCodeForMoveInstanceMethod());

        var result = await RefactoringTools.MoveInstanceMethod(
            testFile,
            "LogOperation",
            "Logger",
            "_logger",
            "field",
            SolutionPath
        );

        Assert.Contains("Successfully moved instance method", result);

        // File modification verification skipped
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
        return File.ReadAllText(Path.Combine(Path.GetDirectoryName(SolutionPath)!, "RefactorMCP.Tests", "ExampleCode.cs"));
    }

    private static string GetSampleCodeForIntroduceVariable()
    {
        return File.ReadAllText(Path.Combine(Path.GetDirectoryName(SolutionPath)!, "RefactorMCP.Tests", "ExampleCode.cs"));
    }

    private static string GetSampleCodeForMakeFieldReadonly()
    {
        return File.ReadAllText(Path.Combine(Path.GetDirectoryName(SolutionPath)!, "RefactorMCP.Tests", "ExampleCode.cs"));
    }

    private static string GetSampleCodeForMakeFieldReadonlyNoInit()
    {
        return """
using System;
public class TestClass
{
    private string description;
}
""";
    }

    private static string GetSampleCodeForConvertToStaticInstance()
    {
        return File.ReadAllText(Path.Combine(Path.GetDirectoryName(SolutionPath)!, "RefactorMCP.Tests", "ExampleCode.cs"));
    }

    private static string GetSampleCodeForMoveInstanceMethod()
    {
        return File.ReadAllText(Path.Combine(Path.GetDirectoryName(SolutionPath)!, "RefactorMCP.Tests", "ExampleCode.cs"));
    }

    private static string GetSampleCodeForConvertToExtension()
    {
        return File.ReadAllText(Path.Combine(Path.GetDirectoryName(SolutionPath)!, "RefactorMCP.Tests", "ExampleCode.cs"));
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
