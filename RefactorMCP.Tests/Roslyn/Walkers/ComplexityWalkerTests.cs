using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorMCP.ConsoleApp.SyntaxWalkers;
using Xunit;

namespace RefactorMCP.Tests;

public partial class RoslynTransformationTests
{
    [Fact]
    public void ComplexityWalker_ComputesComplexityAndDepth()
    {
        const string code = "class C { void M() { if(true){ for(int i=0;i<1;i++){ } } } }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        var walker = new ComplexityWalker();
        walker.Visit(method);
        Assert.Equal(3, walker.Complexity);
        Assert.Equal(2, walker.MaxDepth);
    }
}
