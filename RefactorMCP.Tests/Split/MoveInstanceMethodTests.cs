using ModelContextProtocol;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace RefactorMCP.Tests;

public class MoveInstanceMethodTests : TestBase
{
    [Fact]
    public async Task MoveInstanceMethod_ReturnsSuccess()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "MoveInstanceMethod.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForMoveInstanceMethod());

        var result = await MoveMethodsTool.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "Calculator",
            "LogOperation",
            "Logger",
            "_logger",
            "field");

        Assert.Contains("Successfully moved", result);
        Assert.Contains("Calculator.LogOperation", result);
        Assert.Contains("Logger", result);
    }
}
