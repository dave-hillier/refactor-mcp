using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using RefactorMCP.ConsoleApp.Move;
using Xunit;

namespace RefactorMCP.Tests.Tools;

public class MoveProtectedOverrideDependencyToolTests
{
    [Fact]
    public void MoveMethod_UpdatesProtectedOverrideDependency()
    {
        var source = @"public class Base { protected virtual void DoIt() {} } public class Derived : Base { protected override void DoIt() {} public void Another() { DoIt(); } } public class Target { }";
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetRoot();

        var moveResult = MoveMethodAst.MoveInstanceMethodAst(root, "Derived", "Another", "Target", "", "");
        var updatedRoot = MoveMethodAst.AddMethodToTargetClass(moveResult.NewSourceRoot, "Target", moveResult.MovedMethod, moveResult.Namespace);
        var formattedRoot = Formatter.Format(updatedRoot, RefactoringHelpers.SharedWorkspace);

        var derivedClass = formattedRoot.DescendantNodes().OfType<ClassDeclarationSyntax>().First(c => c.Identifier.ValueText == "Derived");
        var method = derivedClass.Members.OfType<MethodDeclarationSyntax>().First(m => m.Identifier.ValueText == "DoIt");
        var mods = method.Modifiers.ToFullString();

        Assert.Contains("protected", mods);
        Assert.Contains("internal", mods);
        Assert.Contains("override", mods);
    }
}
