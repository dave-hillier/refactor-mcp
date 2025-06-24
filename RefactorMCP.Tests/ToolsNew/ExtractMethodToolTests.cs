using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using Xunit;

namespace RefactorMCP.Tests.ToolsNew;

public class ExtractMethodToolTests : RefactorMCP.Tests.TestBase
{
    [Fact]
    public async Task ExtractMethod_CreatesNewMethod()
    {
        const string initialCode = """
using System;

public class Sample
{
    public int Calc(int a, int b)
    {
        if (a < 0 || b < 0)
        {
            throw new ArgumentException();
        }
        var result = a + b;
        return result;
    }
}
""";

        const string expectedCode = """
using System;

public class Sample
{
    public int Calc(int a, int b)
    {
        ValidateInputs();
        var result = a + b;
        return result;
    }

    private void ValidateInputs()
    {
        if (a < 0 || b < 0)
        {
            throw new ArgumentException();
        }
    }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "ExtractMethod.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        var result = await ExtractMethodTool.ExtractMethod(
            SolutionPath,
            testFile,
            "6:9-9:10",
            "ValidateInputs");

        Assert.Contains("Successfully extracted method", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Equal(expectedCode, fileContent.Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task ExtractMethod_InvalidRange_ReturnsError()
    {
        const string initialCode = """
using System;

public class Sample
{
    public int Calc(int a, int b)
    {
        return a + b;
    }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "InvalidRange.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        await Assert.ThrowsAsync<McpException>(async () =>
            await ExtractMethodTool.ExtractMethod(
                SolutionPath,
                testFile,
                "invalid-range",
                "TestMethod"));
    }

    [Fact]
    public async Task ExtractMethod_FileNotInSolution_ReturnsError()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        await Assert.ThrowsAsync<McpException>(async () =>
            await ExtractMethodTool.ExtractMethod(
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
        const string initialCode = """
public class A { public void M() { } }
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "InvalidFormat.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        await Assert.ThrowsAsync<McpException>(async () =>
            await ExtractMethodTool.ExtractMethod(
                SolutionPath,
                testFile,
                range,
                methodName));
    }

    [Theory]
    [InlineData("0:1-1:1", "TestMethod")]
    [InlineData("5:5-3:1", "TestMethod")]
    public async Task ExtractMethod_InvalidRangeValues_ReturnsError(string range, string methodName)
    {
        const string initialCode = """
public class A { public void M() { } }
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "InvalidValues.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        await Assert.ThrowsAsync<McpException>(async () =>
            await ExtractMethodTool.ExtractMethod(
                SolutionPath,
                testFile,
                range,
                methodName));
    }
}
