using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace RefactorMCP.Tests.SyntaxRewriters;

public class StaticFieldRewriterTests
{
    [Fact]
    public void StaticFieldRewriter_QualifiesStaticField()
    {
        var method = SyntaxFactory.ParseMemberDeclaration("void Test(){ x = 1; }") as MethodDeclarationSyntax;
        var rewriter = new StaticFieldRewriter(new HashSet<string> { "x" }, "C");
        var result = rewriter.Visit(method!)!.NormalizeWhitespace().ToFullString();
        Assert.Contains("C.x", result);
    }
}
