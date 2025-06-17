using ModelContextProtocol;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class IntroduceFieldTests : TestBase
{
    [Fact]
    public async Task IntroduceField_ValidExpression_ReturnsSuccess()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "IntroduceFieldTest.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForIntroduceField());

        var result = await IntroduceFieldTool.IntroduceField(
            SolutionPath,
            testFile,
            "4:16-4:58",
            "_averageValue",
            "private");

        Assert.Contains("Successfully introduced", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("_averageValue", fileContent);
    }

    [Fact]
    public async Task IntroduceField_WithPublicModifier_ReturnsSuccess()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "IntroduceFieldPublicTest.cs");
        await TestUtilities.CreateTestFile(testFile, TestUtilities.GetSampleCodeForIntroduceField());

        var result = await IntroduceFieldTool.IntroduceField(
            SolutionPath,
            testFile,
            "4:16-4:58",
            "_publicField",
            "public");

        Assert.Contains("Successfully introduced public field", result);
        var fileContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("_publicField", fileContent);
    }

    [Fact]
    public async Task IntroduceField_DifferentAccessModifiers_ReturnsCorrectModifier()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "AccessModifierTest.cs");

        var accessModifiers = new[] { "private", "public", "protected", "internal" };
        foreach (var modifier in accessModifiers)
        {
            var modifierTestFile = testFile.Replace(".cs", $"_{modifier}.cs");
            await TestUtilities.CreateTestFile(modifierTestFile, TestUtilities.GetSampleCodeForIntroduceField());

            var result = await IntroduceFieldTool.IntroduceField(
                SolutionPath,
                modifierTestFile,
                "36:20-36:56",
                $"_{modifier}Field",
                modifier);

            Assert.Contains($"Successfully introduced {modifier} field", result);
            var fileContent = await File.ReadAllTextAsync(modifierTestFile);
            Assert.Contains($"_{modifier}Field", fileContent);
        }
    }

    [Fact]
    public async Task IntroduceField_FieldNameAlreadyExists_ReturnsError()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "IntroduceFieldDuplicate.cs");
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
