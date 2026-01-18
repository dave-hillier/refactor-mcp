using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Xunit;

namespace RefactorMCP.Tests.SyntaxRewriters;

public class ExtractMethodRewriterTests
{
    [Fact]
    public void ExtractMethodRewriter_ExtractsStatementsToNewMethod()
    {
        var code = @"class C{ void M(){ Console.WriteLine(1); Console.WriteLine(2); } }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        var firstStmt = method.Body!.Statements.First();
        var rewriter = new ExtractMethodRewriter(method, root.DescendantNodes().OfType<ClassDeclarationSyntax>().First(), new List<StatementSyntax> { firstStmt }, "NewMethod");
        var newRoot = Formatter.Format(rewriter.Visit(root)!, RefactoringHelpers.SharedWorkspace);
        var text = newRoot.ToFullString();
        Assert.Contains("private void NewMethod()", text);
        Assert.Contains("NewMethod();", text);
    }
}
