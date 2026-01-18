using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using Xunit;

namespace RefactorMCP.Tests.Tools;

public class MakeStaticThenMoveToolTests : RefactorMCP.Tests.TestBase
{
    [Fact]
    public async Task MakeStaticThenMove_ReturnsSuccess()
    {
        const string initialCode = "public class SourceClass { public string Value = \"x\"; public string GetValueWithSuffix(string suffix){ return Value + suffix; } } public class NewMathUtils { }";

        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.Combine(TestOutputPath, "MakeStaticThenMove.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        var result = await MakeStaticThenMoveTool.MakeStaticThenMove(
            SolutionPath,
            testFile,
            "GetValueWithSuffix",
            "NewMathUtils",
            "source",
            null,
            null,
            CancellationToken.None);

        Assert.Contains("Successfully moved static method", result);
        var newFile = Path.Combine(Path.GetDirectoryName(testFile)!, "NewMathUtils.cs");
        Assert.True(File.Exists(newFile));
    }
}
