using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using RefactorMCP.ConsoleApp.SyntaxWalkers;
using Xunit;

namespace RefactorMCP.Tests.SyntaxWalkers;

public class RefactoringOpportunityWalkerTests
{
    [Fact]
    public async Task RefactoringOpportunityWalker_DetectsLargeClass()
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
        var walker = new RefactoringOpportunityWalker();
        walker.Visit(tree.GetRoot());
        await walker.PostProcessAsync();

        Assert.Contains(walker.Suggestions, s => s.Contains("LargeClass"));
    }

    [Fact]
    public async Task RefactoringOpportunityWalker_DetectsUnusedPrivateMethod()
    {
        var code = @"
class C
{
    private void UnusedMethod() { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new RefactoringOpportunityWalker();
        walker.Visit(tree.GetRoot());
        await walker.PostProcessAsync();

        Assert.Contains(walker.Suggestions, s => s.Contains("UnusedMethod") && s.Contains("safe-delete"));
    }

    [Fact]
    public async Task RefactoringOpportunityWalker_DetectsUnusedField()
    {
        var code = @"
class C
{
    private int unusedField;
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new RefactoringOpportunityWalker();
        walker.Visit(tree.GetRoot());
        await walker.PostProcessAsync();

        Assert.Contains(walker.Suggestions, s => s.Contains("unusedField") && s.Contains("safe-delete"));
    }

    [Fact]
    public async Task RefactoringOpportunityWalker_DetectsMethodWithTooManyParameters()
    {
        var code = @"
class C
{
    void TooManyParams(int a, int b, int c, int d, int e) { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new RefactoringOpportunityWalker();
        walker.Visit(tree.GetRoot());
        await walker.PostProcessAsync();

        Assert.Contains(walker.Suggestions, s => s.Contains("TooManyParams") && s.Contains("5 parameters"));
    }

    [Fact]
    public async Task RefactoringOpportunityWalker_CombinesMultipleSuggestions()
    {
        var code = @"
class LargeClass
{
    private int unusedField;
    private void UnusedMethod() { }
    void TooManyParams(int a, int b, int c, int d, int e) { }
    void M1() { } void M2() { } void M3() { } void M4() { } void M5() { }
    void M6() { } void M7() { } void M8() { } void M9() { } void M10() { }
    void M11() { } void M12() { } void M13() { } void M14() { } void M15() { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new RefactoringOpportunityWalker();
        walker.Visit(tree.GetRoot());
        await walker.PostProcessAsync();

        // Should have multiple suggestions
        Assert.True(walker.Suggestions.Count >= 3);
    }

    [Fact]
    public async Task RefactoringOpportunityWalker_NoSuggestionsForCleanCode()
    {
        var code = @"
class CleanClass
{
    private int usedField;

    public int GetField() => usedField;
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new RefactoringOpportunityWalker();
        walker.Visit(tree.GetRoot());
        await walker.PostProcessAsync();

        // Field is used, class is small - no suggestions expected
        Assert.Empty(walker.Suggestions);
    }

    [Fact]
    public async Task RefactoringOpportunityWalker_HandlesEmptyFile()
    {
        var code = "";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new RefactoringOpportunityWalker();
        walker.Visit(tree.GetRoot());
        await walker.PostProcessAsync();

        Assert.Empty(walker.Suggestions);
    }

    [Fact]
    public async Task RefactoringOpportunityWalker_DoesNotFlagPublicMethods()
    {
        var code = @"
class C
{
    public void PublicMethod() { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new RefactoringOpportunityWalker();
        walker.Visit(tree.GetRoot());
        await walker.PostProcessAsync();

        Assert.DoesNotContain(walker.Suggestions, s => s.Contains("PublicMethod"));
    }

    [Fact]
    public async Task RefactoringOpportunityWalker_HandlesMultipleClasses()
    {
        var code = @"
class Small1 { void M() { } }
class Small2 { void M() { } }
class Large
{
    void M1() { } void M2() { } void M3() { } void M4() { } void M5() { }
    void M6() { } void M7() { } void M8() { } void M9() { } void M10() { }
    void M11() { } void M12() { } void M13() { } void M14() { } void M15() { }
    void M16() { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new RefactoringOpportunityWalker();
        walker.Visit(tree.GetRoot());
        await walker.PostProcessAsync();

        // Only Large class should be flagged
        Assert.Contains(walker.Suggestions, s => s.Contains("Large"));
        Assert.DoesNotContain(walker.Suggestions, s => s.Contains("Small1"));
        Assert.DoesNotContain(walker.Suggestions, s => s.Contains("Small2"));
    }

    [Fact]
    public async Task RefactoringOpportunityWalker_SuggestsActionableRefactorings()
    {
        var code = @"
class C
{
    private int unused;
    private void UnusedHelper() { }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new RefactoringOpportunityWalker();
        walker.Visit(tree.GetRoot());
        await walker.PostProcessAsync();

        // All suggestions should include actionable tool names
        foreach (var suggestion in walker.Suggestions)
        {
            Assert.True(
                suggestion.Contains("safe-delete") ||
                suggestion.Contains("move-method") ||
                suggestion.Contains("make-static") ||
                suggestion.Contains("splitting") ||
                suggestion.Contains("introduce-parameter-object"),
                $"Suggestion should contain actionable tool: {suggestion}");
        }
    }
}
