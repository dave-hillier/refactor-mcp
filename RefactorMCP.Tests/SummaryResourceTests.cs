using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class SummaryResourceTests : TestBase
{
    [Fact]
    public async Task GetSummary_OmitsMethodBodies()
    {
        var result = await SummaryResources.GetSummary(ExampleFilePath, CancellationToken.None);
        
        // Use regex to match method signature with empty body, allowing for flexible whitespace
        // This is more reliable than exact string matching as it handles platform differences in formatting
        var methodSignaturePattern = @"public\s+int\s+Calculate\s*\(\s*int\s+a\s*,\s*int\s+b\s*\)\s*\{\s*\}";
        Assert.Matches(methodSignaturePattern, result);
        
        // Verify that method bodies are actually omitted (should not contain the original implementation)
        Assert.DoesNotContain("throw new ArgumentException", result);
        Assert.DoesNotContain("numbers.Add(result)", result);
        Assert.DoesNotContain("Console.WriteLine($\"Result: {result}\")", result);
        
        // Additional verification that the summary structure is correct
        Assert.Contains("// summary://", result);
        Assert.Contains("// This file omits method bodies for brevity.", result);
    }

    [Fact]
    public async Task GetSummary_FileNotFound_ReturnsMessage()
    {
        var result = await SummaryResources.GetSummary("does_not_exist.cs", CancellationToken.None);
        Assert.StartsWith("// File not found:", result);
    }
}
