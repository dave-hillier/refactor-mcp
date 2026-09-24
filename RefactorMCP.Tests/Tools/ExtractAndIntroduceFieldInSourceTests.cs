using Xunit;

namespace RefactorMCP.Tests.Tools;

public class ExtractAndIntroduceFieldInSourceTests
{
    [Fact]
    public void IntroduceFieldInSource_AddsField()
    {
        var input = @"class Calculator
{
    int CalculateSum()
    {
        return 10 + 20;
    }
}";
        var output = IntroduceFieldTool.IntroduceFieldInSource(input, "5:16-5:23", "calculationResult", "private");
        Assert.Contains("private var calculationResult", output);
        Assert.Contains("return calculationResult;", output);
    }
}
