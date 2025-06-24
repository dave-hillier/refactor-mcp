using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests.ToolsNew;

public class IntroduceFieldToolTests : RefactorMCP.Tests.TestBase
{
    [Fact]
    public async Task IntroduceField_CreatesField()
    {
        const string initialCode = """
using System.Linq;

public class Sample
{
    public double GetAverage(int[] values)
    {
        return values.Sum() / (double)values.Length;
    }
}
""";
        const string expectedCode = """
using System.Linq;

public class Sample
{
    private double _avg;

    public double GetAverage(int[] values)
    {
        _avg = values.Sum() / (double)values.Length;
        return _avg;
    }
}
""";

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "IntroduceField.cs");
        await TestUtilities.CreateTestFile(testFile, initialCode);

        var result = await IntroduceFieldTool.IntroduceField(
            SolutionPath,
            testFile,
            "6:16-6:57",
            "_avg");

        Assert.Contains("Successfully introduced", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("_avg", fileContent);
        Assert.Contains("values.Sum()", fileContent);
    }
}
