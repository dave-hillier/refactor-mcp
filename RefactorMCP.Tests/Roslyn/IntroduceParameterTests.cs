using Xunit;

namespace RefactorMCP.Tests;

public partial class RoslynTransformationTests
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

    [Fact]
    public void IntroduceParameterInSource_HandlesThisExpression()
    {
        var input = @"class A
{
    void MethodBefore()
    {
        var m = new T(this);
    }
}

class T { public T(A a) {} }";

        var expected = @"class A
{
    void MethodBefore(object arg)
    {
        var m = new T(arg);
    }
}

class T { public T(A a) {} }";

        var output = IntroduceParameterTool.IntroduceParameterInSource(input, "MethodBefore", "4:23-4:26", "arg");
        Assert.Equal(expected, output);
    }
}
