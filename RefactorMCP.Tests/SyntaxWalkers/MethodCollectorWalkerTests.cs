using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using RefactorMCP.ConsoleApp.SyntaxWalkers;
using Xunit;

namespace RefactorMCP.Tests.SyntaxWalkers;

public class MethodCollectorWalkerTests
{
    [Fact]
    public void MethodCollectorWalker_CollectsSpecifiedMethods()
    {
        var code = @"class A { void X(){} void Y(){} } class B { void X(){} }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new MethodCollectorWalker(new HashSet<string> { "A.X", "B.X" });
        walker.Visit(tree.GetRoot());
        Assert.Contains("A.X", walker.Methods.Keys);
        Assert.Contains("B.X", walker.Methods.Keys);
        Assert.DoesNotContain("A.Y", walker.Methods.Keys);
    }
}
