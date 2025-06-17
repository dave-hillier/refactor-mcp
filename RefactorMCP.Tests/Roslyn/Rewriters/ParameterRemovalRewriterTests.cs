using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Editing;
using Xunit;

namespace RefactorMCP.Tests;

public partial class RoslynTransformationTests
{
    [Fact]
    public void ParameterRemovalRewriter_RemovesParameter()
    {
        var tree = CSharpSyntaxTree.ParseText("class A{void M(int a,int b){}} class B{void Call(){new A().M(1,2);}} ");
        var root = tree.GetRoot();
        var generator = SyntaxGenerator.GetGenerator(new AdhocWorkspace(), LanguageNames.CSharp);
        var rewriter = new ParameterRemovalRewriter("M", 1, generator);
        var newRoot = Formatter.Format(rewriter.Visit(root)!, new AdhocWorkspace());
        var text = newRoot.ToFullString();
        Assert.Contains("void M(int a)", text);
        Assert.Contains("M(1)", text);
        Assert.DoesNotContain("2)", text);
    }
}
