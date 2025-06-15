using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Xunit;

namespace RefactorMCP.Tests;

public partial class RoslynTransformationTests
{
    [Fact]
    public void ParameterRemovalRewriter_RemovesParameter()
    {
        var tree = CSharpSyntaxTree.ParseText("class A{void M(int a,int b){}} ");
        var root = tree.GetRoot();
        var rewriter = new ParameterRemovalRewriter("M", 1);
        var newRoot = Formatter.Format(rewriter.Visit(root)!, new AdhocWorkspace());
        Assert.Contains("void M(int a)", newRoot.ToFullString());
    }
}
