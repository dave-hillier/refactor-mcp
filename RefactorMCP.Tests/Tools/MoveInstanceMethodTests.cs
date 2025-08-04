using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorMCP.ConsoleApp.Move;

namespace RefactorMCP.Tests;

public class MoveInstanceMethodTests : TestBase
{
    [Fact]
    public async Task MoveInstanceMethod_ReturnsSuccess()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.GetFullPath(Path.Combine(TestOutputPath, "MoveInstanceMethod.cs"));
        await TestUtilities.CreateTestFile(testFile, "public class A { public void Do(){} } public class B { }");
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var result = await MoveMethodTool.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "A",
            new[] { "Do" },
            "B",
            null,
            Array.Empty<string>(),
            Array.Empty<string>(),
            null,
            CancellationToken.None);

        Assert.Contains("Successfully moved", result);
        Assert.Contains("A.Do", result);
        Assert.Contains("B", result);
        Assert.Contains("made static", result);

        var newContent = await File.ReadAllTextAsync(testFile);
        var tree = CSharpSyntaxTree.ParseText(newContent);
        var root = await tree.GetRootAsync();
        var bClass = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == "B");
        var method = bClass.Members.OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.ValueText == "Do");
        Assert.True(method.Modifiers.Any(SyntaxKind.StaticKeyword));
    }

    [Fact]
    public async Task MoveInstanceMethod_AllowsStaticTargetWhenNoDependencies()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.GetFullPath(Path.Combine(TestOutputPath, "MoveInstanceMethodStatic.cs"));
        await TestUtilities.CreateTestFile(testFile, "public class A { public void Do(){} } public static class B { }");
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var result = await MoveMethodTool.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "A",
            new[] { "Do" },
            "B",
            null,
            Array.Empty<string>(),
            Array.Empty<string>(),
            null,
            CancellationToken.None);

        Assert.Contains("Successfully moved", result);
    }

    [Fact]
    public async Task MoveInstanceMethod_FailsWhenMethodIsProtectedOverride()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.GetFullPath(Path.Combine(TestOutputPath, "MoveInstanceProtectedOverride.cs"));
        await TestUtilities.CreateTestFile(testFile, @"public class Base { protected virtual void Do(){} } public class A : Base { protected override void Do(){} } public class B { }");
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        await Assert.ThrowsAsync<McpException>(() =>
            MoveMethodTool.MoveInstanceMethod(
                SolutionPath,
                testFile,
                "A",
                new[] { "Do" },
                "B",
                null,
                Array.Empty<string>(),
                Array.Empty<string>(),
                null,
                CancellationToken.None));
    }

    [Fact]
    public async Task MoveInstanceMethod_FailsOnSecondMove()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.GetFullPath(Path.Combine(TestOutputPath, "MoveInstanceMethodTwice.cs"));
        await TestUtilities.CreateTestFile(testFile, "public class A { public void Do(){} } public class B { }");
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var result = await MoveMethodTool.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "A",
            new[] { "Do" },
            "B",
            null,
            Array.Empty<string>(),
            Array.Empty<string>(),
            null,
            CancellationToken.None);
        Assert.Contains("Successfully moved", result);

        await Assert.ThrowsAsync<McpException>(() =>
            MoveMethodTool.MoveInstanceMethod(
                SolutionPath,
                testFile,
                "A",
                new[] { "Do" },
                "B",
                null,
                Array.Empty<string>(),
                Array.Empty<string>(),
                null,
                CancellationToken.None));
    }

    [Fact]
    public async Task ResetMoveHistory_AllowsRepeatMove()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.GetFullPath(Path.Combine(TestOutputPath, "ResetMoveHistory.cs"));
        await TestUtilities.CreateTestFile(testFile, "public class A { public void Do(){} } public class B { }");
        await LoadSolutionTool.LoadSolution(SolutionPath);

        var result1 = await MoveMethodTool.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "A",
            new[] { "Do" },
            "B",
            null,
            Array.Empty<string>(),
            Array.Empty<string>(),
            null,
            CancellationToken.None);
        Assert.Contains("Successfully moved", result1);

        // Clear move tracking and try again
        MoveMethodTool.ResetMoveHistory();

        var result2 = await MoveMethodTool.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "A",
            new[] { "Do" },
            "B",
            null,
            Array.Empty<string>(),
            Array.Empty<string>(),
            null,
            CancellationToken.None);

        Assert.Contains("Successfully moved", result2);
    }

    [Fact]
    public async Task LoadSolution_ResetsMoveHistory()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.GetFullPath(Path.Combine(TestOutputPath, "LoadSolutionReset.cs"));
        await TestUtilities.CreateTestFile(testFile, "public class A { public void Do(){} } public class B { }");
        await LoadSolutionTool.LoadSolution(SolutionPath);

        var result1 = await MoveMethodTool.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "A",
            new[] { "Do" },
            "B",
            null,
            Array.Empty<string>(),
            Array.Empty<string>(),
            null,
            CancellationToken.None);
        Assert.Contains("Successfully moved", result1);

        await LoadSolutionTool.LoadSolution(SolutionPath);

        var result2 = await MoveMethodTool.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "A",
            new[] { "Do" },
            "B",
            null,
            Array.Empty<string>(),
            Array.Empty<string>(),
            null,
            CancellationToken.None);

        Assert.Contains("Successfully moved", result2);
    }

    [Fact]
    public async Task MoveInstanceMethod_FailureDoesNotRecordHistory()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.GetFullPath(Path.Combine(TestOutputPath, "MoveFailHistory.cs"));
        await TestUtilities.CreateTestFile(testFile, "public class A { public void Do(){} } public class B { }");
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        await Assert.ThrowsAsync<McpException>(() =>
            MoveMethodTool.MoveInstanceMethod(
                SolutionPath,
                testFile,
                "Wrong",
                new[] { "Do" },
                "B",
                null,
                Array.Empty<string>(),
                Array.Empty<string>(),
                null,
                CancellationToken.None));

        var result = await MoveMethodTool.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "A",
            new[] { "Do" },
            "B",
            null,
            Array.Empty<string>(),
            Array.Empty<string>(),
            null,
            CancellationToken.None);

        Assert.Contains("Successfully moved", result);
    }

    [Fact]
    public async Task MoveInstanceMethod_ComplexInheritedMemberAccess_ReproducesBug()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.GetFullPath(Path.Combine(TestOutputPath, "ComplexInheritedBug.cs"));
        var code = @"using System;
