using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class SummaryResourceTests : TestBase
{
    [Fact]
    public async Task GetSummary_OmitsMethodBodies()
    {
        var result = await SummaryResources.GetSummary(ExampleFilePath);
        Assert.Contains("public int Calculate(int a, int b)\n        {}", result);
        Assert.DoesNotContain("throw new ArgumentException", result);
    }
}
