using Microsoft.CodeAnalysis.CSharp;
using RefactorMCP.ConsoleApp.SyntaxWalkers;
using Xunit;

namespace RefactorMCP.Tests.SyntaxWalkers;

public class MethodNameWalkerTests
{
    [Fact]
    public void MethodNameWalker_CollectsMethodNames()
    {
        var code = @"
class C
{
    void Method1() { }
    void Method2() { }
    int Method3() => 0;
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new MethodNameWalker();
        walker.Visit(tree.GetRoot());

        Assert.Equal(3, walker.Names.Count);
        Assert.Contains("Method1", walker.Names);
        Assert.Contains("Method2", walker.Names);
        Assert.Contains("Method3", walker.Names);
    }

    [Fact]
    public void MethodNameWalker_HandlesOverloads()
    {
        var code = @"
class C
{
    void Process() { }
    void Process(int x) { }
    void Process(string s) { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new MethodNameWalker();
        walker.Visit(tree.GetRoot());

        // Overloads have the same name, so only one entry
        Assert.Single(walker.Names);
        Assert.Contains("Process", walker.Names);
    }

    [Fact]
    public void MethodNameWalker_HandlesEmptyClass()
    {
        var code = @"class C { }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new MethodNameWalker();
        walker.Visit(tree.GetRoot());

        Assert.Empty(walker.Names);
    }

    [Fact]
    public void MethodNameWalker_IgnoresProperties()
    {
        var code = @"
class C
{
    public int Value { get; set; }
    public string Name => ""test"";
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new MethodNameWalker();
        walker.Visit(tree.GetRoot());

        Assert.Empty(walker.Names);
    }

    [Fact]
    public void MethodNameWalker_IgnoresConstructors()
    {
        var code = @"
class C
{
    public C() { }
    public C(int x) { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new MethodNameWalker();
        walker.Visit(tree.GetRoot());

        // Constructors are not MethodDeclarationSyntax
        Assert.Empty(walker.Names);
    }

    [Fact]
    public void MethodNameWalker_HandlesNestedClasses()
    {
        var code = @"
class Outer
{
    void OuterMethod() { }

    class Inner
    {
        void InnerMethod() { }
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new MethodNameWalker();
        walker.Visit(tree.GetRoot());

        Assert.Equal(2, walker.Names.Count);
        Assert.Contains("OuterMethod", walker.Names);
        Assert.Contains("InnerMethod", walker.Names);
    }

    [Fact]
    public void MethodNameWalker_HandlesStaticMethods()
    {
        var code = @"
class C
{
    static void StaticMethod() { }
    void InstanceMethod() { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new MethodNameWalker();
        walker.Visit(tree.GetRoot());

        Assert.Equal(2, walker.Names.Count);
        Assert.Contains("StaticMethod", walker.Names);
        Assert.Contains("InstanceMethod", walker.Names);
    }

    [Fact]
    public void MethodNameWalker_HandlesGenericMethods()
    {
        var code = @"
class C
{
    void Generic<T>() { }
    T Convert<T, U>(U input) => default!;
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new MethodNameWalker();
        walker.Visit(tree.GetRoot());

        Assert.Equal(2, walker.Names.Count);
        Assert.Contains("Generic", walker.Names);
        Assert.Contains("Convert", walker.Names);
    }

    [Fact]
    public void MethodNameWalker_HandlesInterfaceMethods()
    {
        var code = @"
interface I
{
    void InterfaceMethod();
    int Calculate(int x);
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new MethodNameWalker();
        walker.Visit(tree.GetRoot());

        Assert.Equal(2, walker.Names.Count);
        Assert.Contains("InterfaceMethod", walker.Names);
        Assert.Contains("Calculate", walker.Names);
    }

    [Fact]
    public void MethodNameWalker_HandlesAllAccessModifiers()
    {
        var code = @"
class C
{
    public void PublicMethod() { }
    private void PrivateMethod() { }
    protected void ProtectedMethod() { }
    internal void InternalMethod() { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new MethodNameWalker();
        walker.Visit(tree.GetRoot());

        Assert.Equal(4, walker.Names.Count);
        Assert.Contains("PublicMethod", walker.Names);
        Assert.Contains("PrivateMethod", walker.Names);
        Assert.Contains("ProtectedMethod", walker.Names);
        Assert.Contains("InternalMethod", walker.Names);
    }

    [Fact]
    public void MethodNameWalker_IgnoresLocalFunctions()
    {
        var code = @"
class C
{
    void Method()
    {
        void LocalFunc() { }
        LocalFunc();
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new MethodNameWalker();
        walker.Visit(tree.GetRoot());

        // Local functions are LocalFunctionStatementSyntax, not MethodDeclarationSyntax
        Assert.Single(walker.Names);
        Assert.Contains("Method", walker.Names);
    }
}
