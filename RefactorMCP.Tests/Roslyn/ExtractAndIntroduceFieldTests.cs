using Xunit;

namespace RefactorMCP.Tests;

public partial class RoslynTransformationTests
{
    [Fact]
    public void ExtractMethodInSource_CreatesMethod()
    {
        var input = @"class MessageHandler
{
    void ProcessMessage()
    {
        Console.WriteLine(""Processing message"");
    }
}";
        var expected = @"class MessageHandler
{
    void ProcessMessage()
    {
        DisplayProcessingMessage();
    }

    private void DisplayProcessingMessage()
    {
        Console.WriteLine(""Processing message"");
    }
}";
        var output = ExtractMethodTool.ExtractMethodInSource(input, "5:9-5:49", "DisplayProcessingMessage");
        Assert.Equal(expected, output);
    }

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
        var expected = @"class Calculator
{
    private var calculationResult = 10 + 20;

    int CalculateSum()
    {
        return calculationResult;
    }
}
";
        var output = IntroduceFieldTool.IntroduceFieldInSource(input, "5:16-5:23", "calculationResult", "private");
        Assert.Contains("private var calculationResult", output);
        Assert.Contains("return calculationResult;", output);
    }
}
