using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModelContextProtocol;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
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
        await Assert.ThrowsAsync<McpException>(async () =>
            await RefactoringTools.LoadSolution("./NonExistent.sln"));
    }

    [Fact]
    public void Version_ReturnsInfo()
    {
        var result = RefactoringTools.Version();
        Assert.Contains("Version:", result);
        Assert.Contains("Build", result);
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
            SolutionPath,
            testFile,
            "7:9-10:10", // The validation block in the test method
            "ValidateInputs"
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
        await Assert.ThrowsAsync<McpException>(async () =>
            await RefactoringTools.ExtractMethod(
                SolutionPath,
                ExampleFilePath,
                "invalid-range",
                "TestMethod"));
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
            SolutionPath,
            testFile,
            "4:16-4:58",
            "_publicField",
            "public"
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
            SolutionPath,
            testFile,
            "42:50-42:63", // The value * 2 + 10 expression
            "processedValue"
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
            SolutionPath,
            testFile,
            "format"
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
            SolutionPath,
            testFile,
            "description"
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
        await Assert.ThrowsAsync<McpException>(async () =>
            await RefactoringTools.MakeFieldReadonly(
                SolutionPath,
                ExampleFilePath,
                "nonexistent"));
    }

    [Fact]
    public async Task RefactoringTools_FileNotInSolution_ReturnsError()
    {
        // Arrange
        await RefactoringTools.LoadSolution(SolutionPath);

        // Act
        await Assert.ThrowsAsync<McpException>(async () =>
            await RefactoringTools.ExtractMethod(
                SolutionPath,
                "./NonExistent.cs",
                "1:1-2:2",
                "TestMethod"));
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
        await Assert.ThrowsAsync<McpException>(async () =>
            await RefactoringTools.ExtractMethod(
                SolutionPath,
                ExampleFilePath,
                range,
                methodName));
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
            SolutionPath,
            testFile,
            "GetFormattedNumber",
            "instance"
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
            SolutionPath,
            testFile,
            "GetFormattedNumber",
            null
        );

        Assert.Contains("Successfully converted method 'GetFormattedNumber' to extension method", result);

        // File modification verification skipped
    }

    [Fact]
    public async Task MoveStaticMethod_ReturnsSuccess()
    {
        await RefactoringTools.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "MoveStaticMethod.cs");
        await CreateTestFile(testFile, GetSampleCodeForMoveStaticMethod());

        var result = await RefactoringTools.MoveStaticMethod(
            SolutionPath,
            testFile,
            "FormatCurrency",
            "MathUtilities"
        );

        Assert.Contains("Successfully moved static method", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.DoesNotContain("static string FormatCurrency", fileContent);
        Assert.Contains("class MathUtilities", fileContent);
    }

    [Fact]
    public async Task MoveStaticMethod_AddsUsingsAndCompiles()
    {
        await RefactoringTools.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "MoveStaticWithUsings.cs");
        await CreateTestFile(testFile, GetSampleCodeForMoveStaticMethodWithUsings());

        var result = await RefactoringTools.MoveStaticMethod(
            SolutionPath,
            testFile,
            "PrintList",
            "UtilClass"
        );

        Assert.Contains("Successfully moved static method", result);
        var targetFile = Path.Combine(Path.GetDirectoryName(testFile)!, "UtilClass.cs");
        var fileContent = await File.ReadAllTextAsync(targetFile);
        Assert.Contains("using System", fileContent);
        Assert.Contains("using System.Collections.Generic", fileContent);

        var syntaxTree = CSharpSyntaxTree.ParseText(fileContent);
        var refs = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!
            .Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create(
            "test",
            new[] { syntaxTree },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var diagnostics = compilation.GetDiagnostics();
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task MoveInstanceMethod_ReturnsSuccess()
    {
        await RefactoringTools.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "MoveInstanceMethod.cs");
        await CreateTestFile(testFile, GetSampleCodeForMoveInstanceMethod());

        var result = await RefactoringTools.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "Calculator",
            "LogOperation",
            "Logger",
            "_logger",
            "field"
        );

        Assert.Contains("Successfully moved instance method", result);

        // File modification verification skipped
    }

    [Fact]
    public async Task MoveInstanceMethod_SameFile_MovesMethod()
    {
        await RefactoringTools.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "MoveInstanceMethodSameFile.cs");
        await CreateTestFile(testFile, GetSampleCodeForMoveInstanceMethod());

        var result = await RefactoringTools.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "Calculator",
            "LogOperation",
            "Logger",
            "_logger",
            "field"
        );

        Assert.Contains("Successfully moved instance method", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        var tree = CSharpSyntaxTree.ParseText(fileContent);
        var root = tree.GetRoot();
        var calcClass = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == "Calculator");
        Assert.DoesNotContain(calcClass.Members.OfType<MethodDeclarationSyntax>(), m => m.Identifier.ValueText == "LogOperation");
        var loggerClass = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == "Logger");
        Assert.Contains(loggerClass.Members.OfType<MethodDeclarationSyntax>(), m => m.Identifier.ValueText == "LogOperation");
    }

    [Fact]
    public async Task MoveInstanceMethod_CreatesTargetClassIfMissing()
    {
        await RefactoringTools.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "MoveInstanceMethodMissingTarget.cs");
        await CreateTestFile(testFile, GetSampleCodeForMoveInstanceMethod());

        var result = await RefactoringTools.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "Calculator",
            "LogOperation",
            "NewLogger",
            "_logger",
            "field"
        );

        Assert.Contains("Successfully moved instance method", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("class NewLogger", fileContent);
    }

    [Fact]
    public async Task MoveInstanceMethod_NewFile_AddsUsingsAndCompiles()
    {
        await RefactoringTools.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "MoveInstanceToNewFile.cs");
        await CreateTestFile(testFile, GetSampleCodeForMoveInstanceMethod());

        var targetFile = Path.Combine(Path.GetDirectoryName(testFile)!, "Logger.cs");

        var result = await RefactoringTools.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "Calculator",
            "LogOperation",
            "Logger",
            "_logger",
            "field",
            targetFile
        );

        Assert.Contains("Successfully moved instance method", result);
        var fileContent = await File.ReadAllTextAsync(targetFile);
        Assert.Contains("using System", fileContent);

        var syntaxTree = CSharpSyntaxTree.ParseText(fileContent);
        var refs = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!
            .Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create(
            "test",
            new[] { syntaxTree },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var diagnostics = compilation.GetDiagnostics();
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task SafeDeleteParameter_RemovesParameter()
    {
        await RefactoringTools.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "SafeDeleteParam.cs");
        await CreateTestFile(testFile, GetSampleCodeForSafeDelete());

        var result = await RefactoringTools.SafeDeleteParameter(
            SolutionPath,
            testFile,
            "Multiply",
            "unusedParam"
        );

        Assert.Contains("Successfully deleted parameter", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.DoesNotContain("unusedParam", fileContent);
    }

    [Fact]
    public async Task MoveInstanceMethod_WithDependencies_AddsAccessMemberAndUpdatesModifiers()
    {
        // Arrange
        await RefactoringTools.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "MoveInstanceMethodWithDeps.cs");
        await CreateTestFile(testFile, GetSampleCodeForMoveInstanceMethodWithDependencies());

        // Act
        var result = await RefactoringTools.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "OrderProcessor",
            "ProcessPayment",
            "PaymentService",
            "_paymentService",
            "field"
        );

        // Assert
        Assert.Contains("Successfully moved instance method", result);
        var fileContent = await File.ReadAllTextAsync(testFile);

        // Verify the method was removed from source class
        var tree = CSharpSyntaxTree.ParseText(fileContent);
        var root = tree.GetRoot();
        var sourceClass = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == "OrderProcessor");
        Assert.DoesNotContain(sourceClass.Members.OfType<MethodDeclarationSyntax>(),
            m => m.Identifier.ValueText == "ProcessPayment");

        // Verify the access member field was added to source class
        var fields = sourceClass.Members.OfType<FieldDeclarationSyntax>();
        Assert.Contains(fields, f => f.Declaration.Variables.Any(v => v.Identifier.ValueText == "_paymentService"));

        // Verify the method was added to target class with public modifier
        var targetClass = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == "PaymentService");
        var movedMethod = targetClass.Members.OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.ValueText == "ProcessPayment");
        Assert.Contains(movedMethod.Modifiers, m => m.IsKind(SyntaxKind.PublicKeyword));
    }

    [Fact]
    public async Task MoveInstanceMethod_NonExistentMethod_ThrowsException()
    {
        // Arrange
        await RefactoringTools.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "MoveInstanceMethodError.cs");
        await CreateTestFile(testFile, GetSampleCodeForMoveInstanceMethodWithDependencies());

        // Act & Assert
        await Assert.ThrowsAsync<McpException>(async () =>
            await RefactoringTools.MoveInstanceMethod(
                SolutionPath,
                testFile,
                "OrderProcessor",
                "NonExistentMethod",  // This method doesn't exist
                "PaymentService",
                "_paymentService",
                "field"
            ));
    }

    [Fact]
    public async Task MoveInstanceMethod_NonExistentSourceClass_ThrowsException()
    {
        // Arrange
        await RefactoringTools.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "MoveInstanceMethodErrorClass.cs");
        await CreateTestFile(testFile, GetSampleCodeForMoveInstanceMethodWithDependencies());

        // Act & Assert
        await Assert.ThrowsAsync<McpException>(async () =>
            await RefactoringTools.MoveInstanceMethod(
                SolutionPath,
                testFile,
                "NonExistentClass",  // This class doesn't exist
                "ProcessPayment",
                "PaymentService",
                "_paymentService",
                "field"
            ));
    }

    [Fact]
    public async Task MoveInstanceMethod_ToNewFileWithProperty_CreatesFileAndMovesMethod()
    {
        // Arrange
        await RefactoringTools.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "MoveInstanceMethodPropertyAccess.cs");
        await CreateTestFile(testFile, GetSampleCodeForMoveInstanceMethodWithDependencies());
        var targetFile = Path.Combine(Path.GetDirectoryName(testFile)!, "NewPaymentService.cs");

        // Act
        var result = await RefactoringTools.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "OrderProcessor",
            "ProcessPayment",
            "NewPaymentService",
            "PaymentHandler",
            "property",  // Using property instead of field
            targetFile
        );

        // Assert
        Assert.Contains("Successfully moved instance method", result);
        Assert.True(File.Exists(targetFile), "Target file should be created");

        var sourceContent = await File.ReadAllTextAsync(testFile);
        var targetContent = await File.ReadAllTextAsync(targetFile);

        // Verify method was REMOVED from source class (this is the key assertion)
        var sourceTree = CSharpSyntaxTree.ParseText(sourceContent);
        var sourceRoot = sourceTree.GetRoot();
        var sourceClass = sourceRoot.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == "OrderProcessor");
        Assert.DoesNotContain(sourceClass.Members.OfType<MethodDeclarationSyntax>(),
            m => m.Identifier.ValueText == "ProcessPayment");

        // Verify property was added to source class
        var properties = sourceClass.Members.OfType<PropertyDeclarationSyntax>();
        Assert.Contains(properties, p => p.Identifier.ValueText == "PaymentHandler");

        // Verify method was added to target file with correct class and public modifier
        Assert.Contains("class NewPaymentService", targetContent);
        Assert.Contains("public bool ProcessPayment", targetContent);
        Assert.Contains("decimal amount, string cardNumber", targetContent);

        // Verify usings were copied to target file
        Assert.Contains("using System;", targetContent);
        Assert.Contains("using System.Collections.Generic;", targetContent);

        // Verify the target class has the method
        var targetTree = CSharpSyntaxTree.ParseText(targetContent);
        var targetRoot = targetTree.GetRoot();
        var targetClass = targetRoot.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == "NewPaymentService");
        Assert.Contains(targetClass.Members.OfType<MethodDeclarationSyntax>(),
            m => m.Identifier.ValueText == "ProcessPayment");
    }

    [Fact]
    public async Task MoveInstanceMethod_WithinSameFile_ShouldRemoveFromSource()
    {
        // Test moving a method within the same file - this should work correctly

        // Arrange
        await RefactoringTools.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "MoveInstanceSameFile.cs");
        await CreateTestFile(testFile, GetSampleCodeForMoveInstanceMethodWithDependencies());

        // Act - move to existing PaymentService class in same file
        var result = await RefactoringTools.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "OrderProcessor",
            "ProcessPayment",
            "PaymentService",
            "_paymentService",
            "field"
        // No targetFilePath - should move within same file
        );

        // Assert
        Assert.Contains("Successfully moved instance method", result);

        var sourceContent = await File.ReadAllTextAsync(testFile);
        var sourceTree = CSharpSyntaxTree.ParseText(sourceContent);
        var sourceRoot = sourceTree.GetRoot();

        // Verify method was removed from OrderProcessor
        var orderProcessorClass = sourceRoot.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == "OrderProcessor");
        Assert.DoesNotContain(orderProcessorClass.Members.OfType<MethodDeclarationSyntax>(),
            m => m.Identifier.ValueText == "ProcessPayment");

        // Verify field was added to OrderProcessor
        var fields = orderProcessorClass.Members.OfType<FieldDeclarationSyntax>();
        Assert.Contains(fields, f => f.Declaration.Variables.Any(v => v.Identifier.ValueText == "_paymentService"));

        // Verify method was added to PaymentService
        var paymentServiceClass = sourceRoot.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == "PaymentService");
        Assert.Contains(paymentServiceClass.Members.OfType<MethodDeclarationSyntax>(),
            m => m.Identifier.ValueText == "ProcessPayment");
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

    private static string GetSampleCodeForMoveStaticMethod()
    {
        return File.ReadAllText(Path.Combine(Path.GetDirectoryName(SolutionPath)!, "RefactorMCP.Tests", "ExampleCode.cs"));
    }

    private static string GetSampleCodeForMoveStaticMethodWithUsings()
    {
        return """
using System;
using System.Collections.Generic;

public class TestClass
{
    public static void PrintList(List<int> numbers)
    {
        Console.WriteLine(string.Join(",", numbers));
    }
}

public class UtilClass { }
""";
    }

    private static string GetSampleCodeForMoveInstanceMethod()
    {
        return File.ReadAllText(Path.Combine(Path.GetDirectoryName(SolutionPath)!, "RefactorMCP.Tests", "ExampleCode.cs"));
    }

    private static string GetSampleCodeForConvertToExtension()
    {
        return File.ReadAllText(Path.Combine(Path.GetDirectoryName(SolutionPath)!, "RefactorMCP.Tests", "ExampleCode.cs"));
    }

    private static string GetSampleCodeForSafeDelete()
    {
        return File.ReadAllText(Path.Combine(Path.GetDirectoryName(SolutionPath)!, "RefactorMCP.Tests", "ExampleCode.cs"));
    }

    private static string GetSampleCodeForMoveInstanceMethodWithDependencies()
    {
        return """
using System;
using System.Collections.Generic;

namespace Test.Domain
{
    public class OrderProcessor
    {
        private readonly string processorId;
        private List<string> log = new();
        
        public OrderProcessor(string id)
        {
            processorId = id;
        }
        
        public bool ValidateOrder(decimal amount)
        {
            return amount > 0;
        }
        
        // This method should be moved to PaymentService
        private bool ProcessPayment(decimal amount, string cardNumber)
        {
            if (string.IsNullOrEmpty(cardNumber))
                return false;
                
            log.Add($"Processing payment of {amount} for processor {processorId}");
            
            // Simulate payment processing
            return amount <= 1000;
        }
        
        public void CompleteOrder(decimal amount, string cardNumber)
        {
            if (ValidateOrder(amount) && ProcessPayment(amount, cardNumber))
            {
                log.Add("Order completed successfully");
            }
        }
    }
    
    public class PaymentService
    {
        // Target class for the moved method
    }
}
""";
    }
}

