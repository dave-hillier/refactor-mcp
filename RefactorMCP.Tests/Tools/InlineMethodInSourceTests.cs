using Xunit;

namespace RefactorMCP.Tests.Tools;

public class InlineMethodInSourceTests
{
    [Fact]
    public void InlineMethodInSource_ReplacesInvocationWithBody()
    {
        var input = @"class InlineSample
{
    private void Helper()
    {
        Console.WriteLine(""Hi"");
    }

    public void Call()
    {
        Helper();
        Console.WriteLine(""Done"");
    }
}";
        var expected = "class InlineSample\n{\n\n    public void Call()\n    {\n        Console.WriteLine(\"Hi\");\n        Console.WriteLine(\"Done\");\n    }\n}";
        var output = InlineMethodTool.InlineMethodInSource(input, "Helper");
        Assert.Equal(expected, output.Trim());
    }
}

