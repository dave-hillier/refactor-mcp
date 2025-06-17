using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Editing;
using Xunit;

namespace RefactorMCP.Tests;

public partial class RoslynTransformationTests
{
    [Fact]
    public void ParameterIntroductionRewriter_AddsParameterAndArgument()
    {
        var code = @"class C{ void M(){Console.WriteLine(1);} void Call(){ M(); } }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var expr = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(1));
        var parameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier("p")).WithType(SyntaxFactory.ParseTypeName("int"));
        var paramRef = SyntaxFactory.IdentifierName("p");
        var generator = SyntaxGenerator.GetGenerator(new AdhocWorkspace(), LanguageNames.CSharp);
        var rewriter = new ParameterIntroductionRewriter(expr, "M", parameter, paramRef, generator);
        var newRoot = Formatter.Format(rewriter.Visit(root)!, new AdhocWorkspace());
        var text = newRoot.ToFullString();
        Assert.Contains("void M(int p)", text);
        Assert.Contains("Console.WriteLine(p)", text);
        Assert.Contains("M(1);", text);
    }
}
