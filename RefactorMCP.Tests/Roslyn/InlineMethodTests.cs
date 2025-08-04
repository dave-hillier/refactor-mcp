using Xunit;

namespace RefactorMCP.Tests;

public partial class RoslynTransformationTests
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
        expected = expected.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
        output = output.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
        Assert.Equal(expected, output.Trim());
    }
}

