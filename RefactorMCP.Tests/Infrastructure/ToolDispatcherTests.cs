using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using RefactorMCP.Tests;
using Xunit;

namespace RefactorMCP.Tests.Infrastructure;

public class ToolDispatcherTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Dispatcher over the probe tools below. <see cref="ToolDispatcher.Default"/>
    /// scans the application assembly, so the probes cannot leak into it.
    /// </summary>
    private static readonly ToolDispatcher Probes = new(typeof(BindingProbeTools).Assembly);

    private static IReadOnlyDictionary<string, JsonElement> Args(string json)
        => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, Options)!;

    [Theory]
    [InlineData("extract-method")]
    [InlineData("ExtractMethod")]
    [InlineData("extractMethod")]
    [InlineData("extract_method")]
    [InlineData("EXTRACT-METHOD")]
    public void Resolve_AcceptsEverySpelling(string name)
    {
        var tool = ToolDispatcher.Default.Resolve(name);

        Assert.NotNull(tool);
        Assert.Equal("ExtractMethod", tool!.MethodName);
        Assert.Equal("extract-method", tool.Name);
    }

    [Fact]
    public void Resolve_ReachesPrompts()
    {
        var tool = ToolDispatcher.Default.Resolve("list-class-lengths");

        Assert.NotNull(tool);
        Assert.True(tool!.IsPrompt);
        Assert.Equal("ListClassLengths", tool.MethodName);
    }

    [Fact]
    public void Resolve_UnknownName_ReturnsNull()
        => Assert.Null(ToolDispatcher.Default.Resolve("no-such-tool"));

    [Fact]
    public void ListTools_AreSortedAndRoundTripThroughResolve()
    {
        var tools = ToolDispatcher.Default.ListTools();

        Assert.NotEmpty(tools);
        Assert.Equal(tools.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal), tools.Select(t => t.Name));
        Assert.All(tools, tool => Assert.Same(tool, ToolDispatcher.Default.Resolve(tool.Name)));
    }

    [Fact]
    public void ListTools_DescribesParametersForHelp()
    {
        var tool = ToolDispatcher.Default.Resolve("extract-method")!;

        Assert.Equal("Extract a code block into a new method (preferred for large C# file refactoring)", tool.Description);
        Assert.Equal(
            new[] { ("solutionPath", "string", true), ("filePath", "string", true), ("selectionRange", "string", true), ("methodName", "string", true) },
            tool.CallerParameters.Select(p => (p.Name, p.TypeName, p.IsRequired)));
    }

    [Fact]
    public void ListTools_HidesInjectedParameters()
    {
        var tool = ToolDispatcher.Default.Resolve("move-static-method")!;

        Assert.DoesNotContain(tool.CallerParameters, p => p.Name == "cancellationToken");
        Assert.DoesNotContain(tool.CallerParameters, p => p.Name == "progress");
        Assert.Equal("targetFilePath", tool.CallerParameters.Last().Name);
        Assert.False(tool.CallerParameters.Last().IsRequired);
    }

    [Fact]
    public async Task Invoke_BindsStringsIntsBoolsArraysAndObjects()
    {
        var result = await Probes.InvokeAsync("probe", Args("""
            {"text":"hello","count":3,"flag":true,"names":["a","b"],"map":{"x":"1","y":"2"}}
            """));

        Assert.False(result.IsError);
        Assert.Equal("hello | 3 | True | a,b | x=1,y=2 | default | False", result.Text);
    }

    /// <summary>
    /// The shape a command line call arrives in: every value is text, and only
    /// the parameter it is aimed at says how to read it.
    /// </summary>
    [Fact]
    public async Task Invoke_TextValues_AreConvertedToParameterTypes()
    {
        var tool = Probes.Resolve("probe")!;
        var arguments = new Dictionary<string, JsonElement>
        {
            ["text"] = ToolDispatcher.ToJsonElement("hello", typeof(string)),
            ["count"] = ToolDispatcher.ToJsonElement("3", typeof(int)),
            ["flag"] = ToolDispatcher.ToJsonElement("true", typeof(bool)),
            ["names"] = ToolDispatcher.ToJsonElement("a,b", typeof(string[])),
            ["map"] = ToolDispatcher.ToJsonElement("""{"x":"1","y":"2"}""", typeof(Dictionary<string, string>)),
        };

        var result = await Probes.InvokeAsync(tool, arguments);

        Assert.False(result.IsError);
        Assert.Equal("hello | 3 | True | a,b | x=1,y=2 | default | False", result.Text);
    }

    [Fact]
    public async Task Invoke_StringParameterKeepsJsonLookingTextVerbatim()
    {
        var tool = Probes.Resolve("probe")!;
        var arguments = new Dictionary<string, JsonElement>
        {
            ["text"] = ToolDispatcher.ToJsonElement("""{"not":"json for this parameter"}""", typeof(string)),
            ["count"] = ToolDispatcher.ToJsonElement("1", typeof(int)),
            ["flag"] = ToolDispatcher.ToJsonElement("false", typeof(bool)),
            ["names"] = ToolDispatcher.ToJsonElement("a", typeof(string[])),
            ["map"] = ToolDispatcher.ToJsonElement("{}", typeof(Dictionary<string, string>)),
        };

        var result = await Probes.InvokeAsync(tool, arguments);

        Assert.False(result.IsError);
        Assert.StartsWith("""{"not":"json for this parameter"}""", result.Text);
    }

    [Fact]
    public async Task Invoke_OmittedOptionalParameter_UsesItsDefault()
    {
        var result = await Probes.InvokeAsync("probe", Args(
            """{"text":"t","count":1,"flag":false,"names":[],"map":{}}"""));

        Assert.False(result.IsError);
        Assert.EndsWith("| default | False", result.Text);
    }

    [Fact]
    public async Task Invoke_MissingRequiredParameter_ReportsIt()
    {
        var result = await Probes.InvokeAsync("probe", Args("""{"text":"t"}"""));

        Assert.True(result.IsError);
        Assert.Equal("Error: Missing required parameter 'count'", result.Text);
    }

    [Fact]
    public async Task Invoke_UnknownTool_ReportsIt()
    {
        var result = await Probes.InvokeAsync("not-a-tool", Args("{}"));

        Assert.True(result.IsError);
        Assert.Contains("Unknown tool: not-a-tool", result.Text);
    }

    [Fact]
    public async Task Invoke_UnconvertibleValue_NamesTheParameter()
    {
        var result = await Probes.InvokeAsync("probe", Args(
            """{"text":"t","count":"not-a-number","flag":false,"names":[],"map":{}}"""));

        Assert.True(result.IsError);
        Assert.Contains("count", result.Text);
        Assert.Contains("int", result.Text);
    }

    [Fact]
    public async Task Invoke_ResolvesRelativeFilePathsAgainstTheSolutionsDirectory()
    {
        var solution = TestUtilities.GetSolutionPath();

        try
        {
            var result = await Probes.InvokeAsync("probe-path", Args($$"""
                {"solutionPath":"{{solution}}","filePath":"RefactorMCP.Tests/ExampleCode.cs"}
                """));

            Assert.False(result.IsError);
            Assert.Equal(
                Path.Combine(Path.GetDirectoryName(solution)!, "RefactorMCP.Tests", "ExampleCode.cs"),
                result.Text);
        }
        finally
        {
            UnloadSolutionTool.ClearSolutionCache();
        }
    }

    [Fact]
    public async Task Invoke_WithoutASolution_LeavesRelativePathsToTheCaller()
    {
        UnloadSolutionTool.ClearSolutionCache();

        var result = await Probes.InvokeAsync("probe-path", Args("""{"solutionPath":"","filePath":"Relative.cs"}"""));

        Assert.False(result.IsError);
        Assert.Equal(Path.GetFullPath("Relative.cs"), result.Text);
    }

    [Fact]
    public async Task Invoke_McpException_KeepsItsMessage()
    {
        var result = await Probes.InvokeAsync("failing", Args("{}"));

        Assert.True(result.IsError);
        Assert.Equal("Error: probe failed", result.Text);
    }

    [Fact]
    public async Task Invoke_UnexpectedException_IsReportedAsExecutionFailure()
    {
        var result = await Probes.InvokeAsync("exploding", Args("{}"));

        Assert.True(result.IsError);
        Assert.Equal("Error executing tool: boom", result.Text);
    }

    [Fact]
    public async Task Invoke_AwaitsTaskOfString()
    {
        var result = await Probes.InvokeAsync("async", Args("""{"value":"v"}"""));

        Assert.False(result.IsError);
        Assert.Equal("async:v", result.Text);
    }

    [Fact]
    public async Task Invoke_AwaitsBareTask()
    {
        var result = await Probes.InvokeAsync("bare", Args("{}"));

        Assert.False(result.IsError);
        Assert.Equal("Done", result.Text);
    }
}

/// <summary>Tool shaped methods used to exercise parameter binding without touching Roslyn.</summary>
[McpServerToolType]
public static class BindingProbeTools
{
    [McpServerTool, Description("Echo the bound parameters back")]
    public static string Probe(
        string text,
        int count,
        bool flag,
        string[] names,
        Dictionary<string, string> map,
        string suffix = "default",
        CancellationToken cancellationToken = default)
    {
        return string.Join(
            " | ",
            text,
            count,
            flag,
            string.Join(",", names),
            string.Join(",", map.OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value}")),
            suffix,
            cancellationToken.CanBeCanceled);
    }

    [McpServerTool, Description("Echo the file path the dispatcher resolved")]
    public static string ProbePath(string solutionPath, string filePath) => filePath;

    [McpServerTool, Description("Report a tool level error")]
    public static string Failing() => throw new McpException("Error: probe failed");

    [McpServerTool, Description("Report an unexpected error")]
    public static string Exploding() => throw new InvalidOperationException("boom");

    [McpServerTool, Description("Return a task of string")]
    public static Task<string> Async(string value) => Task.FromResult($"async:{value}");

    [McpServerTool, Description("Return a bare task")]
    public static async Task Bare() => await Task.Yield();
}
