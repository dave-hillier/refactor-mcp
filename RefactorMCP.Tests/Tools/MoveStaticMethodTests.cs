using ModelContextProtocol;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorMCP.ConsoleApp.Move;
using Xunit;

namespace RefactorMCP.Tests;

public class MoveStaticMethodTests : TestBase
{
    [Fact]
    public async Task MoveStaticMethod_ReturnsSuccess()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.Combine(TestOutputPath, "MoveStaticMethod.cs");
        await TestUtilities.CreateTestFile(testFile, "public class SourceClass { public static void Foo(){} } public class TargetClass { } ");

        var result = await MoveMethodTool.MoveStaticMethod(
            SolutionPath,
            testFile,
            "Foo",
            "TargetClass",
            null,
            null,
            CancellationToken.None);

        Assert.Contains("Successfully moved static method", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.DoesNotContain("static void Foo()", fileContent);
        Assert.Contains("class TargetClass", fileContent);
    }

    [Fact]
    public async Task MoveStaticMethod_AddsUsingsAndCompiles()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "MoveStaticWithUsings.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForMoveStaticMethodWithUsings());

        var result = await MoveMethodTool.MoveStaticMethod(
            SolutionPath,
            testFile,
            "PrintList",
            "UtilClass",
            null,
            null,
            CancellationToken.None);

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
