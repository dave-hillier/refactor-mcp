using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests.ToolsNew;

public class CleanupUsingsToolTests : RefactorMCP.Tests.TestBase
{
    [Fact]
    public async Task CleanupUsings_RemovesUnusedUsings()
    {
        const string initialCode = """
using System;
using System.Text;

public class CleanupSample
{
    public void Say() => Console.WriteLine("Hi");
}
""";

        const string expectedCode = """
using System;

public class CleanupSample
{
    public void Say() => Console.WriteLine("Hi");
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "CleanupSample.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        var result = await CleanupUsingsTool.CleanupUsings(SolutionPath, testFile);

        Assert.Contains("Removed unused usings", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        var expected = expectedCode.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
        fileContent = fileContent.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
        Assert.Equal(expected, fileContent);
    }

    [Fact]
    public void CleanupUsingsInSource_PreservesRequiredUsings()
    {
        // Test with using statements that require more than just basic system references
        var input = @"using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

public class DataProcessor
{
    public string ProcessData(List<string> data)
    {
        var filtered = data.Where(x => !string.IsNullOrEmpty(x)).ToList();
        return JsonSerializer.Serialize(filtered);
    }
}";
        var output = CleanupUsingsTool.CleanupUsingsInSource(input);

        // Check that no required usings were removed
        // System should be removed
        Assert.DoesNotContain("using System;", output);
        Assert.Contains("using System.Collections.Generic;", output);
        Assert.Contains("using System.Linq;", output);
        Assert.Contains("using System.Text.Json;", output);
    }

    [Fact]
    public async Task CleanupUsings_WithSolution_PreservesRequiredUsings()
    {
        // Test the solution-based scenario that was failing
        const string initialCode = """
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

public class RealWorldExample
{
    public async Task<string> ProcessFileData(string filePath)
    {
        var lines = await File.ReadAllLinesAsync(filePath);
        var data = lines.Where(line => !string.IsNullOrEmpty(line))
                       .Select(line => new { Value = line })
                       .ToList();
        return JsonSerializer.Serialize(data);
    }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "RealWorld.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        var result = await CleanupUsingsTool.CleanupUsings(SolutionPath, testFile);

        Assert.Contains("Removed unused usings", result);
        var fileContent = await File.ReadAllTextAsync(testFile);

        // Verify that required usings are preserved
        Assert.Contains("using System.IO;", fileContent);
        Assert.Contains("using System.Linq;", fileContent);
        Assert.Contains("using System.Text.Json;", fileContent);
        Assert.Contains("using System.Threading.Tasks;", fileContent);

        // System.Collections.Generic might be implicitly used for anonymous types/LINQ
        // System should be removed as it's not explicitly used
        Assert.DoesNotContain("using System;", fileContent);
    }
}
