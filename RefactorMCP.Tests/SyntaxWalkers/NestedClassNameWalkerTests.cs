using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using RefactorMCP.ConsoleApp.SyntaxWalkers;
using Xunit;

namespace RefactorMCP.Tests.SyntaxWalkers;

public class NestedClassNameWalkerTests
{
    [Fact]
    public void NestedClassNameWalker_CollectsDirectNestedClasses()
    {
        var code = @"
class Outer
{
    class Inner1 { }
    class Inner2 { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var outer = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == "Outer");

        var walker = new NestedClassNameWalker(outer);
        walker.Visit(tree.GetRoot());

        Assert.Equal(2, walker.Names.Count);
        Assert.Contains("Inner1", walker.Names);
        Assert.Contains("Inner2", walker.Names);
    }

    [Fact]
    public void NestedClassNameWalker_IgnoresDeeplyNestedClasses()
    {
        var code = @"
class Outer
{
    class Inner
    {
        class DeepInner { }
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var outer = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == "Outer");

        var walker = new NestedClassNameWalker(outer);
        walker.Visit(tree.GetRoot());

        // Only direct children, not grandchildren
        Assert.Single(walker.Names);
        Assert.Contains("Inner", walker.Names);
        Assert.DoesNotContain("DeepInner", walker.Names);
    }

    [Fact]
    public void NestedClassNameWalker_CollectsNestedEnums()
    {
        var code = @"
class Outer
{
    enum Status { Active, Inactive }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var outer = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == "Outer");

        var walker = new NestedClassNameWalker(outer);
        walker.Visit(tree.GetRoot());

        Assert.Single(walker.Names);
        Assert.Contains("Status", walker.Names);
    }

    [Fact]
    public void NestedClassNameWalker_CollectsClassesAndEnums()
    {
        var code = @"
class Outer
{
    class Inner { }
    enum Status { Active }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var outer = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == "Outer");

        var walker = new NestedClassNameWalker(outer);
        walker.Visit(tree.GetRoot());

        Assert.Equal(2, walker.Names.Count);
        Assert.Contains("Inner", walker.Names);
        Assert.Contains("Status", walker.Names);
    }

    [Fact]
    public void NestedClassNameWalker_HandlesEmptyClass()
    {
        var code = @"
class Outer
{
    void Method() { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var outer = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == "Outer");

        var walker = new NestedClassNameWalker(outer);
        walker.Visit(tree.GetRoot());

        Assert.Empty(walker.Names);
    }

    [Fact]
    public void NestedClassNameWalker_IgnoresSiblingClasses()
    {
        var code = @"
class Outer
{
    class Inner { }
}

class Sibling
{
    class SiblingInner { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var outer = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == "Outer");

        var walker = new NestedClassNameWalker(outer);
        walker.Visit(tree.GetRoot());

        Assert.Single(walker.Names);
        Assert.Contains("Inner", walker.Names);
        Assert.DoesNotContain("Sibling", walker.Names);
        Assert.DoesNotContain("SiblingInner", walker.Names);
    }

    [Fact]
    public void NestedClassNameWalker_WorksForDifferentTargets()
    {
        var code = @"
class ClassA
{
    class NestedA { }
}

class ClassB
{
    class NestedB { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var classA = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == "ClassA");
        var classB = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == "ClassB");

        var walkerA = new NestedClassNameWalker(classA);
        walkerA.Visit(tree.GetRoot());

        var walkerB = new NestedClassNameWalker(classB);
        walkerB.Visit(tree.GetRoot());

        Assert.Single(walkerA.Names);
        Assert.Contains("NestedA", walkerA.Names);

        Assert.Single(walkerB.Names);
        Assert.Contains("NestedB", walkerB.Names);
    }

    [Fact]
    public void NestedClassNameWalker_HandlesMultipleLevels()
    {
        var code = @"
class Level1
{
    class Level2
    {
        class Level3 { }
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var level1 = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == "Level1");
        var level2 = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == "Level2");

        var walker1 = new NestedClassNameWalker(level1);
        walker1.Visit(tree.GetRoot());

        var walker2 = new NestedClassNameWalker(level2);
        walker2.Visit(tree.GetRoot());

        Assert.Single(walker1.Names);
        Assert.Contains("Level2", walker1.Names);

        Assert.Single(walker2.Names);
        Assert.Contains("Level3", walker2.Names);
    }
}
