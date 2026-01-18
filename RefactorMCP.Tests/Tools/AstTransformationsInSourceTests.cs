using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Xunit;

namespace RefactorMCP.Tests.Tools;

public class AstTransformationsInSourceTests
{
    [Fact]
    public void AddParameter_AddsParameterToMethod()
    {
        var method = SyntaxFactory.ParseMemberDeclaration("void Test() { }") as MethodDeclarationSyntax;
        var updated = AstTransformations.AddParameter(method!, "value", "int");

        Assert.Single(updated.ParameterList.Parameters);
        Assert.Equal("value", updated.ParameterList.Parameters[0].Identifier.ValueText);
        Assert.Equal("int", updated.ParameterList.Parameters[0].Type!.ToString());
    }

    [Fact]
    public void ReplaceThisReferences_ReplacesWithParameter()
    {
        var method = SyntaxFactory.ParseMemberDeclaration(
            "void Test() { Console.WriteLine(this.Value); }") as MethodDeclarationSyntax;
        var updated = AstTransformations.ReplaceThisReferences(method!, "instance");
        var output = updated.NormalizeWhitespace().ToFullString();

        Assert.Contains("instance.Value", output);
        Assert.DoesNotContain("this.Value", output);
    }

    [Fact]
    public void QualifyInstanceMembers_AddsParameterQualification()
    {
        var method = SyntaxFactory.ParseMemberDeclaration(
            "void Test() { Console.WriteLine(Value); }") as MethodDeclarationSyntax;
        var qualified = AstTransformations.QualifyInstanceMembers(
            method!,
            "instance",
            new HashSet<string> { "Value" });

        var output = qualified.NormalizeWhitespace().ToFullString();
        Assert.Contains("instance.Value", output);
    }

    [Fact]
    public void EnsureStaticModifier_AddsStaticIfMissing()
    {
        var method = SyntaxFactory.ParseMemberDeclaration("void Test() { }") as MethodDeclarationSyntax;
        var updated = AstTransformations.EnsureStaticModifier(method!);

        Assert.Contains("static", updated.Modifiers.ToFullString());
    }

    [Fact]
    public void AddArgument_AddsArgumentToInvocation()
    {
        var invocation = SyntaxFactory.ParseExpression("M()") as InvocationExpressionSyntax;
        var expr = SyntaxFactory.IdentifierName("x");
        var generator = SyntaxGenerator.GetGenerator(RefactoringHelpers.SharedWorkspace, LanguageNames.CSharp);
        var updated = AstTransformations.AddArgument(invocation!, expr, generator);

        Assert.Equal("M(x)", updated.NormalizeWhitespace().ToFullString());
    }

    [Fact]
    public void RemoveArgument_RemovesArgumentFromInvocation()
    {
        var invocation = SyntaxFactory.ParseExpression("M(a, b)") as InvocationExpressionSyntax;
        var updated = AstTransformations.RemoveArgument(invocation!, 0);

        Assert.Equal("M(b)", updated.NormalizeWhitespace().ToFullString());
    }
}
