using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace RefactorMCP.Tests.SyntaxRewriters;

public class FeatureFlagRewriterTests
{
    [Fact]
    public void FeatureFlagRewriter_ReplacesIfStatementWithStrategyCall()
    {
        var code = @"
class Service
{
    private readonly IFeatureFlags flags;

    public Service(IFeatureFlags flags)
    {
        this.flags = flags;
    }

    void DoWork()
    {
        if (flags.IsEnabled(""NewFeature""))
        {
            Console.WriteLine(""New"");
        }
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var rewriter = new FeatureFlagRewriter("NewFeature");
        var result = rewriter.Visit(tree.GetRoot());

        var resultText = result.ToFullString();
        Assert.Contains("_newFeatureStrategy.Apply()", resultText);
        Assert.DoesNotContain("flags.IsEnabled", resultText);
    }

    [Fact]
    public void FeatureFlagRewriter_AddsStrategyField()
    {
        var code = @"
class Service
{
    private readonly IFeatureFlags flags;

    public Service(IFeatureFlags flags)
    {
        this.flags = flags;
    }

    void DoWork()
    {
        if (flags.IsEnabled(""CoolFeature""))
        {
            Console.WriteLine(""Cool"");
        }
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var rewriter = new FeatureFlagRewriter("CoolFeature");
        var result = rewriter.Visit(tree.GetRoot());

        var resultText = result.ToFullString();
        Assert.Contains("private readonly ICoolFeatureStrategy _coolFeatureStrategy", resultText);
    }

    [Fact]
    public void FeatureFlagRewriter_AddsConstructorParameter()
    {
        var code = @"
class Service
{
    private readonly IFeatureFlags flags;

    public Service(IFeatureFlags flags)
    {
        this.flags = flags;
    }

    void DoWork()
    {
        if (flags.IsEnabled(""MyFeature""))
        {
            Console.WriteLine(""Feature"");
        }
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var rewriter = new FeatureFlagRewriter("MyFeature");
        var result = rewriter.Visit(tree.GetRoot());

        var resultText = result.ToFullString();
        Assert.Contains("IMyFeatureStrategy myFeatureStrategy", resultText);
        Assert.Contains("_myFeatureStrategy = myFeatureStrategy", resultText);
    }

    [Fact]
    public void FeatureFlagRewriter_GeneratesStrategyInterface()
    {
        var code = @"
class Service
{
    void DoWork()
    {
        if (flags.IsEnabled(""Test""))
        {
            Console.WriteLine(""Test"");
        }
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var rewriter = new FeatureFlagRewriter("Test");
        rewriter.Visit(tree.GetRoot());

        var generated = rewriter.GeneratedMembers;
        Assert.NotEmpty(generated);

        var generatedText = string.Join("\n", generated.Select(m => m.ToFullString()));
        Assert.Contains("interface ITestStrategy", generatedText);
        Assert.Contains("void Apply()", generatedText);
    }

    [Fact]
    public void FeatureFlagRewriter_GeneratesEnabledStrategy()
    {
        var code = @"
class Service
{
    void DoWork()
    {
        if (flags.IsEnabled(""Feature""))
        {
            Console.WriteLine(""Enabled"");
        }
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var rewriter = new FeatureFlagRewriter("Feature");
        rewriter.Visit(tree.GetRoot());

        var generated = rewriter.GeneratedMembers;
        var generatedText = string.Join("\n", generated.Select(m => m.ToFullString()));

        Assert.Contains("class FeatureStrategy", generatedText);
        Assert.Contains("IFeatureStrategy", generatedText);
        Assert.Contains("Console.WriteLine(\"Enabled\")", generatedText);
    }

    [Fact]
    public void FeatureFlagRewriter_GeneratesDisabledStrategy()
    {
        var code = @"
class Service
{
    void DoWork()
    {
        if (flags.IsEnabled(""Feature""))
        {
            Console.WriteLine(""Enabled"");
        }
        else
        {
            Console.WriteLine(""Disabled"");
        }
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var rewriter = new FeatureFlagRewriter("Feature");
        rewriter.Visit(tree.GetRoot());

        var generated = rewriter.GeneratedMembers;
        var generatedText = string.Join("\n", generated.Select(m => m.ToFullString()));

        Assert.Contains("class NoFeatureStrategy", generatedText);
        Assert.Contains("Console.WriteLine(\"Disabled\")", generatedText);
    }

    [Fact]
    public void FeatureFlagRewriter_HandlesNoElseBranch()
    {
        var code = @"
class Service
{
    void DoWork()
    {
        if (flags.IsEnabled(""Feature""))
        {
            DoSomething();
        }
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var rewriter = new FeatureFlagRewriter("Feature");
        rewriter.Visit(tree.GetRoot());

        var generated = rewriter.GeneratedMembers;
        var generatedText = string.Join("\n", generated.Select(m => m.ToFullString()));

        // NoFeatureStrategy should have empty body
        Assert.Contains("class NoFeatureStrategy", generatedText);
    }

    [Fact]
    public void FeatureFlagRewriter_OnlyReplacesFirstMatch()
    {
        var code = @"
class Service
{
    void DoWork()
    {
        if (flags.IsEnabled(""Feature""))
        {
            Console.WriteLine(""First"");
        }

        if (flags.IsEnabled(""Feature""))
        {
            Console.WriteLine(""Second"");
        }
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var rewriter = new FeatureFlagRewriter("Feature");
        var result = rewriter.Visit(tree.GetRoot());

        var resultText = result.ToFullString();
        // First if should be replaced
        Assert.Contains("_featureStrategy.Apply()", resultText);
        // Second if should remain
        Assert.Contains("flags.IsEnabled(\"Feature\")", resultText);
    }

    [Fact]
    public void FeatureFlagRewriter_IgnoresNonMatchingFlags()
    {
        var code = @"
class Service
{
    void DoWork()
    {
        if (flags.IsEnabled(""OtherFeature""))
        {
            Console.WriteLine(""Other"");
        }
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var rewriter = new FeatureFlagRewriter("Feature");
        var result = rewriter.Visit(tree.GetRoot());

        var resultText = result.ToFullString();
        // Should remain unchanged
        Assert.Contains("flags.IsEnabled(\"OtherFeature\")", resultText);
        Assert.DoesNotContain("_featureStrategy", resultText);
    }

    [Fact]
    public void FeatureFlagRewriter_HandlesBlocklessIfStatement()
    {
        var code = @"
class Service
{
    void DoWork()
    {
        if (flags.IsEnabled(""Feature""))
            DoSomething();
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var rewriter = new FeatureFlagRewriter("Feature");
        rewriter.Visit(tree.GetRoot());

        var generated = rewriter.GeneratedMembers;
        var generatedText = string.Join("\n", generated.Select(m => m.ToFullString()));

        // Should wrap in block
        Assert.Contains("DoSomething()", generatedText);
    }
}
