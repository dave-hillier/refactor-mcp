using Microsoft.CodeAnalysis.CSharp;
using RefactorMCP.ConsoleApp.SyntaxWalkers;
using Xunit;

namespace RefactorMCP.Tests.SyntaxWalkers;

public class StaticFieldNameWalkerTests
{
    [Fact]
    public void StaticFieldNameWalker_CollectsStaticFields()
    {
        var code = @"
class C
{
    private static int staticField;
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new StaticFieldNameWalker();
        walker.Visit(tree.GetRoot());

        Assert.Single(walker.Names);
        Assert.Contains("staticField", walker.Names);
    }

    [Fact]
    public void StaticFieldNameWalker_IgnoresInstanceFields()
    {
        var code = @"
class C
{
    private int instanceField;
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new StaticFieldNameWalker();
        walker.Visit(tree.GetRoot());

        Assert.Empty(walker.Names);
    }

    [Fact]
    public void StaticFieldNameWalker_HandlesMultipleStaticFieldDeclarators()
    {
        var code = @"
class C
{
    private static int a, b, c;
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new StaticFieldNameWalker();
        walker.Visit(tree.GetRoot());

        Assert.Equal(3, walker.Names.Count);
        Assert.Contains("a", walker.Names);
        Assert.Contains("b", walker.Names);
        Assert.Contains("c", walker.Names);
    }

    [Fact]
    public void StaticFieldNameWalker_MixedStaticAndInstance()
    {
        var code = @"
class C
{
    private static int staticField1;
    private int instanceField;
    private static int staticField2;
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new StaticFieldNameWalker();
        walker.Visit(tree.GetRoot());

        Assert.Equal(2, walker.Names.Count);
        Assert.Contains("staticField1", walker.Names);
        Assert.Contains("staticField2", walker.Names);
        Assert.DoesNotContain("instanceField", walker.Names);
    }

    [Fact]
    public void StaticFieldNameWalker_HandlesStaticReadonly()
    {
        var code = @"
class C
{
    private static readonly int constant = 42;
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new StaticFieldNameWalker();
        walker.Visit(tree.GetRoot());

        Assert.Single(walker.Names);
        Assert.Contains("constant", walker.Names);
    }

    [Fact]
    public void StaticFieldNameWalker_IgnoresProperties()
    {
        var code = @"
class C
{
    public static int StaticProperty { get; set; }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new StaticFieldNameWalker();
        walker.Visit(tree.GetRoot());

        // Properties are not fields
        Assert.Empty(walker.Names);
    }

    [Fact]
    public void StaticFieldNameWalker_HandlesEmptyClass()
    {
        var code = @"class C { }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new StaticFieldNameWalker();
        walker.Visit(tree.GetRoot());

        Assert.Empty(walker.Names);
    }

    [Fact]
    public void StaticFieldNameWalker_HandlesNestedClasses()
    {
        var code = @"
class Outer
{
    private static int outerStatic;

    class Inner
    {
        private static int innerStatic;
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new StaticFieldNameWalker();
        walker.Visit(tree.GetRoot());

        Assert.Equal(2, walker.Names.Count);
        Assert.Contains("outerStatic", walker.Names);
        Assert.Contains("innerStatic", walker.Names);
    }

    [Fact]
    public void StaticFieldNameWalker_HandlesAllAccessModifiers()
    {
        var code = @"
class C
{
    public static int publicStatic;
    private static int privateStatic;
    protected static int protectedStatic;
    internal static int internalStatic;
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new StaticFieldNameWalker();
        walker.Visit(tree.GetRoot());

        Assert.Equal(4, walker.Names.Count);
    }
}
