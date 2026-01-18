using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RefactorMCP.ConsoleApp.SyntaxWalkers;
using Xunit;

namespace RefactorMCP.Tests.SyntaxWalkers;

public class UnusedMembersWalkerTests
{
    [Fact]
    public async Task UnusedMembersWalker_DetectsUnusedPrivateMethod()
    {
        var code = @"
class C
{
    private void UnusedMethod() { }
    public void UsedMethod() { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new UnusedMembersWalker();
        walker.Visit(tree.GetRoot());
        await walker.PostProcessAsync();

        Assert.Contains(walker.Suggestions, s => s.Contains("UnusedMethod") && s.Contains("safe-delete-method"));
    }

    [Fact]
    public async Task UnusedMembersWalker_DoesNotFlagPublicMethods()
    {
        var code = @"
class C
{
    public void PublicMethod() { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new UnusedMembersWalker();
        walker.Visit(tree.GetRoot());
        await walker.PostProcessAsync();

        Assert.DoesNotContain(walker.Suggestions, s => s.Contains("PublicMethod"));
    }

    [Fact]
    public async Task UnusedMembersWalker_DetectsUnusedField()
    {
        var code = @"
class C
{
    private int unusedField;
    public void Method() { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new UnusedMembersWalker();
        walker.Visit(tree.GetRoot());
        await walker.PostProcessAsync();

        Assert.Contains(walker.Suggestions, s => s.Contains("unusedField") && s.Contains("safe-delete-field"));
    }

    [Fact]
    public async Task UnusedMembersWalker_DoesNotFlagUsedField()
    {
        var code = @"
class C
{
    private int usedField;
    public int GetValue() => usedField + 1;
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new UnusedMembersWalker();
        walker.Visit(tree.GetRoot());
        await walker.PostProcessAsync();

        Assert.DoesNotContain(walker.Suggestions, s => s.Contains("usedField"));
    }

    [Fact]
    public async Task UnusedMembersWalker_DoesNotFlagUsedMethod()
    {
        var code = @"
class C
{
    private void Helper() { }
    public void Caller() { Helper(); }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new UnusedMembersWalker();
        walker.Visit(tree.GetRoot());
        await walker.PostProcessAsync();

        Assert.DoesNotContain(walker.Suggestions, s => s.Contains("Helper"));
    }

    [Fact]
    public async Task UnusedMembersWalker_DetectsMultipleUnusedMembers()
    {
        var code = @"
class C
{
    private int field1;
    private int field2;
    private void Method1() { }
    private void Method2() { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new UnusedMembersWalker();
        walker.Visit(tree.GetRoot());
        await walker.PostProcessAsync();

        Assert.True(walker.Suggestions.Count >= 4);
    }

    [Fact]
    public async Task UnusedMembersWalker_HandlesEmptyClass()
    {
        var code = @"class C { }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new UnusedMembersWalker();
        walker.Visit(tree.GetRoot());
        await walker.PostProcessAsync();

        Assert.Empty(walker.Suggestions);
    }

    [Fact]
    public async Task UnusedMembersWalker_HandlesInternalMethod()
    {
        var code = @"
class C
{
    internal void InternalMethod() { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new UnusedMembersWalker();
        walker.Visit(tree.GetRoot());
        await walker.PostProcessAsync();

        // Internal methods without invocations should be flagged in single file mode
        Assert.Contains(walker.Suggestions, s => s.Contains("InternalMethod"));
    }
}