using System.Collections.Generic;
using System.Linq;

public class TransactionLineChargesTotalService
{
    public void CopyChargeSettingsToInterfaceList(object sourceData, object settings) { }
}

public class PoliziaMunicipaleModel
{
    protected TransactionLineChargesTotalService objCharges;

    protected void ExecuteCopyChargeSettings(object item)
    {
        objCharges.CopyChargeSettingsToInterfaceList(item, null);
    }
}

public class TargetClass { }";

        await TestUtilities.CreateTestFile(testFile, code);
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        var result = await MoveMethodTool.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "PoliziaMunicipaleModel",
            new[] { "ExecuteCopyChargeSettings" },
            "TargetClass",
            null,
            new[] { "objCharges" },
            Array.Empty<string>(),
            null,
            CancellationToken.None);

        Assert.Contains("Successfully moved", result);
        Assert.Contains("ExecuteCopyChargeSettings", result);
    }

    [Fact]
    public async Task MoveInstanceMethod_WithNestedClassGenerics_ShouldSucceed()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.GetFullPath(Path.Combine(TestOutputPath, "NestedGeneric.cs"));
        var code = @"using System.Collections.Generic;
public class Outer
{
    public class Inner { }
    public List<Inner> MakeList() => new List<Inner>();
    public int CountList(List<Inner> items) => items.Count;
}
public class Target { }";
        await TestUtilities.CreateTestFile(testFile, code);
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        var result = await MoveMultipleMethodsTool.MoveMultipleMethodsStatic(
            SolutionPath,
            testFile,
            "Outer",
            new[] { "MakeList", "CountList" },
            "Target");

        Assert.Contains("Successfully moved", result);
    }

    [Fact]
    public async Task MoveInstanceMethod_WithConditionalAccessPattern_ShouldSucceed()
    {
        // This reproduces the exact pattern from AddDepositFromDepositTransaction that previously caused casting issues
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.GetFullPath(Path.Combine(TestOutputPath, "ConditionalAccessTest.cs"));
        var code = @"
using System;
using System.Collections.Generic;
using System.Linq;

public class BookingLine
{
    public int Id { get; set; }
}

public class DepositTransaction
{
    public int? FolioId { get; set; }
    public decimal Value { get; set; }
    public string PaymentTypeCode { get; set; }
    public int? ParentTransactionId { get; set; }
}

public class GroupedCharge
{
    public string strParam2 { get; set; }
    public int iTransID { get; set; }
}

public class ResTransaction
{
    public int iResBookingLineID { get; set; }
    public int? ParentDepositTransId { get; set; }
}

public class cResRoom
{
    private BookingLine cResBookingLine;
    private List<GroupedCharge> colGroupedPostedCharges;
    
    public void AddDepositFromDepositTransaction(DepositTransaction depositTransaction)
    {
        var resTransaction = new ResTransaction();
        
        // This line previously caused the casting exception - conditional access with member binding
        resTransaction.iResBookingLineID = cResBookingLine?.Id ?? 0;
        
        // This line also previously caused the casting exception - conditional access on method result
        if (depositTransaction.ParentTransactionId.HasValue)
        {
            resTransaction.ParentDepositTransId = colGroupedPostedCharges
                .SingleOrDefault(t => (t.strParam2 ?? "") == depositTransaction.ParentTransactionId.ToString())?
                .iTransID;
        }
    }
}

public class DepositManager 
{
}";

        await TestUtilities.CreateTestFile(testFile, code);
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        // This should succeed - previously failed with casting exception
        var result = await MoveMultipleMethodsTool.MoveMultipleMethodsInstance(
            SolutionPath,
            testFile,
            "cResRoom",
            new[] { "AddDepositFromDepositTransaction" },
            "DepositManager");

        Assert.Contains("Successfully moved", result);
    }

    [Fact]
    public async Task MoveInstanceMethod_WithParameterInjectionAndNamedArguments_ShouldSucceed()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.GetFullPath(Path.Combine(TestOutputPath, "ParameterInjectionNamed.cs"));
        var code = @"
