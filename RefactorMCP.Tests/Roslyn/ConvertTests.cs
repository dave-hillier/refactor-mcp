using Xunit;

namespace RefactorMCP.Tests;

public partial class RoslynTransformationTests
{
    [Fact]
    public void ConvertToExtensionMethodInSource_TransformsMethod()
    {
        var input = @"class StringProcessor
{
    void FormatText()
    {
        Console.WriteLine(""Hello"");
    }
}";
        var expected = @"class StringProcessor
{
    void FormatText()
    {
        StringProcessorExtensions.FormatText(this);
    }
}

public static class StringProcessorExtensions
{
    static void FormatText(this StringProcessor stringProcessor)
    {
        Console.WriteLine(""Hello"");
    }
}";
        var output = ConvertToExtensionMethodTool.ConvertToExtensionMethodInSource(input, "FormatText", null);
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void ConvertToExtensionMethodInSource_AppendsToExistingClass()
    {
        var input = @"class StringProcessor
{
    void FormatText()
    {
        Console.WriteLine(""Hello"");
    }
}

public static class StringProcessorExtensions
{
}
";
        var expected = @"class StringProcessor
{
    void FormatText()
    {
        StringProcessorExtensions.FormatText(this);
    }
}

public static class StringProcessorExtensions
{
    static void FormatText(this StringProcessor stringProcessor)
    {
        Console.WriteLine(""Hello"");
    }
}";
        var output = ConvertToExtensionMethodTool.ConvertToExtensionMethodInSource(input, "FormatText", "StringProcessorExtensions");
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void ConvertToStaticWithInstanceInSource_TransformsMethod()
    {
        var input = @"class DataProcessor
{
    int dataCount;
    int GetDataCount()
    {
        return dataCount;
    }
}";
        var expected = @"class DataProcessor
{
    int dataCount;

    static int GetDataCount(DataProcessor instance)
    {
        return instance.dataCount;
    }
}";
        var output = ConvertToStaticWithInstanceTool.ConvertToStaticWithInstanceInSource(input, "GetDataCount", "instance");
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void ConvertToStaticWithParametersInSource_TransformsMethod()
    {
        var input = @"class Calculator
{
    int multiplier;
    int MultiplyValue()
    {
        return multiplier;
    }
}";
        var expected = @"class Calculator
{
    int multiplier;

    static int MultiplyValue(int multiplier)
    {
        return multiplier;
    }
}";
        var output = ConvertToStaticWithParametersTool.ConvertToStaticWithParametersInSource(input, "MultiplyValue");
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void ConvertToStaticWithParametersInSource_TrimsUnderscorePrefix()
    {
        var input = @"class Calculator
{
    int _multiplier;
    int MultiplyValue()
    {
        return _multiplier;
    }
}";
        var expected = @"class Calculator
{
    int _multiplier;

    static int MultiplyValue(int multiplier)
    {
        return multiplier;
    }
}";
        var output = ConvertToStaticWithParametersTool.ConvertToStaticWithParametersInSource(input, "MultiplyValue");
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void ConvertToStaticWithInstanceInSource_HandlesExplicitInterface()
    {
        var input = @"public class cResRoom : IRoomPick
{
    object IRoomPick.GetResProfile(int id) => GetResProfile(id);
    public object GetResProfile(int id) => null;
}

public interface IRoomPick
{
    object GetResProfile(int id);
}";
        var expected = @"public class cResRoom : IRoomPick
{
    public static object GetResProfile(cResRoom room, int id) => room.GetResProfile(id);
    public object GetResProfile(int id) => null;
}

public interface IRoomPick
{
    object GetResProfile(int id);
}";
        var output = ConvertToStaticWithInstanceTool.ConvertToStaticWithInstanceInSource(input, "GetResProfile", "room");
        Assert.Equal(expected, output.Trim());
        Assert.DoesNotContain("IRoomPick.", output);
    }
}
