using ModelContextProtocol;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class BatchMoveMethodsTests : TestBase
{
    [Fact]
    public async Task BatchMoveMethods_CrossFileMovesAllMethods()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath);
        var testFile = Path.Combine(TestOutputPath, "BatchMove.cs");
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

        var json = JsonSerializer.Serialize(ops);
        var result = await BatchMoveMethodsTool.BatchMoveMethods(
            SolutionPath, testFile, json);

        Assert.Contains("Successfully moved 3 methods", result);
        Assert.True(File.Exists(target1));
        Assert.True(File.Exists(target2));
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