using System;

public class PostingItem 
{
    public PostingItem(decimal amount, string description, DateTime date) { }
}

public class SourceClass
{
    private decimal _amount;
    private string _description; 
    private DateTime _date;
    
    // This method uses instance fields that will be converted to parameters
    // AND uses named arguments that reference these fields - this now works correctly
    public PostingItem CreatePostingItem()
    {
        return new PostingItem(
            amount: _amount,       // _amount becomes parameter, properly handled by rewriters
            description: _description,  // ParameterRewriter and InstanceMemberRewriter work together correctly
            date: _date           // All casting conflicts have been resolved
        );
    }
    
    // Another method that calls CreatePostingItem, creating recursive dependencies  
    public void AddPosting()
    {
        var item = CreatePostingItem();
        ProcessPosting(item: item);  // Named argument with method result
    }
    
    private void ProcessPosting(PostingItem item) { }
}

public class Target { }";

        await TestUtilities.CreateTestFile(testFile, code);
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        // This should now succeed - the ParameterRewriter vs InstanceMemberRewriter conflict has been resolved
        var result = await MoveMultipleMethodsTool.MoveMultipleMethodsInstance(
            SolutionPath,
            testFile,
            "SourceClass",
            new[] { "CreatePostingItem", "AddPosting" },
            "Target");

        Assert.Contains("Successfully moved", result);
    }

    [Fact]
    public async Task MoveInstanceMethod_WithThisQualifierInNamedArgs_ShouldSucceed()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.GetFullPath(Path.Combine(TestOutputPath, "ThisQualifierNamed.cs"));
        var code = @"
