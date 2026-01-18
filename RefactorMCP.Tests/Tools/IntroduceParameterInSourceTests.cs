using Xunit;

namespace RefactorMCP.Tests.Tools;

public class IntroduceParameterInSourceTests
{
    [Fact]
    public void IntroduceParameterInSource_AddsParameter()
    {
        var input = @"class MathHelper
{
    int AddNumbers()
    {
        return 1 + 2;
    }

    int Calculate()
    {
        return AddNumbers();
    }
}";
        var expected = @"class MathHelper
{
    int AddNumbers(object result)
    {
        return result;
    }

    int Calculate()
    {
        return AddNumbers(1 + 2);
    }
}";
        var output = IntroduceParameterTool.IntroduceParameterInSource(input, "AddNumbers", "5:16-5:20", "result");
        Assert.Equal(expected, output);
    }
}
