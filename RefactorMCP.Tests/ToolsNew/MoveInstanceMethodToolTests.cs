using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RefactorMCP.ConsoleApp.Move;
using Xunit;

namespace RefactorMCP.Tests.ToolsNew;

public class MoveInstanceMethodToolTests : RefactorMCP.Tests.TestBase
{
    [Fact]
    public async Task MoveInstanceMethod_CreatesTargetFile()
    {
        const string initialCode = """
public class A
{
    public int Bar() { return 1; }
}
""";

        const string expectedSource = """
public class A
{
    public int Bar()
    {
        return B.Bar();
    }
}
""";

        const string expectedTarget = """
public class B
{
    public static int Bar()
    {
        return 1;
    }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "MoveInstance.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        var targetFile = Path.Combine(TestOutputPath, "B.cs");
        var result = await MoveMethodTool.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "A",
            new[] { "Bar" },
            "B",
            targetFile,
            Array.Empty<string>(),
            Array.Empty<string>());

        Assert.Contains("Successfully moved", result);
        var sourceContent = await File.ReadAllTextAsync(testFile);
        Assert.DoesNotContain("int Bar() { return 1; }", sourceContent);
        var targetContent = await File.ReadAllTextAsync(targetFile);
        Assert.Contains("static int Bar", targetContent);
    }

    [Fact]
    public async Task MoveInstanceMethod_PublicDependency_ParameterInjection()
    {
        const string initialCode = """
public class Source
{
    public int Value = 1;
    public int Add(int x) { return x + Value; }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "MoveParamPublic.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        var targetFile = Path.Combine(TestOutputPath, "TargetParamPublic.cs");
        var result = await MoveMethodTool.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "Source",
            new[] { "Add" },
            "Target",
            targetFile,
            Array.Empty<string>(),
            new[] { "this" });

        Assert.Contains("Successfully moved", result);
        var sourceContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("Target.Add(this", sourceContent);
        var targetContent = await File.ReadAllTextAsync(targetFile);
        Assert.Contains("Source source", targetContent);
        Assert.Contains("source.Value", targetContent);
    }

    [Fact]
    public async Task MoveInstanceMethod_PublicDependency_ConstructorInjection()
    {
        const string initialCode = """
public class Source
{
    public int Value = 1;
    public int Add() { return Value + 1; }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "MoveCtorPublic.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        var targetFile = Path.Combine(TestOutputPath, "TargetCtorPublic.cs");
        var result = await MoveMethodTool.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "Source",
            new[] { "Add" },
            "Target",
            targetFile,
            new[] { "this" },
            Array.Empty<string>());

        Assert.Contains("Successfully moved", result);
    }

    [Fact]
    public async Task MoveInstanceMethod_PrivateDependency_ParameterInjection()
    {
        const string initialCode = """
public class Source
{
    private int _offset = 2;
    public int Calc(int n) { return n + _offset; }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "MoveParamPrivate.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        var targetFile = Path.Combine(TestOutputPath, "TargetParamPrivate.cs");
        var result = await MoveMethodTool.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "Source",
            new[] { "Calc" },
            "Target",
            targetFile,
            Array.Empty<string>(),
            new[] { "_offset" });

        Assert.Contains("Successfully moved", result);
        var sourceContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("Target.Calc(_offset", sourceContent);
        var targetContent = await File.ReadAllTextAsync(targetFile);
        Assert.Contains("int Calc(int offset", targetContent);
        Assert.Contains("n + offset", targetContent);
    }

    [Fact]
    public async Task MoveInstanceMethod_PrivateDependency_ConstructorInjection()
    {
        const string initialCode = """
public class Source
{
    private int _offset = 2;
    public int Calc() { return _offset + 1; }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "MoveCtorPrivate.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        var targetFile = Path.Combine(TestOutputPath, "TargetCtorPrivate.cs");
        var result = await MoveMethodTool.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "Source",
            new[] { "Calc" },
            "Target",
            targetFile,
            new[] { "_offset" },
            Array.Empty<string>());

        Assert.Contains("Successfully moved", result);
        var targetContent = await File.ReadAllTextAsync(targetFile);
        Assert.Contains("Target(int offset)", targetContent);
        Assert.Contains("_offset", targetContent);
        Assert.Contains("_offset + 1", targetContent);
    }
}
