using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace RefactorMCP.Tests.SyntaxRewriters;

public class InstanceMemberQualifierRewriterTests
{
    [Fact]
    public void InstanceMemberQualifierRewriter_QualifiesMember()
    {
        var method = SyntaxFactory.ParseMemberDeclaration("void M(){ Value = 1; }") as MethodDeclarationSyntax;
        var rewriter = new InstanceMemberQualifierRewriter("inst", knownMembers: new HashSet<string> { "Value" });
        var result = rewriter.Visit(method!)!.NormalizeWhitespace().ToFullString();
        Assert.Contains("inst.Value", result);
    }
}
