using ModelContextProtocol;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class MoveMultipleMethodsTests : TestBase
{
    [Fact]
    public async Task MoveMultipleMethods_CrossFileMovesAllMethods()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "MultiMove.cs");
        await TestUtilities.CreateTestFile(testFile, GetSampleCode());

        var target1 = Path.Combine(TestOutputPath, "Target1.cs");
        var target2 = Path.Combine(TestOutputPath, "Target2.cs");

        var ops = new[]
        {
            new MoveMultipleMethodsTool.MoveOperation
            {
                SourceClass = "SourceClass",
                Method = "A",
                TargetClass = "Target1",
                AccessMember = "t1",
                AccessMemberType = "field",
                IsStatic = false,
                TargetFile = target1
            },
            new MoveMultipleMethodsTool.MoveOperation
            {
                SourceClass = "SourceClass",
                Method = "B",
                TargetClass = "Target1",
                AccessMember = "t1",
                AccessMemberType = "field",
                IsStatic = false,
                TargetFile = target1
            },
            new MoveMultipleMethodsTool.MoveOperation
            {
                Method = "C",
                TargetClass = "Target2",
                IsStatic = true,
                TargetFile = target2
            }
        };

        var result = await MoveMultipleMethodsTool.MoveMultipleMethods(
            SolutionPath, testFile, ops);

        Assert.Contains("Successfully moved 3 methods", result);

        Assert.True(File.Exists(target1));
        Assert.True(File.Exists(target2));

        var target1Content = await File.ReadAllTextAsync(target1);
        Assert.Contains("class Target1", target1Content);
        Assert.Contains("void A(SourceClass", target1Content);
        Assert.Contains("void B()", target1Content);

        var target2Content = await File.ReadAllTextAsync(target2);
        Assert.Contains("class Target2", target2Content);
        Assert.Contains("static void C()", target2Content);
    }

    [Fact]
    public async Task MoveMultipleMethods_UsesDefaultTargetFileWhenMissing()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "DefaultTargetTest.cs");
        await TestUtilities.CreateTestFile(testFile, GetSampleCode());

        var ops = new[]
        {
            new MoveMultipleMethodsTool.MoveOperation
            {
                SourceClass = "SourceClass",
                Method = "A",
                TargetClass = "Target",
                AccessMember = "t",
                AccessMemberType = "field",
                IsStatic = false
            },
            new MoveMultipleMethodsTool.MoveOperation
            {
                SourceClass = "SourceClass",
                Method = "B",
                TargetClass = "Target",
                AccessMember = "t",
                AccessMemberType = "field",
                IsStatic = false
            }
        };

        var targetFile = Path.Combine(TestOutputPath, "Target.cs");
        var result = await MoveMultipleMethodsTool.MoveMultipleMethods(
            SolutionPath, testFile, ops, targetFile);

        Assert.Contains("Successfully moved 2 methods", result);
        Assert.True(File.Exists(targetFile));
        var content = await File.ReadAllTextAsync(targetFile);
        Assert.Contains("class Target", content);
        Assert.Contains("void A(SourceClass", content);
        Assert.Contains("void B()", content);
    }

    private static string GetSampleCode() => """
using System;

public class SourceClass
{
    public void A() { B(); }
    public void B() { Console.WriteLine("B"); }
    public static void C() { Console.WriteLine("C"); }
}

public class Target1 { }
public class Target2 { }
""";
}
