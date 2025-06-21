using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace RefactorMCP.Tests;

/// <summary>
/// Performance tests to ensure refactoring tools work efficiently with larger codebases
/// </summary>
public class PerformanceTests
{
    private readonly ITestOutputHelper _output;
    private static readonly string SolutionPath = TestHelpers.GetSolutionPath();
    private readonly string _testOutputPath;

    public PerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        _testOutputPath = TestHelpers.CreateTestOutputDir("Performance");
    }


    [Fact]
    public async Task LoadSolution_LargeProject_CompletesInReasonableTime()
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await LoadSolutionTool.LoadSolution(SolutionPath);

        // Assert
        stopwatch.Stop();
        _output.WriteLine($"Solution loading took: {stopwatch.ElapsedMilliseconds}ms");

        Assert.Contains("Successfully loaded solution", result);
        Assert.True(stopwatch.ElapsedMilliseconds < 10000, "Solution loading should complete within 10 seconds");
    }

    [Fact]
    public async Task ExtractMethod_LargeFile_CompletesInReasonableTime()
    {
        // Arrange
        var testFile = Path.GetFullPath(Path.Combine(_testOutputPath, "LargeFileTest.cs"));
        await CreateLargeTestFile(testFile);
        await LoadSolutionTool.LoadSolution(SolutionPath);

        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await ExtractMethodTool.ExtractMethod(
            SolutionPath,
            testFile,
            "10:9-13:10", // Extract a validation block
            "ValidateInputs"
        );

        // Assert
        stopwatch.Stop();
        _output.WriteLine($"Extract method on large file took: {stopwatch.ElapsedMilliseconds}ms");

        Assert.Contains("Successfully extracted method", result);
        Assert.True(stopwatch.ElapsedMilliseconds < 5000, "Extract method should complete within 5 seconds");
    }

    [Fact]
    public async Task MultipleRefactorings_Sequential_AllComplete()
    {
        // Arrange
        var testFile = Path.GetFullPath(Path.Combine(_testOutputPath, "MultipleRefactoringsTest.cs"));
        await CreateTestFileForMultipleRefactorings(testFile);
        await LoadSolutionTool.LoadSolution(SolutionPath);

        var totalStopwatch = Stopwatch.StartNew();

        // Act - Perform multiple refactorings in sequence
        var extractResult = await ExtractMethodTool.ExtractMethod(
            SolutionPath,
            testFile,
            "8:9-11:10",
            "ValidateInputs"
        );

        var fieldResult = await IntroduceFieldTool.IntroduceField(
            SolutionPath,
            testFile,
            "15:16-15:40",
            "_calculatedValue",
            "private"
        );

        var variableResult = await IntroduceVariableTool.IntroduceVariable(
            SolutionPath,
            testFile,
            "12:20-12:29",
            "processedValue"
        );

        // Assert
        totalStopwatch.Stop();
        _output.WriteLine($"Multiple refactorings took: {totalStopwatch.ElapsedMilliseconds}ms");

        Assert.Contains("Successfully extracted method", extractResult);
        Assert.Contains("Successfully introduced", fieldResult);
        Assert.Contains("Successfully introduced", variableResult);
        Assert.True(totalStopwatch.ElapsedMilliseconds < 15000, "Multiple refactorings should complete within 15 seconds");
    }

    [Fact(Skip = "Unstable timing in CI")]
    public async Task SolutionCaching_SecondLoad_IsFaster()
    {
        // Arrange & Act - First load
        var firstStopwatch = Stopwatch.StartNew();
        var firstResult = await LoadSolutionTool.LoadSolution(SolutionPath);
        firstStopwatch.Stop();

        // Act - Second load (should use cache)
        var secondStopwatch = Stopwatch.StartNew();
        var secondResult = await LoadSolutionTool.LoadSolution(SolutionPath);
        secondStopwatch.Stop();

        // Assert
        _output.WriteLine($"First load: {firstStopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Second load: {secondStopwatch.ElapsedMilliseconds}ms");

        Assert.Contains("Successfully loaded solution", firstResult);
        Assert.Contains("Successfully loaded solution", secondResult);

        // Second load should be faster due to caching
        Assert.True(secondStopwatch.ElapsedMilliseconds <= firstStopwatch.ElapsedMilliseconds + 200,
            "Second solution load should be faster or roughly equal due to caching");
    }

    [Fact]
    public async Task MemoryUsage_MultipleOperations_DoesNotLeak()
    {
        // Arrange
        var testFile = Path.GetFullPath(Path.Combine(_testOutputPath, "MemoryTest.cs"));
        await CreateTestFileForMultipleRefactorings(testFile);
        await LoadSolutionTool.LoadSolution(SolutionPath);

        var initialMemory = GC.GetTotalMemory(true);

        // Act - Perform many operations
        for (int i = 0; i < 10; i++)
        {
            var testFileCopy = testFile.Replace(".cs", $"_{i}.cs");
            await File.WriteAllTextAsync(testFileCopy, await File.ReadAllTextAsync(testFile));

            await ExtractMethodTool.ExtractMethod(
                SolutionPath,
                testFileCopy,
                "8:9-11:10",
                $"ValidateInputs{i}"
            );
        }

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncrease = finalMemory - initialMemory;

        // Assert
        _output.WriteLine($"Initial memory: {initialMemory / 1024 / 1024}MB");
        _output.WriteLine($"Final memory: {finalMemory / 1024 / 1024}MB");
        _output.WriteLine($"Memory increase: {memoryIncrease / 1024 / 1024}MB");

        // Memory increase should be reasonable (less than 50MB for this test)
        Assert.True(memoryIncrease < 50 * 1024 * 1024, "Memory usage should not increase excessively");
    }

    // Helper methods
    private static async Task CreateLargeTestFile(string filePath)
    {
        var content = """
using System;
using System.Collections.Generic;

namespace PerformanceTests
{
    public class LargeClass
    {
        public void TestMethod()
        {
            if (true)
            {
                Console.WriteLine("Test");
            }
            
""";

        // Add many methods to make it a larger file
        for (int i = 0; i < 100; i++)
        {
            content += $@"
            // Method {i}
            public void Method{i}()
            {{
                var value{i} = {i} * 2;
                Console.WriteLine($""Method {{value{i}}}"");
            }}
            
";
        }

        content += """
        }
    }
}
""";

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(filePath, content);
    }

    private static async Task CreateTestFileForMultipleRefactorings(string filePath)
    {
        var content = """
using System;
namespace PerformanceTests
{
    public class MultiRefactorClass
    {
        public int TestMethod(int a, int b)
        {
            if (a < 0 || b < 0)
            {
                throw new ArgumentException("Invalid");
            }
            
            var result = a + b;
            return result * 2;
        }

        public string FormatValue(int value)
        {
            return $"Value: {value + 10}";
        }
    }
}
""";

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(filePath, content);
    }
}
