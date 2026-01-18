using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RefactorMCP.ConsoleApp.Move;
using Xunit;

namespace RefactorMCP.Tests.Tools;

public class MoveMethodsFileToolTests : RefactorMCP.Tests.TestBase
{
    [Fact]
    public async Task MoveStaticMethodInFile_CreatesNewFileAndStub()
    {
        var testFile = Path.Combine(TestOutputPath, "StaticFile.cs");
        await TestUtilities.CreateTestFile(testFile, "public class A { public static int Foo(){ return 1; } } public class B { }");

        var result = await MoveMethodFileService.MoveStaticMethodInFile(
            testFile,
            "Foo",
            "B");

        Assert.Contains("Successfully moved static method", result);
        var targetFile = Path.Combine(Path.GetDirectoryName(testFile)!, "B.cs");
        Assert.True(File.Exists(targetFile));

        var sourceContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("return B.Foo()", sourceContent);
        var targetContent = await File.ReadAllTextAsync(targetFile);
        Assert.Contains("class B", targetContent);
        Assert.Contains("static int Foo", targetContent);
    }

    [Fact]
    public async Task MoveStaticMethodInFile_SameFileAddsMethodAndStub()
    {
        var testFile = Path.Combine(TestOutputPath, "StaticSameFile.cs");
        await TestUtilities.CreateTestFile(testFile, "public class A { public static int Foo(){ return 1; } } public class B { }");

        var result = await MoveMethodFileService.MoveStaticMethodInFile(
            testFile,
            "Foo",
            "B",
            testFile);

        Assert.Contains("Successfully moved static method", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("class B", fileContent);
        Assert.Contains("static int Foo", fileContent);
        Assert.Contains("return B.Foo()", fileContent);
    }

    [Fact]
    public async Task MoveInstanceMethodInFile_CreatesNewFileAndStub()
    {
        var testFile = Path.Combine(TestOutputPath, "InstanceFile.cs");
        await TestUtilities.CreateTestFile(testFile, "public class A { public int Bar(){ return 1; } } public class B { }");

        var targetFile = Path.Combine(Path.GetDirectoryName(testFile)!, "B.cs");
        var result = await MoveMethodFileService.MoveInstanceMethodInFile(
            testFile,
            "A",
            "Bar",
            Array.Empty<string>(),
            Array.Empty<string>(),
            "B",
            "",
            "",
            targetFile,
            progress: null,
            cancellationToken: CancellationToken.None);

        Assert.Contains("Successfully moved instance method", result);
        Assert.Contains("made static", result);
        Assert.True(File.Exists(targetFile));

        var sourceContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("return B.Bar()", sourceContent);
        var targetContent = await File.ReadAllTextAsync(targetFile);
        Assert.Contains("class B", targetContent);
        Assert.Contains("static int Bar", targetContent);
    }

    [Fact]
    public async Task MoveInstanceMethodInFile_SameFileAddsMethodAndStub()
    {
        var testFile = Path.Combine(TestOutputPath, "InstanceSameFile.cs");
        await TestUtilities.CreateTestFile(testFile, "public class A { public int Bar(){ return 1; } } public class B { }");

        var result = await MoveMethodFileService.MoveInstanceMethodInFile(
            testFile,
            "A",
            "Bar",
            Array.Empty<string>(),
            Array.Empty<string>(),
            "B",
            "",
            "",
            testFile,
            progress: null,
            cancellationToken: CancellationToken.None);

        Assert.Contains("Successfully moved instance method", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("class B", fileContent);
        Assert.Contains("static int Bar", fileContent);
        Assert.Contains("return B.Bar()", fileContent);
    }
}
