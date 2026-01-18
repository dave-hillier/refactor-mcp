using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorMCP.ConsoleApp.SyntaxWalkers;
using Xunit;

namespace RefactorMCP.Tests.SyntaxWalkers;

public class MethodAnalysisWalkerTests
{
    [Fact]
    public void MethodAnalysisWalker_DetectsUsageAndCalls()
    {
        const string code = @"class C { int x; void A(){ x = 1; B(); } void B(){} }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var methodA = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First(m => m.Identifier.ValueText == "A");
        var instanceMembers = new HashSet<string> { "x" };
        var methodNames = new HashSet<string> { "A", "B" };
        var walker = new MethodAnalysisWalker(instanceMembers, methodNames, "A");
        walker.Visit(methodA);
        Assert.True(walker.UsesInstanceMembers);
        Assert.True(walker.CallsOtherMethods);
        Assert.False(walker.IsRecursive);
    }

    [Fact]
    public void MethodAnalysisWalker_DetectsRecursion()
    {
        const string code = @"class C { void A(){ A(); } }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var methodA = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        var methodNames = new HashSet<string> { "A" };
        var walker = new MethodAnalysisWalker(new HashSet<string>(), methodNames, "A");
        walker.Visit(methodA);
        Assert.True(walker.IsRecursive);
    }
}
