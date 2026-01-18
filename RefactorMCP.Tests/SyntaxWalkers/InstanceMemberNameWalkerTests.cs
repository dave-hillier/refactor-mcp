using Microsoft.CodeAnalysis.CSharp;
using RefactorMCP.ConsoleApp.SyntaxWalkers;
using Xunit;

namespace RefactorMCP.Tests.SyntaxWalkers;

public class InstanceMemberNameWalkerTests
{
    [Fact]
    public void InstanceMemberNameWalker_CollectsFields()
    {
        var code = @"
class C
{
    private int field1;
    private string field2;
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new InstanceMemberNameWalker();
        walker.Visit(tree.GetRoot());

        Assert.Equal(2, walker.Names.Count);
        Assert.Contains("field1", walker.Names);
        Assert.Contains("field2", walker.Names);
    }

    [Fact]
    public void InstanceMemberNameWalker_CollectsProperties()
    {
        var code = @"
class C
{
    public int Property1 { get; set; }
    public string Property2 => ""test"";
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new InstanceMemberNameWalker();
        walker.Visit(tree.GetRoot());

        Assert.Equal(2, walker.Names.Count);
        Assert.Contains("Property1", walker.Names);
        Assert.Contains("Property2", walker.Names);
    }

    [Fact]
    public void InstanceMemberNameWalker_CollectsFieldsAndProperties()
    {
        var code = @"
class C
{
    private int _value;
    public int Value { get; set; }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new InstanceMemberNameWalker();
        walker.Visit(tree.GetRoot());

        Assert.Equal(2, walker.Names.Count);
        Assert.Contains("_value", walker.Names);
        Assert.Contains("Value", walker.Names);
    }

    [Fact]
    public void InstanceMemberNameWalker_HandlesMultipleFieldDeclarators()
    {
        var code = @"
class C
{
    private int a, b, c;
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new InstanceMemberNameWalker();
        walker.Visit(tree.GetRoot());

        Assert.Equal(3, walker.Names.Count);
        Assert.Contains("a", walker.Names);
        Assert.Contains("b", walker.Names);
        Assert.Contains("c", walker.Names);
    }

    [Fact]
    public void InstanceMemberNameWalker_IgnoresMethods()
    {
        var code = @"
class C
{
    private int field;
    void Method() { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new InstanceMemberNameWalker();
        walker.Visit(tree.GetRoot());

        Assert.Single(walker.Names);
        Assert.Contains("field", walker.Names);
        Assert.DoesNotContain("Method", walker.Names);
    }

    [Fact]
    public void InstanceMemberNameWalker_HandleEmptyClass()
    {
        var code = @"class C { }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new InstanceMemberNameWalker();
        walker.Visit(tree.GetRoot());

        Assert.Empty(walker.Names);
    }

    [Fact]
    public void InstanceMemberNameWalker_HandlesNestedClasses()
    {
        var code = @"
class Outer
{
    private int outerField;

    class Inner
    {
        private int innerField;
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new InstanceMemberNameWalker();
        walker.Visit(tree.GetRoot());

        Assert.Equal(2, walker.Names.Count);
        Assert.Contains("outerField", walker.Names);
        Assert.Contains("innerField", walker.Names);
    }

    [Fact]
    public void InstanceMemberNameWalker_HandlesAllAccessModifiers()
    {
        var code = @"
class C
{
    public int publicField;
    private int privateField;
    protected int protectedField;
    internal int internalField;
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new InstanceMemberNameWalker();
        walker.Visit(tree.GetRoot());

        Assert.Equal(4, walker.Names.Count);
    }

    [Fact]
    public void InstanceMemberNameWalker_IncludesStaticFields()
    {
        // Note: This walker collects ALL fields, including static
        var code = @"
class C
{
    private static int staticField;
    private int instanceField;
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new InstanceMemberNameWalker();
        walker.Visit(tree.GetRoot());

        Assert.Equal(2, walker.Names.Count);
        Assert.Contains("staticField", walker.Names);
        Assert.Contains("instanceField", walker.Names);
    }
}
