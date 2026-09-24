using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using ModelContextProtocol;
using RefactorMCP.ConsoleApp.SyntaxWalkers;
using Xunit;

namespace RefactorMCP.Tests;

/// <summary>
/// Regression tests for the defects found in the review recorded in
/// BUG-REPORT.md. Each test pins one defect with a minimal reproduction, and the
/// numbered comments name it as it was when the test was written; the tests
/// themselves are what has to keep passing.
/// </summary>
public class BugHuntTests
{
    // =========================================================================
    // BUG 1: BodyOmitter.VisitArrowExpressionClause returns BlockSyntax
    //        where an ArrowExpressionClauseSyntax is expected by the parent node.
    //        This causes an InvalidCastException when Roslyn reconstructs the tree.
    // File: RefactorMCP.ConsoleApp/SyntaxRewriters/BodyOmitter.cs:14
    // =========================================================================

    [Fact]
    public void BodyOmitter_ExpressionBodiedMethod_ShouldNotThrow()
    {
        var code = @"
class C
{
    int GetValue() => 42;
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var rewriter = new BodyOmitter();

        // This should not throw, but VisitArrowExpressionClause returns a BlockSyntax
        // where Roslyn expects an ArrowExpressionClauseSyntax, causing InvalidCastException
        var result = rewriter.Visit(root);
        Assert.NotNull(result);

        // The result should be valid syntax (parseable without errors)
        var resultText = result.ToFullString();
        var reparsed = CSharpSyntaxTree.ParseText(resultText);
        Assert.Empty(reparsed.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void BodyOmitter_ExpressionBodiedProperty_ShouldNotThrow()
    {
        var code = @"
class C
{
    public int Count => _list.Count;
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var rewriter = new BodyOmitter();

        // Expression-bodied property should also work without crashing
        var result = rewriter.Visit(root);
        Assert.NotNull(result);
    }

    // =========================================================================
    // BUG 3: SetterToInitRewriter drops access modifiers from the setter.
    //        A property with "private set;" became just "init;" (losing private).
    //        The protected case is pinned by the catalog case
    //        primitives/convert-setter-to-init/protected-setter-in-constructors.
    // File: RefactorMCP.ConsoleApp/SyntaxRewriters/SetterToInitRewriter.cs:25-26
    // =========================================================================

    [Fact]
    public void SetterToInitRewriter_PrivateSetter_ShouldPreserveAccessModifier()
    {
        var code = "public string Name { get; private set; }";
        var prop = SyntaxFactory.ParseMemberDeclaration(code) as PropertyDeclarationSyntax;
        Assert.NotNull(prop);

        var rewriter = new SetterToInitRewriter("Name");
        var result = rewriter.Visit(prop!)!.NormalizeWhitespace().ToFullString();

        // The "private" access modifier should be preserved on the init accessor
        Assert.Contains("private init", result);
    }

    // =========================================================================
    // BUG 4: UnusedMembersWalker uses count <= 1 threshold for fields.
    //        Since field declaration tokens are not IdentifierNameSyntax, the
    //        declaration doesn't count. A field used exactly once has count=1,
    //        and 1 <= 1 is true, so it's falsely flagged as unused.
    // File: RefactorMCP.ConsoleApp/SyntaxWalkers/UnusedMembersWalker.cs:115
    // =========================================================================

    [Fact]
    public async Task UnusedMembersWalker_FieldUsedOnce_ShouldNotBeFlaggedAsUnused()
    {
        var code = @"
class C
{
    private int _timeout = 30;
    void Configure() { SetTimeout(_timeout); }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new UnusedMembersWalker();
        walker.Visit(tree.GetRoot());
        await walker.PostProcessAsync();

        // _timeout is used once, so it should NOT be flagged as unused
        Assert.DoesNotContain(walker.Suggestions, s => s.Contains("_timeout"));
    }

    [Fact]
    public async Task UnusedMembersWalker_FieldUsedMultipleTimes_ShouldNotBeFlaggedAsUnused()
    {
        var code = @"
class C
{
    private int _value;
    void Set(int v) { _value = v; }
    int Get() { return _value; }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new UnusedMembersWalker();
        walker.Visit(tree.GetRoot());
        await walker.PostProcessAsync();

        // _value is used multiple times, should NOT be flagged
        Assert.DoesNotContain(walker.Suggestions, s => s.Contains("_value"));
    }

    // =========================================================================
    // BUG 5: UnusedMembersWalker.VisitInvocationExpression only counts bare
    //        IdentifierNameSyntax calls. Qualified calls like this.Method() are
    //        missed, so a method called via this.Method() is falsely flagged unused.
    // File: RefactorMCP.ConsoleApp/SyntaxWalkers/UnusedMembersWalker.cs:36-44
    // =========================================================================

    [Fact]
    public async Task UnusedMembersWalker_MethodCalledViaThis_ShouldNotBeFlaggedAsUnused()
    {
        var code = @"
class C
{
    private void Helper() { }
    void DoWork() { this.Helper(); }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new UnusedMembersWalker();
        walker.Visit(tree.GetRoot());
        await walker.PostProcessAsync();

        // Helper is called via this.Helper(), so it is NOT unused
        Assert.DoesNotContain(walker.Suggestions, s => s.Contains("Helper"));
    }
}
