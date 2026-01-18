using Xunit;

namespace RefactorMCP.Tests.Tools;

public class ConstructorInjectionInSourceTests
{
    [Fact]
    public void ConstructorInjectionInSource_ConvertsParameter()
    {
        var input = @"class C{ int M(int x){ return x+1; } void Call(){ M(1); } }";
        var pairs = new[] { new ConstructorInjectionTool.MethodParameterPair("M", "x") };
        var output = ConstructorInjectionTool.ConvertInSource(input, pairs, false);
        Assert.Contains("private readonly int _x", output);
        Assert.Contains("public C(int x)", output);
        Assert.Contains("int M()", output);
        Assert.DoesNotContain("M(1)", output);
    }

    [Fact]
    public void ConstructorInjectionInSource_MultiplePairs()
    {
        var input = @"class C{ int A(int x){return x;} int B(int y){return y;} void C1(){A(1);B(2);} }";
        var pairs = new[]
        {
            new ConstructorInjectionTool.MethodParameterPair("A","x"),
            new ConstructorInjectionTool.MethodParameterPair("B","y")
        };
        var output = ConstructorInjectionTool.ConvertInSource(input, pairs, false);
        Assert.Contains("_x", output);
        Assert.Contains("_y", output);
        Assert.DoesNotContain("A(1)", output);
        Assert.DoesNotContain("B(2)", output);
    }
}
