using ModelContextProtocol;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RefactorMCP.ConsoleApp.Move;
using System;

namespace RefactorMCP.Tests;

public class MoveMultipleMethodsBugTests : TestBase
{
    [Fact]
    public async Task MoveMultipleMethods_NestedClassGenerics_Fails()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.Combine(TestOutputPath, "NestedGeneric.cs");
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
    public void DirectMoveMethod_WithNamedArgumentsAndThisAccess_ShouldSucceed()
    {
        // This pattern was previously failing but is now fixed after the comprehensive patch
        var sourceCode = @"
using System;

public class PostingItem 
{
    public PostingItem(decimal amount, string description) { }
}

public class cResRoom
{
    public decimal DepositAmount { get; set; }
    public string Description { get; set; }
    
    public PostingItem CreatePostingItem()
    {
        return new PostingItem(
            amount: this.DepositAmount,
            description: this.Description
        );
    }
}

public class DepositManager 
{
}";

        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetRoot();

        // This should now succeed without throwing exceptions - the casting bug has been fixed
        var result = MoveMethodAst.MoveInstanceMethodAst(
            root,
            "cResRoom",
            "CreatePostingItem", 
            "DepositManager",
            "instance",
            "instance"
        );

        // Verify the operation succeeded without throwing an exception
        Assert.NotNull(result);
    }

    [Fact]
    public void DirectMoveMethod_WithConditionalAccessInAssignment_ShouldSucceed()
    {
        // Pattern that previously triggered the MemberBindingExpression casting issue
        // but should now work correctly after the fix
        var sourceCode = @"
using System;

public class DepositTransaction
{
    public string Status { get; set; }
    public DateTime? ProcessedDate { get; set; }
}

public class cResRoom
{
    public DepositTransaction Transaction { get; set; }
    
    public void AddDepositFromDepositTransaction()
    {
        Transaction?.ProcessedDate = DateTime.Now;
        var status = Transaction?.Status;
    }
}

public class DepositManager 
{
}";

        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetRoot();

        // This should now succeed with the fix applied (previously threw InvalidCastException)
        // The main verification is that no exception is thrown
        var result = MoveMethodAst.MoveInstanceMethodAst(
            root,
            "cResRoom", 
            "AddDepositFromDepositTransaction",
            "DepositManager",
            "instance",
            "instance"
        );

        // Verify the operation succeeded without throwing an exception
        Assert.NotNull(result);
    }

    [Fact]
    public void DirectMoveMethod_WithComplexLambdaAndNamedArgs_ShouldSucceed()
    {
        // This pattern was previously failing in GenerateInvoice but is now fixed
        var sourceCode = @"
using System;
using System.Collections.Generic;
using System.Linq;

public class InvoiceItem
{
    public InvoiceItem(decimal amount, string description) { }
}

public class cResRoom
{
    public List<string> Items { get; set; }
    public decimal Amount { get; set; }
    
    public void GenerateInvoice()
    {
        if (Items?.Any() == true)
        {
            var invoiceItems = Items.Select(item => new InvoiceItem(
                amount: this.Amount,
                description: item
            )).ToList();
        }
    }
}

public class DepositManager 
{
}";

        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetRoot();

        // This should now succeed without throwing exceptions - the lambda with named args casting bug has been fixed
        var result = MoveMethodAst.MoveInstanceMethodAst(
            root,
            "cResRoom",
            "GenerateInvoice",
            "DepositManager", 
            "instance",
            "instance"
        );

        // Verify the operation succeeded without throwing an exception
        Assert.NotNull(result);
    }

    [Fact]
    public async Task MoveMethod_WithConditionalAccessPattern_ShouldSucceed()
    {
        // This reproduces the exact pattern from AddDepositFromDepositTransaction that currently causes the casting issue
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.Combine(TestOutputPath, "ConditionalAccessTest.cs");
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
        
        // This line causes the casting exception - conditional access with member binding
        resTransaction.iResBookingLineID = cResBookingLine?.Id ?? 0;
        
        // This line also causes the casting exception - conditional access on method result
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

        // This should succeed but currently fails with casting exception
        var result = await MoveMultipleMethodsTool.MoveMultipleMethodsInstance(
            SolutionPath,
            testFile,
            "cResRoom",
            new[] { "AddDepositFromDepositTransaction" },
            "DepositManager");

        Assert.Contains("Successfully moved", result);
    }

    [Fact]
    public async Task MoveMethod_WithParameterInjectionAndNamedArguments_ShouldSucceed()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.Combine(TestOutputPath, "ParameterInjectionNamed.cs");
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
    public async Task MoveMethod_WithThisQualifierInNamedArgs_ShouldSucceed()  
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.Combine(TestOutputPath, "ThisQualifierNamed.cs");
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
    public async Task MoveMethod_WithMixedInstanceMembersAndNamedArgs_ShouldSucceed()
    {
        UnloadSolutionTool.ClearSolutionCache();
        var testFile = Path.Combine(TestOutputPath, "MixedInstanceNamed.cs");
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

    [Fact]
    public void DirectMoveMethod_WithNamedArgumentMemberAccess_ShouldSucceed()
    {
        // This test reproduces the GetInvoicedTransactionIds scenario
        // where named arguments contain member access expressions (_dbContextFactory)
        // This should work without throwing exceptions
        var sourceCode = @"
using System.Collections.Generic;

public class cReportList
{
    public cReportList(object parent, object dbContextFactory) { }
    public IEnumerable<int> GetInvoicedTransactionIds(string bookRef, int roomId) { return null; }
}

public class cResRoom
{
    private object _dbContextFactory;
    private string strBookRef;
    private int iRoomPickID;
    
    public IEnumerable<int> GetInvoicedTransactionIds()
    {
        var reportList = new cReportList(this, dbContextFactory: _dbContextFactory);
        return reportList.GetInvoicedTransactionIds(strBookRef, iRoomPickID);
    }
}

public class TargetManager { }";

        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetRoot();

        // This should work successfully - the method should be moved without throwing exceptions
        var result = MoveMethodAst.MoveInstanceMethodAst(
            root,
            "cResRoom",
            "GetInvoicedTransactionIds",
            "TargetManager",
            "instance",
            "cResRoom"
        );

        // Verify the operation succeeded without throwing exceptions
        Assert.NotNull(result);
        
        // The main success criteria is that no InvalidCastException was thrown
        // This test reproduces the exact casting bug that was happening with
        // named arguments containing member access expressions like _dbContextFactory
    }
}
