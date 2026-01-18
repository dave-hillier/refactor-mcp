using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using RefactorMCP.ConsoleApp.SyntaxWalkers;
using Xunit;

namespace RefactorMCP.Tests.SyntaxWalkers;

public class MethodDependencyWalkerTests
{
    [Fact]
    public void MethodDependencyWalker_DetectsDirectMethodCall()
    {
        var code = @"
class C
{
    void Caller() { Helper(); }
    void Helper() { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var callerMethod = tree.GetRoot().DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .First(m => m.Identifier.ValueText == "Caller");

        var candidates = new HashSet<string> { "Helper", "Other" };
        var walker = new MethodDependencyWalker(candidates);
        walker.Visit(callerMethod);

        Assert.Contains("Helper", walker.Dependencies);
        Assert.DoesNotContain("Other", walker.Dependencies);
    }

    [Fact]
    public void MethodDependencyWalker_DetectsMemberAccessCall()
    {
        var code = @"
class C
{
    void Caller() { this.Helper(); }
    void Helper() { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var callerMethod = tree.GetRoot().DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .First(m => m.Identifier.ValueText == "Caller");

        var candidates = new HashSet<string> { "Helper" };
        var walker = new MethodDependencyWalker(candidates);
        walker.Visit(callerMethod);

        Assert.Contains("Helper", walker.Dependencies);
    }

    [Fact]
    public void MethodDependencyWalker_DetectsMultipleDependencies()
    {
        var code = @"
class C
{
    void Caller()
    {
        MethodA();
        MethodB();
        MethodC();
    }
    void MethodA() { }
    void MethodB() { }
    void MethodC() { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var callerMethod = tree.GetRoot().DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .First(m => m.Identifier.ValueText == "Caller");

        var candidates = new HashSet<string> { "MethodA", "MethodB", "MethodC" };
        var walker = new MethodDependencyWalker(candidates);
        walker.Visit(callerMethod);

        Assert.Equal(3, walker.Dependencies.Count);
        Assert.Contains("MethodA", walker.Dependencies);
        Assert.Contains("MethodB", walker.Dependencies);
        Assert.Contains("MethodC", walker.Dependencies);
    }

    [Fact]
    public void MethodDependencyWalker_IgnoresNonCandidateMethods()
    {
        var code = @"
class C
{
    void Caller()
    {
        Helper();
        Console.WriteLine();
    }
    void Helper() { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var callerMethod = tree.GetRoot().DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .First(m => m.Identifier.ValueText == "Caller");

        var candidates = new HashSet<string> { "Helper" };
        var walker = new MethodDependencyWalker(candidates);
        walker.Visit(callerMethod);

        Assert.Single(walker.Dependencies);
        Assert.Contains("Helper", walker.Dependencies);
    }

    [Fact]
    public void MethodDependencyWalker_HandlesNestedCalls()
    {
        var code = @"
class C
{
    void Caller()
    {
        if (true) { MethodA(); }
        for (int i = 0; i < 10; i++) { MethodB(); }
    }
    void MethodA() { }
    void MethodB() { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var callerMethod = tree.GetRoot().DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .First(m => m.Identifier.ValueText == "Caller");

        var candidates = new HashSet<string> { "MethodA", "MethodB" };
        var walker = new MethodDependencyWalker(candidates);
        walker.Visit(callerMethod);

        Assert.Contains("MethodA", walker.Dependencies);
        Assert.Contains("MethodB", walker.Dependencies);
    }

    [Fact]
    public void MethodDependencyWalker_HandlesEmptyMethod()
    {
        var code = @"
class C
{
    void EmptyMethod() { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var emptyMethod = tree.GetRoot().DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .First();

        var candidates = new HashSet<string> { "Other" };
        var walker = new MethodDependencyWalker(candidates);
        walker.Visit(emptyMethod);

        Assert.Empty(walker.Dependencies);
    }

    [Fact]
    public void MethodDependencyWalker_HandlesLambdaCalls()
    {
        var code = @"
class C
{
    void Caller()
    {
        var action = () => Helper();
        action();
    }
    void Helper() { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var callerMethod = tree.GetRoot().DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .First(m => m.Identifier.ValueText == "Caller");

        var candidates = new HashSet<string> { "Helper" };
        var walker = new MethodDependencyWalker(candidates);
        walker.Visit(callerMethod);

        Assert.Contains("Helper", walker.Dependencies);
    }

    [Fact]
    public void MethodDependencyWalker_HandlesDuplicateCalls()
    {
        var code = @"
class C
{
    void Caller()
    {
        Helper();
        Helper();
        Helper();
    }
    void Helper() { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var callerMethod = tree.GetRoot().DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .First(m => m.Identifier.ValueText == "Caller");

        var candidates = new HashSet<string> { "Helper" };
        var walker = new MethodDependencyWalker(candidates);
        walker.Visit(callerMethod);

        // HashSet should deduplicate
        Assert.Single(walker.Dependencies);
    }
}
