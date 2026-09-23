using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Xunit;

namespace RefactorMCP.Tests.SyntaxRewriters;

public class ReadonlyFieldRewriterTests
{
    [Fact]
    public void ReadonlyFieldRewriter_MakesFieldReadonlyAndKeepsInit()
    {
        var code = @"class C{ int x=1; C(){ } }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var rewriter = new ReadonlyFieldRewriter("x");
        var newRoot = Formatter.Format(rewriter.Visit(root)!, RefactoringHelpers.SharedWorkspace);
        var text = newRoot.ToFullString();
        Assert.Contains("readonly int x = 1;", text);
        Assert.Contains("C() { }", text);
    }
}
