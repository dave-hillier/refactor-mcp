using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Xunit;

namespace RefactorMCP.Tests;

public partial class RoslynTransformationTests
{
    [Fact]
    public void VariableIntroductionRewriter_AddsVariable()
    {
        var code = @"class C{ void M(){ Console.WriteLine(1+2); } }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var expr = SyntaxFactory.ParseExpression("1+2");
        var varRef = SyntaxFactory.IdentifierName("sum");
        var varDecl = SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName("var"))
                    .AddVariables(SyntaxFactory.VariableDeclarator("sum")
                        .WithInitializer(SyntaxFactory.EqualsValueClause(expr))));
        var callStmt = root.DescendantNodes().OfType<ExpressionStatementSyntax>().First();
        var block = callStmt.Ancestors().OfType<BlockSyntax>().First();
        var rewriter = new VariableIntroductionRewriter(expr, varRef, varDecl, callStmt, block);
        var newRoot = Formatter.Format(rewriter.Visit(root)!, RefactoringHelpers.SharedWorkspace);
        var text = newRoot.ToFullString();
        Assert.Contains("var sum = 1 + 2;", text);
        Assert.Contains("Console.WriteLine(sum);", text);
    }
}
