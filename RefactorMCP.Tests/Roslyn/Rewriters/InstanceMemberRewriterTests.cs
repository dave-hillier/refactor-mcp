using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace RefactorMCP.Tests;

public partial class RoslynTransformationTests
{
    [Fact]
    public void InstanceMemberRewriter_QualifiesMembers()
    {
        var method = SyntaxFactory.ParseMemberDeclaration("void Test(){ Value = 1; }") as MethodDeclarationSyntax;
        var rewriter = new InstanceMemberRewriter("inst", new HashSet<string> { "Value" });
        var result = rewriter.Visit(method!)!.NormalizeWhitespace().ToFullString();
        Assert.Contains("inst.Value", result);
    }
}
