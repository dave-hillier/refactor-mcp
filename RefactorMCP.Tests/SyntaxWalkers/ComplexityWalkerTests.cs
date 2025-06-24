using Microsoft.CodeAnalysis.CSharp;
using RefactorMCP.ConsoleApp.SyntaxWalkers;
using Xunit;

namespace RefactorMCP.Tests.SyntaxWalkers;

public class ComplexityWalkerTests
{
    [Fact]
    public void ComplexityWalker_ComputesComplexityAndDepth()
    {
        var code = @"class C
{
    void M()
    {
        if (true)
        {
            for (int i = 0; i < 10; i++)
            {
                if (false) { }
            }
        }
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new ComplexityWalker();
        walker.Visit(tree.GetRoot());
        Assert.Equal(4, walker.Complexity);
        Assert.Equal(3, walker.MaxDepth);
    }
}
