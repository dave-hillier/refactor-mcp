using ModelContextProtocol;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests.Tools;

public class TransformSetterToInitToolTests : RefactorMCP.Tests.TestBase
{
    [Fact]
    public async Task TransformSetter_InvalidProperty_ReturnsError()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        await Assert.ThrowsAsync<McpException>(async () =>
            await TransformSetterToInitTool.TransformSetterToInit(
                SolutionPath,
                ExampleFilePath,
                "Nonexistent"));
    }
}
