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
            "Do",
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
            "Do",
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
                "Do",
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
            "Do",
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
                "Do",
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
            "Do",
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
            "Do",
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
            "Do",
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
            "Do",
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
                "Do",
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
            "Do",
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
        var testFile = Path.GetFullPath(Path.Combine(TestOutputPath, "ComplexInheritedAccess.cs"));
        
        // Create a more realistic scenario that matches your cResRoom.cs example
        var sourceCode = @"
public interface IBaseInitialiser 
{
    ICache Cache { get; }
}

public interface ICache 
{
    IINISite INISite { get; }
}

public interface IINISite
{
    bool TRANSACTIONHANDLING_EnableDocumentTracking { get; }
}

public class BaseClass : IBaseInitialiser
{
    public ICache Cache { get; } = new CacheImpl();
}

public class CacheImpl : ICache
{
    public IINISite INISite { get; } = new INISiteImpl();
}

public class INISiteImpl : IINISite
{
    public bool TRANSACTIONHANDLING_EnableDocumentTracking => true;
}

public class cResRoom : BaseClass
{
    public List<string> colGroupedPostedCharges { get; } = new List<string>();
    
    public void AddDepositRefundFinTransaction(string taxReference)
    {
        // This complex inherited member access should be transformed to @this.Cache.INISite.TRANSACTIONHANDLING_EnableDocumentTracking
        if (Cache.INISite.TRANSACTIONHANDLING_EnableDocumentTracking)
        {
            var sourceDeposit = colGroupedPostedCharges.FirstOrDefault(x => x == taxReference);
            // More complex logic here...
        }
    }
}";

        await TestUtilities.CreateTestFile(testFile, sourceCode);
        await LoadSolutionTool.LoadSolution(SolutionPath);

        var result = await MoveMethodTool.MoveInstanceMethod(
            SolutionPath,
            testFile,
            "cResRoom",
            "AddDepositRefundFinTransaction",
            "TargetClass",
            null,
            Array.Empty<string>(),
            new[] { "this" },
            null,
            CancellationToken.None);

        Assert.Contains("Successfully moved", result);

        // Check the transformation
        var newContent = await File.ReadAllTextAsync(testFile);
        var tree = CSharpSyntaxTree.ParseText(newContent);
        var root = await tree.GetRootAsync();
        
        var targetClass = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == "TargetClass");
        
        var movedMethod = targetClass.Members.OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.ValueText == "AddDepositRefundFinTransaction");
        
        var methodBody = movedMethod.Body.ToString();
        
        // Check for the bug: complex inherited member access should be transformed
        bool hasCorrectTransformation = methodBody.Contains("@this.Cache.INISite.TRANSACTIONHANDLING_EnableDocumentTracking");
        bool hasBuggyAccess = methodBody.Contains("Cache.INISite.TRANSACTIONHANDLING_EnableDocumentTracking") && 
                             !methodBody.Contains("@this.Cache.INISite.TRANSACTIONHANDLING_EnableDocumentTracking");
        
        if (hasBuggyAccess)
        {
            throw new Exception($"BUG REPRODUCED: Complex inherited member access was not transformed. Method body: {methodBody}");
        }
        
        if (!hasCorrectTransformation)
        {
            throw new Exception($"BUG REPRODUCED: Expected @this.Cache.INISite.TRANSACTIONHANDLING_EnableDocumentTracking but got: {methodBody}");
        }
        
        Assert.Contains("@this.Cache.INISite.TRANSACTIONHANDLING_EnableDocumentTracking", methodBody);
    }
}
