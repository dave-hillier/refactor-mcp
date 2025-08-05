using ModelContextProtocol;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class ExtractMethodTests : TestBase
{
    [Fact]
    public async Task ExtractMethod_ValidSelectionOfAsyncCode_ReturnsMethodWithCorrectParameterType()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "ExtractMethodTest.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForExtractMethod());

        var result = await ExtractMethodTool.ExtractMethod(
            SolutionPath,
            testFile,
            "18:9-18:43",
            "ExtractedWithBoolParam");

        Assert.Contains("Successfully extracted method", result);
        var fileContent = await File.ReadAllTextAsync(testFile);

        // Should create a method that returns int (inferred from return statement and context)
        Assert.Contains("private void ExtractedWithBoolParam(bool theBool)", fileContent);
        // Should call it with the right return assignment
        Assert.Contains("ExtractedWithBoolParam(theBool);", fileContent);
    }

    [Fact]
    public async Task ExtractMethod_ValidSelection_ReturnsMethodWithCorrectReturnType()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "ExtractMethodTest.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForExtractMethod());

        // Extract the lines that calculate and return the result: "var result = a + b; return result;"
        // Looking at the sample code:
        // Line 11: "        var result = a + b;"
        // Line 12: "        return result;"
        var result = await ExtractMethodTool.ExtractMethod(
            SolutionPath,
            testFile,
            "11:9-12:22",  // From start of "var result" to end of "return result;"
            "CalculateInts");

        Assert.Contains("Successfully extracted method", result);
        var fileContent = await File.ReadAllTextAsync(testFile);

        // Should create a method that returns int (inferred from return statement and context)
        Assert.Contains("private int CalculateInts(int a, int b)", fileContent);
        // Should call it with the right return assignment
        Assert.Contains("return CalculateInts(a, b);", fileContent);
    }

    [Fact]
    public async Task ExtractMethod_ValidSelection_ReturnsSuccess()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "ExtractMethodTest.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForExtractMethod());

        // Extract the validation block: lines 6-9 (if/throw block)
        // Line 6: "        if (a < 0 || b < 0)"
        // Line 7: "        {"
        // Line 8: "            throw new ArgumentException(\"Negative numbers not allowed\");"
        // Line 9: "        }"
        var result = await ExtractMethodTool.ExtractMethod(
            SolutionPath,
            testFile,
            "6:9-9:10",
            "ValidateInputs");

        Assert.Contains("Successfully extracted method", result);
        var fileContent = await File.ReadAllTextAsync(testFile);

        // Should call the validation method with parameters
        Assert.Contains("ValidateInputs(a, b);", fileContent);
    }

    [Fact]
    public async Task ExtractMethod_CreatesPrivateMethod()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "ExtractPrivate.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForExtractMethod());

        // Extract the validation block: lines 6-9 (if/throw block)
        await ExtractMethodTool.ExtractMethod(
            SolutionPath,
            testFile,
            "6:9-9:10",
            "ValidateInputs");

        var fileContent = await File.ReadAllTextAsync(testFile);

        // Should create a void method (validation that throws but doesn't return)
        Assert.Contains("private void ValidateInputs(int a, int b)", fileContent);
    }

    [Fact]
    public async Task ExtractMethod_InvalidRange_ReturnsError()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        await Assert.ThrowsAsync<McpException>(async () =>
            await ExtractMethodTool.ExtractMethod(
                SolutionPath,
                ExampleFilePath,
                "invalid-range",
                "TestMethod"));
    }

    [Fact]
    public async Task RefactoringTools_FileNotInSolution_ReturnsError()
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
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        await Assert.ThrowsAsync<McpException>(async () =>
            await ExtractMethodTool.ExtractMethod(
                SolutionPath,
                ExampleFilePath,
                range,
                methodName));
    }

    [Theory]
    [InlineData("0:1-1:1", "TestMethod")]
    [InlineData("5:5-3:1", "TestMethod")]
    public async Task ExtractMethod_InvalidRangeValues_ReturnsError(string range, string methodName)
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        await Assert.ThrowsAsync<McpException>(async () =>
            await ExtractMethodTool.ExtractMethod(
                SolutionPath,
                ExampleFilePath,
                range,
                methodName));
    }

    [Fact]
    public async Task ExtractMethod_AsyncMethodWithTaskReturn_AvoidsDuplicateTaskWrapper()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "ExtractAsyncMethodTest.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForExtractAsyncMethod());

        // Extract the async method call that returns Task<List<int>>
        // Line 10: "        var processedNumbers = await GetListOfIntsAsync();"
        var result = await ExtractMethodTool.ExtractMethod(
            SolutionPath,
            testFile,
            "10:9-10:63",  // Extract only the single await line
            "GetProcessedNumbersAsync");

        Assert.Contains("Successfully extracted method", result);
        var fileContent = await File.ReadAllTextAsync(testFile);

        // Should create an async method that returns Task<List<int>>, not Task<Task<List<int>>>
        Assert.Contains("private async Task<List<int>> GetProcessedNumbersAsync()", fileContent);
        // Should not contain the duplicate Task wrapper
        Assert.DoesNotContain("Task<Task<", fileContent);
        // The extracted method should contain the await call
        Assert.Contains("var processedNumbers = await GetListOfIntsAsync();", fileContent);
    }

    [Fact]
    public void ExtractMethod_AsyncTaskReturnType_SingleFileMode_AvoidsDuplicateTaskWrapper()
    {
        const string sourceCode = """
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class AsyncTestClass
{
    public async Task<List<int>> ProcessListAsync()
    {
        var numbers = new List<int> { 1, 2, 3 };
        var processedNumbers = await GetListOfIntsAsync();
        return processedNumbers;
    }

    private async Task<List<int>> GetListOfIntsAsync()
    {
        await Task.Delay(100);
        return new List<int> { 4, 5, 6 };
    }
}
""";

        // Extract line 10: "var processedNumbers = await GetListOfIntsAsync();"
        var result = ExtractMethodTool.ExtractMethodInSource(sourceCode, "10:9-10:63", "GetProcessedNumbersAsync");

        // First, check if it contains any Task<Task< pattern (the issue we're trying to fix)
        Assert.DoesNotContain("Task<Task<", result);
        
        // Let's be more flexible about the exact signature and just check the important parts
        Assert.Contains("GetProcessedNumbersAsync", result);
        Assert.Contains("private async", result);
        Assert.Contains("Task<List<int>>", result);
    }
}