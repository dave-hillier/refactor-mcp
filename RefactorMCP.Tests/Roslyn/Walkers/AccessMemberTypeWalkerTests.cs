using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using RefactorMCP.ConsoleApp.SyntaxWalkers;
using Xunit;

namespace RefactorMCP.Tests;

public partial class RoslynTransformationTests
{
    [Fact]
    public void AccessMemberTypeWalker_DetectsFieldAndProperty()
    {
        const string code = "class C { int f; string P { get; } }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();

        var fieldWalker = new AccessMemberTypeWalker("f");
        fieldWalker.Visit(root);
        Assert.Equal("field", fieldWalker.MemberType);

        var propWalker = new AccessMemberTypeWalker("P");
        propWalker.Visit(root);
        Assert.Equal("property", propWalker.MemberType);
    }
}
