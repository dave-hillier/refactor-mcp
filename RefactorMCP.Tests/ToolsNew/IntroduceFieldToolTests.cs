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
            await TestUtilities.CreateTestFile(file, code);

            var result = await IntroduceFieldTool.IntroduceField(
                SolutionPath,
                file,
                "6:16-6:57",
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
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForIntroduceField());

        var result = await IntroduceFieldTool.IntroduceField(
            SolutionPath,
            testFile,
            "36:20-36:56",
            "numbers",
            "private");

        Assert.Equal("Error: Field 'numbers' already exists", result);
    }
}
