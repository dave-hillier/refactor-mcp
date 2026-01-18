using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using RefactorMCP.ConsoleApp.SyntaxRewriters;
using Xunit;

namespace RefactorMCP.Tests.SyntaxRewriters;

public class FieldRemovalRewriterTests
{
    [Fact]
    public void FieldRemovalRewriter_RemovesField()
    {
        var tree = CSharpSyntaxTree.ParseText("class A{int x;}");
        var root = tree.GetRoot();
        var rewriter = new FieldRemovalRewriter("x");
        var newRoot = Formatter.Format(rewriter.Visit(root)!, RefactoringHelpers.SharedWorkspace);
        Assert.Equal("class A { }", newRoot.ToFullString().Trim());
    }
}
