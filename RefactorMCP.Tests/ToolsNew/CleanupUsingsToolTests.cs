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
}
