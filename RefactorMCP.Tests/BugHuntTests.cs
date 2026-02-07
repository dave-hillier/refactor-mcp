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
/// Failing tests that expose bugs found during code review.
/// Each test documents a specific bug hypothesis and provides a minimal reproduction.
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
    // BUG 2: FeatureFlagRewriter creates Apply() methods without public modifier.
    //        Interface implementations must be public, so the generated strategy
    //        classes will not compile.
    // File: RefactorMCP.ConsoleApp/SyntaxRewriters/FeatureFlagRewriter.cs:97
    // =========================================================================

    [Fact]
    public void FeatureFlagRewriter_StrategyClasses_ApplyMethodShouldBePublic()
    {
        var code = @"
class Service
{
    void DoWork()
    {
        if (flags.IsEnabled(""Feature""))
        {
            Console.WriteLine(""Enabled"");
        }
        else
        {
            Console.WriteLine(""Disabled"");
        }
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var rewriter = new FeatureFlagRewriter("Feature");
        rewriter.Visit(tree.GetRoot());

        var generated = rewriter.GeneratedMembers;
        var generatedText = string.Join("\n", generated.Select(m => m.NormalizeWhitespace().ToFullString()));

        // The Apply() method on both strategy classes should be public
        // to satisfy the interface contract
        Assert.Contains("public void Apply()", generatedText);
    }

    // =========================================================================
    // BUG 3: SetterToInitRewriter drops access modifiers from the setter.
    //        A property with "protected set;" becomes just "init;" (losing protected).
    // File: RefactorMCP.ConsoleApp/SyntaxRewriters/SetterToInitRewriter.cs:25-26
    // =========================================================================

    [Fact]
    public void SetterToInitRewriter_ProtectedSetter_ShouldPreserveAccessModifier()
    {
        var code = "public int P { get; protected set; }";
        var prop = SyntaxFactory.ParseMemberDeclaration(code) as PropertyDeclarationSyntax;
        Assert.NotNull(prop);

        var rewriter = new SetterToInitRewriter("P");
        var result = rewriter.Visit(prop!)!.NormalizeWhitespace().ToFullString();

        // The "protected" access modifier should be preserved on the init accessor
        Assert.Contains("protected init", result);
    }

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

    // =========================================================================
    // BUG 6: PrivateFieldInfoWalker only matches fields with explicit 'private'
    //        keyword. In C#, fields without any access modifier are implicitly
    //        private, but they won't be detected.
    // File: RefactorMCP.ConsoleApp/SyntaxWalkers/PrivateFieldInfoWalker.cs:14
    // =========================================================================

    [Fact]
    public void PrivateFieldInfoWalker_ImplicitlyPrivateField_ShouldBeDetected()
    {
        var code = @"
class C
{
    int _data;
    string _name;
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new PrivateFieldInfoWalker();
        walker.Visit(tree.GetRoot());

        // Fields without explicit access modifier are implicitly private in C#
        Assert.Equal(2, walker.Infos.Count);
        Assert.Contains("_data", walker.Infos.Keys);
        Assert.Contains("_name", walker.Infos.Keys);
    }

    // =========================================================================
    // BUG 7: InstanceMemberNameWalker collects static fields and properties,
    //        despite being named "InstanceMember". This causes downstream code
    //        to incorrectly treat static member access as instance member usage.
    // File: RefactorMCP.ConsoleApp/SyntaxWalkers/InstanceMemberNameWalker.cs:8-13
    // =========================================================================

    [Fact]
    public void InstanceMemberNameWalker_ShouldExcludeStaticFields()
    {
        var code = @"
class C
{
    private static int _counter;
    private int _value;
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new InstanceMemberNameWalker();
        walker.Visit(tree.GetRoot());

        // An "InstanceMember" walker should only collect instance members
        Assert.Single(walker.Names);
        Assert.Contains("_value", walker.Names);
        Assert.DoesNotContain("_counter", walker.Names);
    }

    [Fact]
    public void InstanceMemberNameWalker_ShouldExcludeStaticProperties()
    {
        var code = @"
class C
{
    public static int Instance { get; set; }
    public int Value { get; set; }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new InstanceMemberNameWalker();
        walker.Visit(tree.GetRoot());

        Assert.Single(walker.Names);
        Assert.Contains("Value", walker.Names);
        Assert.DoesNotContain("Instance", walker.Names);
    }

    // =========================================================================
    // BUG 8: MethodAnalysisWalker does not detect instance member access
    //        through this.member syntax. The check ma.Expression == node
    //        compares 'this' with '_field', which is always false.
    // File: RefactorMCP.ConsoleApp/SyntaxWalkers/MethodAnalysisWalker.cs:29-34
    // =========================================================================

    [Fact]
    public void MethodAnalysisWalker_ThisDotField_ShouldDetectInstanceMemberUsage()
    {
        var code = @"
class C
{
    int _field;
    void Foo() { this._field = 5; }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.ValueText == "Foo");

        var instanceMembers = new HashSet<string> { "_field" };
        var methodNames = new HashSet<string> { "Foo" };
        var walker = new MethodAnalysisWalker(instanceMembers, methodNames, "Foo");
        walker.Visit(method);

        // this._field accesses an instance member, so UsesInstanceMembers should be true
        Assert.True(walker.UsesInstanceMembers);
    }

    [Fact]
    public void MethodAnalysisWalker_ThisDotFieldRead_ShouldDetectInstanceMemberUsage()
    {
        var code = @"
class C
{
    int _count;
    int GetCount() { return this._count; }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.ValueText == "GetCount");

        var instanceMembers = new HashSet<string> { "_count" };
        var methodNames = new HashSet<string> { "GetCount" };
        var walker = new MethodAnalysisWalker(instanceMembers, methodNames, "GetCount");
        walker.Visit(method);

        Assert.True(walker.UsesInstanceMembers);
    }

    // =========================================================================
    // BUG 9: SafeDeleteTool.SafeDeleteFieldInSource uses references > 1 threshold.
    //        Since the field declaration is a VariableDeclarator token (not
    //        IdentifierNameSyntax), the count only includes usages, not the
    //        declaration. A field used once has count=1, and 1 > 1 is false,
    //        so the field is deleted despite being referenced.
    // File: RefactorMCP.ConsoleApp/Tools/SafeDeleteTool.cs:140-141
    // =========================================================================

    [Fact]
    public void SafeDeleteFieldInSource_FieldUsedOnce_ShouldNotDelete()
    {
        var code = @"
class C
{
    private int _timeout = 30;
    void Configure() { SetTimeout(_timeout); }
}";
        // The field _timeout is used once. SafeDelete should refuse to delete it.
        var ex = Assert.Throws<McpException>(() =>
            SafeDeleteTool.SafeDeleteFieldInSource(code, "_timeout"));
        Assert.Contains("referenced", ex.Message);
    }

    // =========================================================================
    // BUG 10: SafeDeleteTool.SafeDeleteMethodInSource only detects bare
    //         IdentifierNameSyntax invocations. Calls via this.Method() or
    //         obj.Method() are missed, so the method is deleted despite being called.
    // File: RefactorMCP.ConsoleApp/Tools/SafeDeleteTool.cs:194-195
    // =========================================================================

    [Fact]
    public void SafeDeleteMethodInSource_MethodCalledViaThis_ShouldNotDelete()
    {
        var code = @"
class C
{
    private void Helper() { Console.WriteLine(""help""); }
    void DoWork() { this.Helper(); }
}";
        // Helper is called via this.Helper() -- it is NOT safe to delete
        var ex = Assert.Throws<McpException>(() =>
            SafeDeleteTool.SafeDeleteMethodInSource(code, "Helper"));
        Assert.Contains("referenced", ex.Message);
    }

    // =========================================================================
    // BUG 11: InlineInvocationRewriter crashes with NullReferenceException
    //         on expression-bodied methods because Body is null.
    // File: RefactorMCP.ConsoleApp/SyntaxRewriters/InlineInvocationRewriter.cs:57
    // =========================================================================

    [Fact]
    public void InlineInvocationRewriter_ExpressionBodiedMethod_ShouldNotThrow()
    {
        var code = @"
class C
{
    void Log() => Console.WriteLine(""log"");
    void Call() { Log(); }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        var logMethod = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.ValueText == "Log");

        // The method is expression-bodied (Body == null, ExpressionBody != null)
        Assert.Null(logMethod.Body);
        Assert.NotNull(logMethod.ExpressionBody);

        var rewriter = new InlineInvocationRewriter(logMethod);

        // This should not throw NullReferenceException
        var result = rewriter.Visit(root);
        Assert.NotNull(result);

        var callMethod = result.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.ValueText == "Call");
        var text = callMethod.ToFullString();

        // The inlined code should contain the expression from the arrow body
        Assert.Contains("Console.WriteLine", text);
    }

    // =========================================================================
    // BUG 12: ExtractInterfaceTool.WithBaseList overwrites existing base types.
    //         If a class already has a base class or interfaces, they are removed.
    // File: RefactorMCP.ConsoleApp/Tools/ExtractInterfaceTool.cs:96
    // =========================================================================

    [Fact]
    public async Task ExtractInterface_ClassWithExistingBaseClass_ShouldPreserveIt()
    {
        var code = "public class OrderService : ServiceBase { public void Process() { } }";
        var testDir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "BugHuntTest_" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(testDir);

        try
        {
            UnloadSolutionTool.ClearSolutionCache();
            var solutionPath = TestUtilities.GetSolutionPath();
            await LoadSolutionTool.LoadSolution(solutionPath, null, System.Threading.CancellationToken.None);

            var testFile = System.IO.Path.Combine(testDir, "ExtractBaseTest.cs");
            await TestUtilities.CreateTestFile(testFile, code);
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath);
            var project = solution.Projects.First();
            RefactoringHelpers.AddDocumentToProject(project, testFile);

            var interfacePath = System.IO.Path.Combine(testDir, "IOrderService.cs");
            await ExtractInterfaceTool.ExtractInterface(
                solutionPath, testFile, "OrderService", "Process", interfacePath);

            var source = await System.IO.File.ReadAllTextAsync(testFile);

            // The existing base class ServiceBase should be preserved
            Assert.Contains("ServiceBase", source);
            // The new interface should also be present
            Assert.Contains("IOrderService", source);
        }
        finally
        {
            if (System.IO.Directory.Exists(testDir))
                System.IO.Directory.Delete(testDir, true);
        }
    }

    // =========================================================================
    // BUG 13: ExtractInterfaceTool produces empty accessor list for
    //         expression-bodied properties (AccessorList is null).
    //         The result is "int Prop { }" which is invalid C#.
    // File: RefactorMCP.ConsoleApp/Tools/ExtractInterfaceTool.cs:57-61
    // =========================================================================

    [Fact]
    public async Task ExtractInterface_ExpressionBodiedProperty_ShouldProduceValidAccessor()
    {
        var code = @"public class MyClass { public int Count => _items.Count; }";
        var testDir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "BugHuntTest_" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(testDir);

        try
        {
            UnloadSolutionTool.ClearSolutionCache();
            var solutionPath = TestUtilities.GetSolutionPath();
            await LoadSolutionTool.LoadSolution(solutionPath, null, System.Threading.CancellationToken.None);

            var testFile = System.IO.Path.Combine(testDir, "ExprBodyProp.cs");
            await TestUtilities.CreateTestFile(testFile, code);
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath);
            var project = solution.Projects.First();
            RefactoringHelpers.AddDocumentToProject(project, testFile);

            var interfacePath = System.IO.Path.Combine(testDir, "IMyClass.cs");
            await ExtractInterfaceTool.ExtractInterface(
                solutionPath, testFile, "MyClass", "Count", interfacePath);

            var ifaceContent = await System.IO.File.ReadAllTextAsync(interfacePath);

            // The interface should have a valid getter: "int Count { get; }"
            // Not an empty accessor list: "int Count { }"
            Assert.Contains("get;", ifaceContent);
        }
        finally
        {
            if (System.IO.Directory.Exists(testDir))
                System.IO.Directory.Delete(testDir, true);
        }
    }
}
