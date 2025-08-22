using Microsoft.VisualStudio.TestPlatform.Utilities;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class SummaryResourceTests : TestBase
{
    private const string ExpectedSummarySnippet = "public int Calculate(int a, int b)\n        {}";

    [Fact]
    public async Task GetSummary_OmitsMethodBodies()
    {
        var result = await SummaryResources.GetSummary(ExampleFilePath, CancellationToken.None);
        var expected = ExpectedSummarySnippet.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
        result = result.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
        Assert.Contains(ExpectedSummarySnippet, result);
        Assert.DoesNotContain("throw new ArgumentException", result);
    }

    [Fact]
    public async Task GetSummary_FileNotFound_ReturnsMessage()
    {
        var result = await SummaryResources.GetSummary("does_not_exist.cs", CancellationToken.None);
        Assert.StartsWith("// File not found:", result);
    }
}
