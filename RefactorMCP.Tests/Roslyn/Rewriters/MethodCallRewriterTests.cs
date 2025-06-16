using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace RefactorMCP.Tests;

public partial class RoslynTransformationTests
{
    [Fact]
    public void MethodCallRewriter_QualifiesMethodCalls()
    {
        var method = SyntaxFactory.ParseMemberDeclaration("void Test(){ Do(); }") as MethodDeclarationSyntax;
        var rewriter = new MethodCallRewriter(new HashSet<string> { "Do" }, "inst");
        var result = rewriter.Visit(method!)!.NormalizeWhitespace().ToFullString();
        Assert.Contains("inst.Do()", result);
    }

    [Fact]
    public void MethodCallRewriter_QualifiesThisMethodCalls()
    {
        var method = SyntaxFactory.ParseMemberDeclaration("void Test(){ this.Do(); }") as MethodDeclarationSyntax;
        var rewriter = new MethodCallRewriter(new HashSet<string> { "Do" }, "inst");
        var result = rewriter.Visit(method!)!.NormalizeWhitespace().ToFullString();
        Assert.Contains("inst.Do()", result);
        Assert.DoesNotContain("this.Do()", result);
    }
}
