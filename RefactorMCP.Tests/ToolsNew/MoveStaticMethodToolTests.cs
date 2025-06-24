using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RefactorMCP.ConsoleApp.Move;
using Xunit;

namespace RefactorMCP.Tests.ToolsNew;

public class MoveStaticMethodToolTests : RefactorMCP.Tests.TestBase
{
    [Fact]
    public async Task MoveStaticMethod_CreatesTargetFile()
    {
        const string initialCode = """
public class SourceClass
{
    public static int Foo() { return 1; }
}
""";

        const string expectedSource = """
public class SourceClass
{
    public static int Foo()
    {
        return TargetClass.Foo();
    }
}
""";

        const string expectedTarget = """
public class TargetClass
{
    public static int Foo()
    {
        return 1;
    }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "MoveStatic.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        var result = await MoveMethodTool.MoveStaticMethod(
            SolutionPath,
            testFile,
            "Foo",
            "TargetClass");

        Assert.Contains("Successfully moved static method", result);
        var sourceContent = await File.ReadAllTextAsync(testFile);
        Assert.DoesNotContain("Foo() { return 1; }", sourceContent);
        var targetFile = Path.Combine(TestOutputPath, "TargetClass.cs");
        var targetContent = await File.ReadAllTextAsync(targetFile);
        Assert.Contains("static int Foo", targetContent);
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
            "UtilClass");

        Assert.Contains("Successfully moved static method", result);
        var targetFile = Path.Combine(TestOutputPath, "UtilClass.cs");
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
