using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests.Tools;

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
        await AddToSolutionAsync(testFile, initialCode);

        var result = await IntroduceFieldTool.IntroduceField(
            SolutionPath,
            testFile,
            "7:16-7:52",  // values.Sum() / (double)values.Length
            "_avg");

        Assert.Contains("Successfully introduced", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Equal(expectedCode, fileContent.Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task IntroduceField_SupportsAccessModifiers()
    {
        const string code = """
using System.Linq;

public class Sample
{
    public double GetAverage(int[] values)
    {
        return values.Sum() / (double)values.Length;
    }
}
""";
        var modifiers = new[] { "public", "protected", "internal" };
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        foreach (var modifier in modifiers)
        {
            var file = Path.Combine(TestOutputPath, $"Access_{modifier}.cs");
            await AddToSolutionAsync(file, code);

            var result = await IntroduceFieldTool.IntroduceField(
                SolutionPath,
                file,
                "7:16-7:52",
                $"_{modifier}Field",
                modifier);

            Assert.Contains($"Successfully introduced {modifier} field", result);
            var content = await File.ReadAllTextAsync(file);
            Assert.Contains($"_{modifier}Field", content);
        }
    }

    [Fact]
    public async Task IntroduceField_FieldNameAlreadyExists_ReturnsError()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "DuplicateField.cs");
        await AddToSolutionAsync(testFile, TestUtilities.GetSampleCodeForIntroduceField());

        var ex = await Assert.ThrowsAsync<ModelContextProtocol.McpException>(() => IntroduceFieldTool.IntroduceField(
            SolutionPath,
            testFile,
            "36:20-36:57",  // numbers.Sum() / (double)numbers.Count
            "numbers",
            "private"));

        Assert.Contains("already has a member named 'numbers'", ex.Message);
    }
}
