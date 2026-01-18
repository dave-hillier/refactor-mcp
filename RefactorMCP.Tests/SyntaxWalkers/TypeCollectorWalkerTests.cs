using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorMCP.ConsoleApp.SyntaxWalkers;
using Xunit;

namespace RefactorMCP.Tests.SyntaxWalkers;

public class TypeCollectorWalkerTests
{
    [Fact]
    public void TypeCollectorWalker_CollectsClasses()
    {
        var code = @"
class First { }
class Second { }
class Third { }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new ClassCollectorWalker();
        walker.Visit(tree.GetRoot());

        Assert.Equal(3, walker.Classes.Count);
        Assert.Contains("First", walker.Classes.Keys);
        Assert.Contains("Second", walker.Classes.Keys);
        Assert.Contains("Third", walker.Classes.Keys);
    }

    [Fact]
    public void TypeCollectorWalker_CollectsInterfaces()
    {
        var code = @"
interface IFirst { }
interface ISecond { }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new InterfaceCollectorWalker();
        walker.Visit(tree.GetRoot());

        Assert.Equal(2, walker.Interfaces.Count);
        Assert.Contains("IFirst", walker.Interfaces.Keys);
        Assert.Contains("ISecond", walker.Interfaces.Keys);
    }

    [Fact]
    public void TypeCollectorWalker_HandlesNestedClasses()
    {
        var code = @"
class Outer
{
    class Inner { }
    class AnotherInner { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new ClassCollectorWalker();
        walker.Visit(tree.GetRoot());

        Assert.Equal(3, walker.Classes.Count);
        Assert.Contains("Outer", walker.Classes.Keys);
        Assert.Contains("Inner", walker.Classes.Keys);
        Assert.Contains("AnotherInner", walker.Classes.Keys);
    }

    [Fact]
    public void TypeCollectorWalker_HandlesEmptyCode()
    {
        var code = "";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new ClassCollectorWalker();
        walker.Visit(tree.GetRoot());

        Assert.Empty(walker.Classes);
    }

    [Fact]
    public void TypeCollectorWalker_FirstDeclarationWins()
    {
        // If there are duplicate names (e.g., partial classes in different files),
        // the walker keeps the first one encountered
        var code = @"
class Duplicate { void First() { } }
class Duplicate { void Second() { } }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new ClassCollectorWalker();
        walker.Visit(tree.GetRoot());

        Assert.Single(walker.Classes);
        var classDecl = walker.Classes["Duplicate"];
        Assert.Contains("First", classDecl.ToFullString());
    }

    [Fact]
    public void TypeCollectorWalker_ReturnsCorrectSyntaxNodes()
    {
        var code = @"
class MyClass
{
    public void Method() { }
    public int Property { get; set; }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new ClassCollectorWalker();
        walker.Visit(tree.GetRoot());

        var classNode = walker.Classes["MyClass"];
        Assert.IsType<ClassDeclarationSyntax>(classNode);
        Assert.Equal(2, classNode.Members.Count);
    }

    [Fact]
    public void TypeCollectorWalker_HandlesGenericTypes()
    {
        var code = @"
class Generic<T> { }
class GenericTwo<T, U> { }
interface IGeneric<T> { }";
        var tree = CSharpSyntaxTree.ParseText(code);

        var classWalker = new ClassCollectorWalker();
        classWalker.Visit(tree.GetRoot());

        var interfaceWalker = new InterfaceCollectorWalker();
        interfaceWalker.Visit(tree.GetRoot());

        Assert.Equal(2, classWalker.Classes.Count);
        Assert.Contains("Generic", classWalker.Classes.Keys);
        Assert.Contains("GenericTwo", classWalker.Classes.Keys);

        Assert.Single(interfaceWalker.Interfaces);
        Assert.Contains("IGeneric", interfaceWalker.Interfaces.Keys);
    }

    [Fact]
    public void TypeCollectorWalker_HandlesMixedTypes()
    {
        var code = @"
class MyClass { }
interface IMyInterface { }
struct MyStruct { }
enum MyEnum { }
record MyRecord { }";
        var tree = CSharpSyntaxTree.ParseText(code);

        var classWalker = new ClassCollectorWalker();
        classWalker.Visit(tree.GetRoot());

        var interfaceWalker = new InterfaceCollectorWalker();
        interfaceWalker.Visit(tree.GetRoot());

        // Only classes should be collected by ClassCollectorWalker
        Assert.Single(classWalker.Classes);
        Assert.Contains("MyClass", classWalker.Classes.Keys);

        // Only interfaces should be collected by InterfaceCollectorWalker
        Assert.Single(interfaceWalker.Interfaces);
        Assert.Contains("IMyInterface", interfaceWalker.Interfaces.Keys);
    }

    [Fact]
    public void TypeCollectorWalker_HandlesNamespaces()
    {
        var code = @"
namespace Ns1
{
    class ClassInNs1 { }
}
namespace Ns2
{
    class ClassInNs2 { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new ClassCollectorWalker();
        walker.Visit(tree.GetRoot());

        Assert.Equal(2, walker.Classes.Count);
        Assert.Contains("ClassInNs1", walker.Classes.Keys);
        Assert.Contains("ClassInNs2", walker.Classes.Keys);
    }
}
