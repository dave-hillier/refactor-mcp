using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using RefactorMCP.ConsoleApp.SyntaxRewriters;
using Xunit;

namespace RefactorMCP.Tests;

public partial class RoslynTransformationTests
{
    [Fact]
    public void MethodRemovalRewriter_RemovesMethod()
    {
        var tree = CSharpSyntaxTree.ParseText("class A{void M(){}}");
        var root = tree.GetRoot();
        var rewriter = new MethodRemovalRewriter("M");
        var newRoot = Formatter.Format(rewriter.Visit(root)!, new AdhocWorkspace());
        Assert.Equal("class A { }", newRoot.ToFullString().Trim());
    }
}
