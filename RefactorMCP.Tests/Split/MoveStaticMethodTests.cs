using ModelContextProtocol;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace RefactorMCP.Tests;

public class MoveStaticMethodTests : TestBase
{
    [Fact]
    public async Task MoveStaticMethod_ReturnsSuccess()
    {
        await RefactoringTools.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "MoveStaticMethod.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForMoveStaticMethod());

        var result = await RefactoringTools.MoveStaticMethod(
            SolutionPath,
            testFile,
            "FormatCurrency",
            "MathUtilities");

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
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForMoveStaticMethodWithUsings());

        var result = await RefactoringTools.MoveStaticMethod(
            SolutionPath,
            testFile,
            "PrintList",
            "UtilClass");

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
}
