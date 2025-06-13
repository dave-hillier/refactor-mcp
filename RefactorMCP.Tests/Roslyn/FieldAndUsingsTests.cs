using Xunit;

namespace RefactorMCP.Tests;

public partial class RoslynTransformationTests
{
    [Fact]
    public void MakeFieldReadonlyInSource_MakesReadonly()
    {
        var input = @"class CurrencyFormatter
{
    private string formatPattern = ""Currency"";

    public CurrencyFormatter()
    {
        Console.WriteLine(formatPattern);
    }
}";
        var expected = @"class CurrencyFormatter
{
    private readonly string formatPattern;

    public CurrencyFormatter()
    {
        Console.WriteLine(formatPattern);
        formatPattern = ""Currency"";
    }
}";
        var output = MakeFieldReadonlyTool.MakeFieldReadonlyInSource(input, "formatPattern");
        Assert.Equal(expected, output);
    }

    [Fact]
    public void CleanupUsingsInSource_RemovesUnusedUsings()
    {
        var input = @"using System;
using System.Text;

public class CleanupSample
{
    public void Say() => Console.WriteLine(""Hi"");
}";
        var expected = @"using System;

public class CleanupSample
{
    public void Say() => Console.WriteLine(""Hi"");
}";
        var output = CleanupUsingsTool.CleanupUsingsInSource(input);
        Assert.Equal(expected, output);
    }

    [Fact]
    public void TransformSetterToInitInSource_ReplacesSetter()
    {
        var input = @"class UserProfile
{
    public string UserName { get; set; } = ""DefaultUser"";
}";
        var expected = @"class UserProfile
{
    public string UserName { get; init; } = ""DefaultUser"";
}";
        var output = TransformSetterToInitTool.TransformSetterToInitInSource(input, "UserName");
        Assert.Equal(expected, output);
    }
}