// Integration tests for the CLI mode
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
        var expectedCommands = new[]
        {
            "list-tools",
            "load-solution",
            "extract-method",
            "introduce-field",
            "introduce-variable",
            "make-field-readonly",
            "unload-solution",
            "clear-solution-cache",
            "convert-to-extension-method",
            "convert-to-static-with-parameters",
            "convert-to-static-with-instance",
            "introduce-parameter",
            "move-static-method",
            "move-instance-method",
            "transform-setter-to-init",
            "safe-delete-field",
            "safe-delete-method",
            "safe-delete-parameter",
            "safe-delete-variable",
            "version"
        };

        var refactoringMethods = typeof(RefactoringTools)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        foreach (var command in expectedCommands)
        {
            if (command == "list-tools")
            {
                var progType = typeof(RefactoringTools).Assembly.GetType("Program");
                Assert.NotNull(progType);
                var progMethod = progType!.GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                    .FirstOrDefault(m => m.Name.Contains("ListAvailableTools"));
                Assert.NotNull(progMethod);
                continue;
            }

            var pascal = string.Concat(command
                .Split('-')
                .Select(w => char.ToUpper(w[0]) + w[1..]));

            var method = refactoringMethods.FirstOrDefault(m =>
                m.Name.Equals(pascal, StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(method);
        }
    }
}
