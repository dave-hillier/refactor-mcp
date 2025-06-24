using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RefactorMCP.ConsoleApp.SyntaxWalkers;
using Xunit;

namespace RefactorMCP.Tests.SyntaxWalkers;

public class MethodMetricsWalkerTests
{
    [Fact]
    public void MethodMetricsWalker_DetectsParameterObjectOpportunity()
    {
        var code = @"class C { void Foo(int a, int b, int c, int d, int e) { } }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new MethodMetricsWalker(null);
        walker.Visit(tree.GetRoot());
        Assert.Contains("Method 'Foo' has 5 parameters", walker.Suggestions.First());
    }

    [Fact]
    public void MethodMetricsWalker_SuggestsMakeStaticWhenNoInstanceUsage()
    {
        var code = @"class C { private int x; void Foo() { int y = 0; } }";
        var tree = CSharpSyntaxTree.ParseText(code);
        var refs = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!
            .Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create(
            "test",
            new[] { tree },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var model = compilation.GetSemanticModel(tree);
        var walker = new MethodMetricsWalker(model);
        walker.Visit(tree.GetRoot());
        Assert.Contains("Method 'Foo' does not access instance state", walker.Suggestions.First());
    }
}
