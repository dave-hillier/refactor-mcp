using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests.Tools;

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
        Assert.Equal(expectedCode, fileContent.Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task CleanupUsings_DoesNotRemoveUsingsFromOtherFiles()
    {
        // FileA has an unused using (System.Text)
        const string fileACode = """
using System;
using System.Text;

public class FileA
{
    public void Say() => Console.WriteLine("Hi");
}
""";

        // FileB uses System.Text - it should NOT be removed
        const string fileBCode = """
using System;
using System.Text;

public class FileB
{
    public void Say() => Console.WriteLine(Encoding.UTF8.EncodingName);
}
""";

        // FileB should remain unchanged since System.Text is used
        const string expectedFileBCode = """
using System;
using System.Text;

public class FileB
{
    public void Say() => Console.WriteLine(Encoding.UTF8.EncodingName);
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var fileA = Path.Combine(TestOutputPath, "FileA.cs");
        var fileB = Path.Combine(TestOutputPath, "FileB.cs");
        await TestUtilities.CreateTestFile(fileA, fileACode);
        await TestUtilities.CreateTestFile(fileB, fileBCode);

        // Clean up FileB - should NOT remove System.Text even though FileA has it unused
        var result = await CleanupUsingsTool.CleanupUsings(SolutionPath, fileB);

        var fileBContent = await File.ReadAllTextAsync(fileB);
        Assert.Equal(expectedFileBCode.Replace("\r\n", "\n"), fileBContent.Replace("\r\n", "\n"));
    }
}
