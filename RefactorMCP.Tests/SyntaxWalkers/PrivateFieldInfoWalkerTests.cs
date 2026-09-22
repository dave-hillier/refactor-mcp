using Microsoft.CodeAnalysis.CSharp;
using RefactorMCP.ConsoleApp.SyntaxWalkers;
using Xunit;

namespace RefactorMCP.Tests.SyntaxWalkers;

public class PrivateFieldInfoWalkerTests
{
    [Fact]
    public void PrivateFieldInfoWalker_CollectsPrivateFields()
    {
        // `string b;` is private too: a field with no access modifier is private,
        // which is how backing fields are normally written.
        var code = @"class C { private int a; string b; private string c; }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new PrivateFieldInfoWalker();
        walker.Visit(tree.GetRoot());
        Assert.Equal(3, walker.Infos.Count);
        Assert.Contains("a", walker.Infos.Keys);
        Assert.Contains("b", walker.Infos.Keys);
        Assert.Contains("c", walker.Infos.Keys);
    }

    [Fact]
    public void PrivateFieldInfoWalker_SkipsAccessibleFields()
    {
        var code = @"class C { public int a; protected int b; internal int c; public static int d; }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new PrivateFieldInfoWalker();
        walker.Visit(tree.GetRoot());
        Assert.Empty(walker.Infos);
    }
}
