using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace RefactorMCP.Tests.Tools;

public class AstTransformationsTests
{
    [Fact]
    public void EnsureStaticModifier_AddsStaticIfMissing()
    {
        var method = SyntaxFactory.ParseMemberDeclaration("void Test() { }") as MethodDeclarationSyntax;
        var updated = AstTransformations.EnsureStaticModifier(method!);

        Assert.Contains("static", updated.Modifiers.ToFullString());
    }
}
