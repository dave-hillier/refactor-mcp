using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace RefactorMCP.Tests.SyntaxRewriters;

public class StaticConversionRewriterTests
{
    [Fact]
    public void StaticConversionRewriter_ConvertsInstanceMethod()
    {
        var method = SyntaxFactory.ParseMemberDeclaration("int GetX(){ return x; }") as MethodDeclarationSyntax;
        var rewriter = new StaticConversionRewriter(System.Array.Empty<(string Name, string Type)>(), "inst", new HashSet<string> { "x" });
        var result = rewriter.Rewrite(method!).NormalizeWhitespace().ToFullString();
        Assert.Contains("static", result);
        Assert.Contains("inst.x", result);
    }
}
