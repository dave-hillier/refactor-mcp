using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace RefactorMCP.Tests.SyntaxRewriters;

public class SetterToInitRewriterTests
{
    [Fact]
    public void SetterToInitRewriter_ReplacesSetterWithInit()
    {
        var prop = SyntaxFactory.ParseMemberDeclaration("public int P { get; set; }") as PropertyDeclarationSyntax;
        var rewriter = new SetterToInitRewriter("P");
        var result = rewriter.Visit(prop!)!.NormalizeWhitespace().ToFullString();
        Assert.Contains("init", result);
    }
}
