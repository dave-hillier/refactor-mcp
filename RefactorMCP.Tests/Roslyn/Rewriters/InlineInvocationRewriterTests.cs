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
    public void InlineInvocationRewriter_InlinesVoidMethodCall()
    {
        var code = @"class C{ void Helper(){ Console.WriteLine(""Hi""); } void Call(){ Helper(); } }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var helper = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First(m => m.Identifier.ValueText == "Helper");
        var rewriter = new InlineInvocationRewriter(helper);
        var newRoot = Formatter.Format(rewriter.Visit(root)!, RefactoringHelpers.SharedWorkspace);
        var callMethod = newRoot.DescendantNodes().OfType<MethodDeclarationSyntax>().First(m => m.Identifier.ValueText == "Call");
        var text = callMethod.Body!.Statements.ToFullString();
        Assert.Contains("Console.WriteLine(\"Hi\");", text);
    }
}
