using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests.Tools;

public class IntroduceParameterToolTests : RefactorMCP.Tests.TestBase
{
    [Fact]
    public async Task IntroduceParameter_InvalidMethod_ReturnsError()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var result = await IntroduceParameterTool.IntroduceParameter(
            SolutionPath,
            ExampleFilePath,
            "Nonexistent",
            "1:1-1:2",
            "param");
        Assert.Equal("Error: No method named 'Nonexistent' found", result);
    }
}
