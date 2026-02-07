using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace RefactorMCP.Tests.SyntaxRewriters;

public class NestedClassRewriterTests
{
    [Fact]
    public void NestedClassRewriter_QualifiesTypeInVariableDeclaration()
    {
        var code = @"
class C
{
    void Method()
    {
        Inner x = new Inner();
    }
}";
        var expected = @"
class C
{
    void Method()
    {
        Outer.Inner x = new Outer.Inner();
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var rewriter = new NestedClassRewriter(new HashSet<string> { "Inner" }, "Outer");
        var result = rewriter.Visit(tree.GetRoot());

        Assert.Equal(expected, result.ToFullString());
    }

    [Fact]
    public void NestedClassRewriter_QualifiesTypeInParameter()
    {
        var code = @"
class C
{
    void Method(Inner param) { }
}";
        var expected = @"
class C
{
    void Method(Outer.Inner param) { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var rewriter = new NestedClassRewriter(new HashSet<string> { "Inner" }, "Outer");
        var result = rewriter.Visit(tree.GetRoot());

        Assert.Equal(expected, result.ToFullString());
    }

    [Fact]
    public void NestedClassRewriter_QualifiesReturnType()
    {
        var code = @"
class C
{
    Inner GetInner() => null;
}";
        var expected = @"
class C
{
    Outer.Inner GetInner() => null;
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var rewriter = new NestedClassRewriter(new HashSet<string> { "Inner" }, "Outer");
        var result = rewriter.Visit(tree.GetRoot());

        Assert.Equal(expected, result.ToFullString());
    }

    [Fact]
    public void NestedClassRewriter_QualifiesInObjectCreation()
    {
        var code = @"
class C
{
    void Method()
    {
        var x = new Inner();
    }
}";
        var expected = @"
class C
{
    void Method()
    {
        var x = new Outer.Inner();
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var rewriter = new NestedClassRewriter(new HashSet<string> { "Inner" }, "Outer");
        var result = rewriter.Visit(tree.GetRoot());

        Assert.Equal(expected, result.ToFullString());
    }

    [Fact]
    public void NestedClassRewriter_QualifiesInCast()
    {
        var code = @"
class C
{
    void Method(object o)
    {
        var x = (Inner)o;
    }
}";
        var expected = @"
class C
{
    void Method(object o)
    {
        var x = (Outer.Inner)o;
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var rewriter = new NestedClassRewriter(new HashSet<string> { "Inner" }, "Outer");
        var result = rewriter.Visit(tree.GetRoot());

        Assert.Equal(expected, result.ToFullString());
    }

    [Fact]
    public void NestedClassRewriter_QualifiesInTypeOf()
    {
        var code = @"
class C
{
    void Method()
    {
        var t = typeof(Inner);
    }
}";
        var expected = @"
class C
{
    void Method()
    {
        var t = typeof(Outer.Inner);
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var rewriter = new NestedClassRewriter(new HashSet<string> { "Inner" }, "Outer");
        var result = rewriter.Visit(tree.GetRoot());

        Assert.Equal(expected, result.ToFullString());
    }

    [Fact]
    public void NestedClassRewriter_QualifiesStaticMemberAccess()
    {
        var code = @"
class C
{
    void Method()
    {
        Inner.StaticMethod();
    }
}";
        var expected = @"
class C
{
    void Method()
    {
        Outer.Inner.StaticMethod();
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var rewriter = new NestedClassRewriter(new HashSet<string> { "Inner" }, "Outer");
        var result = rewriter.Visit(tree.GetRoot());

        Assert.Equal(expected, result.ToFullString());
    }

    [Fact]
    public void NestedClassRewriter_HandlesMultipleNestedClasses()
    {
        var code = @"
class C
{
    void Method()
    {
        First a = null;
        Second b = null;
    }
}";
        var expected = @"
class C
{
    void Method()
    {
        Outer.First a = null;
        Outer.Second b = null;
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var rewriter = new NestedClassRewriter(new HashSet<string> { "First", "Second" }, "Outer");
        var result = rewriter.Visit(tree.GetRoot());

        Assert.Equal(expected, result.ToFullString());
    }

    [Fact]
    public void NestedClassRewriter_DoesNotQualifyUnknownTypes()
    {
        var code = @"
class C
{
    void Method()
    {
        string s = null;
        Other o = null;
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var rewriter = new NestedClassRewriter(new HashSet<string> { "Inner" }, "Outer");
        var result = rewriter.Visit(tree.GetRoot());

        // Should remain unchanged
        Assert.Equal(code, result.ToFullString());
    }

    [Fact]
    public void NestedClassRewriter_QualifiesInGenericTypeArgument()
    {
        var code = @"
class C
{
    void Method()
    {
        List<Inner> list = null;
    }
}";
        var expected = @"
class C
{
    void Method()
    {
        List<Outer.Inner> list = null;
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var rewriter = new NestedClassRewriter(new HashSet<string> { "Inner" }, "Outer");
        var result = rewriter.Visit(tree.GetRoot());

        Assert.Equal(expected, result.ToFullString());
    }

    [Fact]
    public void NestedClassRewriter_QualifiesInBaseType()
    {
        var code = @"
class Derived : Inner
{
}";
        var expected = @"
class Derived : Outer.Inner
{
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var rewriter = new NestedClassRewriter(new HashSet<string> { "Inner" }, "Outer");
        var result = rewriter.Visit(tree.GetRoot());

        Assert.Equal(expected, result.ToFullString());
    }

    [Fact]
    public void NestedClassRewriter_QualifiesInForeach()
    {
        var code = @"
class C
{
    void Method(object[] items)
    {
        foreach (Inner item in items) { }
    }
}";
        var expected = @"
class C
{
    void Method(object[] items)
    {
        foreach (Outer.Inner item in items) { }
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var rewriter = new NestedClassRewriter(new HashSet<string> { "Inner" }, "Outer");
        var result = rewriter.Visit(tree.GetRoot());

        Assert.Equal(expected, result.ToFullString());
    }
}
