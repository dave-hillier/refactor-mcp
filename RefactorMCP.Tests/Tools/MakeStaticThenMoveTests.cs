using ModelContextProtocol;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class MakeStaticThenMoveTests : TestBase
{
    [Fact]
    public async Task MakeStaticThenMove_ReturnsSuccess()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.Combine(TestOutputPath, "MakeStaticThenMove.cs");
        await TestUtilities.CreateTestFile(testFile, @"public class SourceClass
{
    public string Value = ""x"";
    public string GetValueWithSuffix(string suffix)
    {
        return Value + suffix;
    }
}

public class NewMathUtils { }");

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
