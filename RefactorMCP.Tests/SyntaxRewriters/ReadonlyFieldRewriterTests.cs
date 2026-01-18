using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Xunit;

namespace RefactorMCP.Tests.SyntaxRewriters;

public class ReadonlyFieldRewriterTests
{
    [Fact]
    public void ReadonlyFieldRewriter_MakesFieldReadonlyAndMovesInit()
    {
        var code = @"class C{ int x=1; C(){ } }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var init = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(1));
        var rewriter = new ReadonlyFieldRewriter("x", init);
        var newRoot = Formatter.Format(rewriter.Visit(root)!, RefactoringHelpers.SharedWorkspace);
        var text = newRoot.ToFullString();
        Assert.Contains("readonly int x", text);
        Assert.Contains("x = 1;", text);
    }
}
