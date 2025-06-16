using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace RefactorMCP.Tests;

public partial class RoslynTransformationTests
{
    [Fact]
    public void ParameterRewriter_ReplacesIdentifiers()
    {
        var expr = SyntaxFactory.ParseExpression("a + b");
        var map = new Dictionary<string, ExpressionSyntax>
        {
            ["a"] = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(1)),
            ["b"] = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(2))
        };
        var rewriter = new ParameterRewriter(map);
        var result = rewriter.Visit(expr)!.NormalizeWhitespace().ToFullString();
        Assert.Equal("1 + 2", result);
    }

    [Fact]
    public void ParameterRewriter_ReplacesMemberAccess()
    {
        var expr = SyntaxFactory.ParseExpression("this.a + a");
        var map = new Dictionary<string, ExpressionSyntax>
        {
            ["a"] = SyntaxFactory.IdentifierName("p")
        };
        var rewriter = new ParameterRewriter(map);
        var result = rewriter.Visit(expr)!.NormalizeWhitespace().ToFullString();
        Assert.Equal("p + p", result);
    }
}
