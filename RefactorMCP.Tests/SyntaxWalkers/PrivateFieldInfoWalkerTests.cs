using Microsoft.CodeAnalysis.CSharp;
using RefactorMCP.ConsoleApp.SyntaxWalkers;
using Xunit;

namespace RefactorMCP.Tests.SyntaxWalkers;

public class PrivateFieldInfoWalkerTests
{
    [Fact]
    public void PrivateFieldInfoWalker_CollectsPrivateFields()
    {
        var code = @"class C { private int a; string b; private string c; }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new PrivateFieldInfoWalker();
        walker.Visit(tree.GetRoot());
        Assert.Equal(2, walker.Infos.Count);
        Assert.Contains("a", walker.Infos.Keys);
        Assert.Contains("c", walker.Infos.Keys);
    }
}