using System;

public class Transaction
{
    public Transaction(decimal amount, string type, DateTime when) { }
}

public class SourceClass
{
    public decimal Amount { get; set; }
    public string Type { get; set; }
    public DateTime Timestamp { get; set; }
    
    // Using 'this' explicitly in named arguments now works correctly
    // InstanceMemberRewriter properly handles this after ParameterRewriter
    public Transaction CreateDepositTransaction()
    {
        return new Transaction(
            amount: this.Amount * 1.5m,     // Complex expression with this - now handled correctly
            type: this.Type ?? ""deposit"",  // this. with null coalescing - casting bug fixed
            when: this.Timestamp            // Simple this. reference - works correctly
        );
    }
    
    // Method that calls other methods with 'this' in named args
    public void ProcessTransaction()
    {
        var tx = CreateDepositTransaction();
        LogTransaction(
            transaction: tx,
            source: this.Type,      // this. in named argument - now works
            timestamp: this.Timestamp
        );
    }
    
    private void LogTransaction(Transaction transaction, string source, DateTime timestamp) { }
}

public class Target { }";

        await TestUtilities.CreateTestFile(testFile, code);
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        var result = await MoveMultipleMethodsTool.MoveMultipleMethodsInstance(
            SolutionPath,
            testFile,
            "SourceClass",
            new[] { "CreateDepositTransaction", "ProcessTransaction" },
            "Target");

        Assert.Contains("Successfully moved", result);
    }

    [Fact]
    public async Task MoveInstanceMethod_WithMixedInstanceMembersAndNamedArgs_ShouldSucceed()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.GetFullPath(Path.Combine(TestOutputPath, "MixedInstanceNamed.cs"));
        var code = @"
using System;
using System.Collections.Generic;
using System.Linq;

public class LineItem
{
    public LineItem(decimal price, string desc, int qty = 1) { }
}

public class SourceClass
{
    private List<string> _descriptions;
    private decimal _basePrice;
    private int _defaultQty;
    
    // This combines instance fields, properties, and complex named arguments
    // The rewriter conflict has been resolved - all transformations work together correctly
    public List<LineItem> CreateLineItems()
    {
        return _descriptions.Select(desc => new LineItem(
            price: _basePrice * 1.2m,           // Instance field in expression - properly handled
            desc: desc,                         // Parameter - works correctly  
            qty: _defaultQty                    // Instance field direct - casting issues fixed
        )).ToList();
    }
    
    // Method with conditional member access and named args
    public void ProcessItems()
    {
        var items = CreateLineItems();
        
        // Conditional access with named arguments - this pattern now works correctly
        items?.FirstOrDefault()?.ToString();
        
        ValidateItems(
            items: items,
            minPrice: _basePrice,      // Instance field in named arg - now works
            descriptions: _descriptions // Instance field direct - casting fixed
        );
    }
    
    private void ValidateItems(List<LineItem> items, decimal minPrice, List<string> descriptions) { }
}

public class Target { }";

        await TestUtilities.CreateTestFile(testFile, code);
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        var project = solution.Projects.First();
        RefactoringHelpers.AddDocumentToProject(project, testFile);

        var result = await MoveMultipleMethodsTool.MoveMultipleMethodsInstance(
            SolutionPath,
            testFile,
            "SourceClass",
            new[] { "CreateLineItems", "ProcessItems" },
            "Target");

        Assert.Contains("Successfully moved", result);
    }
}
