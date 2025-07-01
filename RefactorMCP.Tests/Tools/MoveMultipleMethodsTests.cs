using Xunit;
using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using RefactorMCP.ConsoleApp.Move;

public class MoveMultipleMethodsTests
{
    private static string MoveMultipleMethodsInSource(
        string source,
        string[] sourceClasses,
        string[] methodNames,
        string[] targetClasses,
        string[] accessMembers,
        string[] accessMemberTypes,
        bool[] isStatic)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetRoot();

        var orderedIndices = MoveMultipleMethodsTool.OrderOperations(root, sourceClasses, methodNames);

        foreach (var i in orderedIndices)
        {
            if (isStatic[i])
            {
                var moveResult = MoveMethodAst.MoveStaticMethodAst(root, methodNames[i], targetClasses[i]);
                root = MoveMethodAst.AddMethodToTargetClass(moveResult.NewSourceRoot, targetClasses[i], moveResult.MovedMethod, moveResult.Namespace);
            }
            else
            {
                var moveResult = MoveMethodAst.MoveInstanceMethodAst(root, sourceClasses[i], methodNames[i], targetClasses[i], accessMembers[i], accessMemberTypes[i]);
                root = MoveMethodAst.AddMethodToTargetClass(moveResult.NewSourceRoot, targetClasses[i], moveResult.MovedMethod, moveResult.Namespace);
            }
        }

        var workspace = new AdhocWorkspace();
        var formattedRoot = Formatter.Format(root, workspace);
        return formattedRoot.ToFullString();
    }

    [Fact]
    public void MoveMultipleMethods_WithStaticMethods_ShouldMoveCorrectly()
    {
        var source = @"
using System;

public class SourceClass
{
    public static int Method1() { return 1; }
    public static int Method2() { return 2; }
}

public class TargetClass
{
}";

        var sourceClasses = new[] { "SourceClass", "SourceClass" };
        var methodNames = new[] { "Method1", "Method2" };
        var targetClasses = new[] { "TargetClass", "TargetClass" };
        var accessMembers = new[] { "", "" };
        var accessMemberTypes = new[] { "", "" };
        var isStatic = new[] { true, true };

        var result = MoveMultipleMethodsInSource(
            source, sourceClasses, methodNames, targetClasses, accessMembers, accessMemberTypes, isStatic);

        var targetClassCode = result.Split(new[] { "public class TargetClass" }, StringSplitOptions.None)[1];
        var sourceClassCode = result.Split(new[] { "public class SourceClass" }, StringSplitOptions.None)[1].Split(new[] { "public class TargetClass" }, StringSplitOptions.None)[0];

        Assert.Contains("public static int Method1()", targetClassCode);
        Assert.Contains("public static int Method2()", targetClassCode);
        Assert.DoesNotContain("public static int Method1() { return 1; }", sourceClassCode);
        Assert.DoesNotContain("public static int Method2() { return 2; }", sourceClassCode);
        Assert.Contains("return TargetClass.Method1()", sourceClassCode);
        Assert.Contains("return TargetClass.Method2()", sourceClassCode);
    }

    [Fact]
    public void MoveMultipleMethods_WithInstanceMethods_ShouldMoveCorrectly()
    {
        var source = @"
using System;

public class SourceClass
{
    private int field1 = 1;
    public int Method1() { return field1; }
    public int Method2() { return field1 + 1; }
}

public class TargetClass
{
    private int field1 = 1;
}";

        var sourceClasses = new[] { "SourceClass", "SourceClass" };
        var methodNames = new[] { "Method1", "Method2" };
        var targetClasses = new[] { "TargetClass", "TargetClass" };
        var accessMembers = new[] { "field1", "field1" };
        var accessMemberTypes = new[] { "field", "field" };
        var isStatic = new[] { false, false };

        var result = MoveMultipleMethodsInSource(
            source, sourceClasses, methodNames, targetClasses, accessMembers, accessMemberTypes, isStatic);

        var targetClassCode = result.Split(new[] { "public class TargetClass" }, StringSplitOptions.None)[1];
        var sourceClassCode = result.Split(new[] { "public class SourceClass" }, StringSplitOptions.None)[1].Split(new[] { "public class TargetClass" }, StringSplitOptions.None)[0];

        Assert.Contains("public static int Method1(int field1)", targetClassCode);
        Assert.Contains("public static int Method2(int field1)", targetClassCode);
        Assert.DoesNotContain("public int Method1() { return field1; }", sourceClassCode);
        Assert.DoesNotContain("public int Method2() { return field1 + 1; }", sourceClassCode);
        Assert.Contains("return TargetClass.Method1(field1)", sourceClassCode);
        Assert.Contains("return TargetClass.Method2(field1)", sourceClassCode);
    }

    [Fact]
    public void MoveMultipleMethods_WithMixedMethods_ShouldMoveCorrectly()
    {
        var source = @"
using System;

public class SourceClass
{
    private int field1 = 1;
    public static int Method1() { return 1; }
    public int Method2() { return field1; }
}

public class TargetClass
{
    private int field1 = 1;
}";

        var sourceClasses = new[] { "SourceClass", "SourceClass" };
        var methodNames = new[] { "Method1", "Method2" };
        var targetClasses = new[] { "TargetClass", "TargetClass" };
        var accessMembers = new[] { "", "field1" };
        var accessMemberTypes = new[] { "", "field" };
        var isStatic = new[] { true, false };

        var result = MoveMultipleMethodsInSource(
            source, sourceClasses, methodNames, targetClasses, accessMembers, accessMemberTypes, isStatic);

        var targetClassCode = result.Split(new[] { "public class TargetClass" }, StringSplitOptions.None)[1];
        var sourceClassCode = result.Split(new[] { "public class SourceClass" }, StringSplitOptions.None)[1].Split(new[] { "public class TargetClass" }, StringSplitOptions.None)[0];

        Assert.Contains("public static int Method1()", targetClassCode);
        Assert.Contains("public static int Method2(int field1)", targetClassCode);
        Assert.DoesNotContain("public static int Method1() { return 1; }", sourceClassCode);
        Assert.DoesNotContain("public int Method2() { return field1; }", sourceClassCode);
        Assert.Contains("return TargetClass.Method1()", sourceClassCode);
        Assert.Contains("return TargetClass.Method2(field1)", sourceClassCode);
    }

    [Fact]
    public void OrderOperations_WithOverloadedMethods_ShouldHandleDuplicates()
    {
        var source = @"
class SourceClass
{
    public void Foo() { }
    public void Foo(int x) { }
}
";

        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetRoot();

        var indices = MoveMultipleMethodsTool.OrderOperations(
            root,
            new[] { "SourceClass", "SourceClass" },
            new[] { "Foo", "Foo" });

        Assert.Equal(2, indices.Count);
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
