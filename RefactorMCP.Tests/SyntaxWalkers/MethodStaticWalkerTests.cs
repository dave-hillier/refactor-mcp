using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using RefactorMCP.ConsoleApp.SyntaxWalkers;
using Xunit;

namespace RefactorMCP.Tests.SyntaxWalkers;

public class MethodStaticWalkerTests
{
    [Fact]
    public void MethodStaticWalker_DetectsStaticMethod()
    {
        var code = @"
class C
{
    public static void StaticMethod() { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new MethodStaticWalker(new[] { "StaticMethod" });
        walker.Visit(tree.GetRoot());

        Assert.True(walker.IsStaticMap["StaticMethod"]);
    }

    [Fact]
    public void MethodStaticWalker_DetectsInstanceMethod()
    {
        var code = @"
class C
{
    public void InstanceMethod() { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new MethodStaticWalker(new[] { "InstanceMethod" });
        walker.Visit(tree.GetRoot());

        Assert.False(walker.IsStaticMap["InstanceMethod"]);
    }

    [Fact]
    public void MethodStaticWalker_HandlesMixedMethods()
    {
        var code = @"
class C
{
    public static void StaticOne() { }
    public void InstanceOne() { }
    private static void StaticTwo() { }
    private void InstanceTwo() { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new MethodStaticWalker(new[] { "StaticOne", "InstanceOne", "StaticTwo", "InstanceTwo" });
        walker.Visit(tree.GetRoot());

        Assert.True(walker.IsStaticMap["StaticOne"]);
        Assert.False(walker.IsStaticMap["InstanceOne"]);
        Assert.True(walker.IsStaticMap["StaticTwo"]);
        Assert.False(walker.IsStaticMap["InstanceTwo"]);
    }

    [Fact]
    public void MethodStaticWalker_IgnoresUnrequestedMethods()
    {
        var code = @"
class C
{
    public void MethodA() { }
    public void MethodB() { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new MethodStaticWalker(new[] { "MethodA" });
        walker.Visit(tree.GetRoot());

        Assert.Single(walker.IsStaticMap);
        Assert.Contains("MethodA", walker.IsStaticMap.Keys);
        Assert.DoesNotContain("MethodB", walker.IsStaticMap.Keys);
    }

    [Fact]
    public void MethodStaticWalker_HandlesEmptyMethodList()
    {
        var code = @"
class C
{
    public void Method() { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new MethodStaticWalker(Enumerable.Empty<string>());
        walker.Visit(tree.GetRoot());

        Assert.Empty(walker.IsStaticMap);
    }

    [Fact]
    public void MethodStaticWalker_HandlesNestedClasses()
    {
        var code = @"
class Outer
{
    public static void OuterStatic() { }

    class Inner
    {
        public static void InnerStatic() { }
        public void InnerInstance() { }
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new MethodStaticWalker(new[] { "OuterStatic", "InnerStatic", "InnerInstance" });
        walker.Visit(tree.GetRoot());

        Assert.True(walker.IsStaticMap["OuterStatic"]);
        Assert.True(walker.IsStaticMap["InnerStatic"]);
        Assert.False(walker.IsStaticMap["InnerInstance"]);
    }

    [Fact]
    public void MethodStaticWalker_HandlesMethodNotFound()
    {
        var code = @"
class C
{
    public void ExistingMethod() { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new MethodStaticWalker(new[] { "NonExistentMethod" });
        walker.Visit(tree.GetRoot());

        Assert.Empty(walker.IsStaticMap);
    }

    [Fact]
    public void MethodStaticWalker_HandlesOverloadedMethods()
    {
        var code = @"
class C
{
    public static void Method() { }
    public void Method(int x) { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new MethodStaticWalker(new[] { "Method" });
        walker.Visit(tree.GetRoot());

        // The walker will find the first matching method by name
        Assert.Contains("Method", walker.IsStaticMap.Keys);
    }
}
