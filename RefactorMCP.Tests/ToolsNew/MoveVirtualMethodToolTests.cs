using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using RefactorMCP.ConsoleApp.Move;
using Xunit;

namespace RefactorMCP.Tests.ToolsNew;

public class MoveVirtualMethodToolTests
{
    [Fact]
    public void MoveOverrideMethod_WithBaseCall_AddsWrapper()
    {
        var source = "public class A { public virtual void Method1() {} } " +
                     "public class B : A { public override void Method1() { base.Method1(); System.Console.WriteLine(\"B\"); } }";
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetRoot();

        var moveResult = MoveMethodAst.MoveInstanceMethodAst(root, "B", "Method1", "ExtractedFromB", "", "");
        var updatedRoot = MoveMethodAst.AddMethodToTargetClass(moveResult.NewSourceRoot, "ExtractedFromB", moveResult.MovedMethod, moveResult.Namespace);
        var formattedRoot = Formatter.Format(updatedRoot, new AdhocWorkspace());

        var bClass = formattedRoot.DescendantNodes().OfType<ClassDeclarationSyntax>().First(c => c.Identifier.ValueText == "B");
        Assert.Contains("BaseMethod1", bClass.ToFullString());

        var targetClass = formattedRoot.DescendantNodes().OfType<ClassDeclarationSyntax>().First(c => c.Identifier.ValueText == "ExtractedFromB");
        var moved = targetClass.Members.OfType<MethodDeclarationSyntax>().First();
        Assert.Contains("BaseMethod1", moved.ToFullString());
    }
}
