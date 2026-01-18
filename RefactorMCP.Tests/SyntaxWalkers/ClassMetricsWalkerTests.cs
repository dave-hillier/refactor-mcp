using Microsoft.CodeAnalysis.CSharp;
using RefactorMCP.ConsoleApp.SyntaxWalkers;
using Xunit;

namespace RefactorMCP.Tests.SyntaxWalkers;

public class ClassMetricsWalkerTests
{
    [Fact]
    public void ClassMetricsWalker_DetectsLargeClass_ByMemberCount()
    {
        var code = @"
class LargeClass
{
    void M1() { }
    void M2() { }
    void M3() { }
    void M4() { }
    void M5() { }
    void M6() { }
    void M7() { }
    void M8() { }
    void M9() { }
    void M10() { }
    void M11() { }
    void M12() { }
    void M13() { }
    void M14() { }
    void M15() { }
    void M16() { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new ClassMetricsWalker();
        walker.Visit(tree.GetRoot());

        Assert.Contains(walker.Suggestions, s => s.Contains("LargeClass") && s.Contains("16 members"));
    }

    [Fact]
    public void ClassMetricsWalker_DoesNotFlagSmallClass()
    {
        var code = @"
class SmallClass
{
    void M1() { }
    void M2() { }
    void M3() { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new ClassMetricsWalker();
        walker.Visit(tree.GetRoot());

        Assert.Empty(walker.Suggestions);
    }

    [Fact]
    public void ClassMetricsWalker_HandlesEmptyClass()
    {
        var code = @"class EmptyClass { }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new ClassMetricsWalker();
        walker.Visit(tree.GetRoot());

        Assert.Empty(walker.Suggestions);
    }

    [Fact]
    public void ClassMetricsWalker_HandlesMultipleClasses()
    {
        var code = @"
class Small1 { void M1() { } }
class Small2 { void M1() { } }
class Large
{
    void M1() { } void M2() { } void M3() { } void M4() { } void M5() { }
    void M6() { } void M7() { } void M8() { } void M9() { } void M10() { }
    void M11() { } void M12() { } void M13() { } void M14() { } void M15() { }
    void M16() { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new ClassMetricsWalker();
        walker.Visit(tree.GetRoot());

        Assert.Single(walker.Suggestions);
        Assert.Contains(walker.Suggestions, s => s.Contains("Large"));
    }

    [Fact]
    public void ClassMetricsWalker_CountsAllMemberTypes()
    {
        var code = @"
class MixedClass
{
    private int field1;
    private int field2;
    public string Prop1 { get; set; }
    public string Prop2 { get; set; }
    void M1() { }
    void M2() { }
    void M3() { }
    void M4() { }
    void M5() { }
    void M6() { }
    void M7() { }
    void M8() { }
    void M9() { }
    void M10() { }
    void M11() { }
    void M12() { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new ClassMetricsWalker();
        walker.Visit(tree.GetRoot());

        // 2 fields + 2 properties + 12 methods = 16 members
        Assert.Contains(walker.Suggestions, s => s.Contains("MixedClass") && s.Contains("16 members"));
    }

    [Fact]
    public void ClassMetricsWalker_HandlesNestedClasses()
    {
        var code = @"
class Outer
{
    void M1() { }

    class Inner
    {
        void M1() { } void M2() { } void M3() { } void M4() { } void M5() { }
        void M6() { } void M7() { } void M8() { } void M9() { } void M10() { }
        void M11() { } void M12() { } void M13() { } void M14() { } void M15() { }
        void M16() { }
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new ClassMetricsWalker();
        walker.Visit(tree.GetRoot());

        Assert.Contains(walker.Suggestions, s => s.Contains("Inner"));
    }

    [Fact]
    public void ClassMetricsWalker_SuggestsMoveMethodOrSplit()
    {
        var code = @"
class LargeClass
{
    void M1() { } void M2() { } void M3() { } void M4() { } void M5() { }
    void M6() { } void M7() { } void M8() { } void M9() { } void M10() { }
    void M11() { } void M12() { } void M13() { } void M14() { } void M15() { }
    void M16() { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new ClassMetricsWalker();
        walker.Visit(tree.GetRoot());

        Assert.Contains(walker.Suggestions, s => s.Contains("move-method") || s.Contains("splitting"));
    }

    [Fact]
    public void ClassMetricsWalker_ThresholdAt15Members()
    {
        var exactly15 = @"
class FifteenMembers
{
    void M1() { } void M2() { } void M3() { } void M4() { } void M5() { }
    void M6() { } void M7() { } void M8() { } void M9() { } void M10() { }
    void M11() { } void M12() { } void M13() { } void M14() { } void M15() { }
}";
        var tree = CSharpSyntaxTree.ParseText(exactly15);
        var walker = new ClassMetricsWalker();
        walker.Visit(tree.GetRoot());

        // At exactly 15 members, should NOT be flagged (threshold is > 15)
        Assert.Empty(walker.Suggestions);
    }
}
