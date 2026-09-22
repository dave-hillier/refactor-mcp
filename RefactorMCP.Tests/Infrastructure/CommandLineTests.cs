using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using RefactorMCP.Tests.Infrastructure;
using Xunit;

namespace RefactorMCP.Tests;

public class ParsedCommandTests
{
    [Fact]
    public void VerbIsTheFirstArgument()
    {
        Assert.Equal("extract-method", ParsedCommand.Parse(new[] { "extract-method", "--file", "a.cs" }).Verb);
    }

    [Fact]
    public void OptionsTakeOneValueAndLeaveTheRestPositional()
    {
        // --constructor-injections takes 'this'; Logger stays a positional, which
        // is what the documented move-instance-method example relies on.
        var command = ParsedCommand.Parse(new[]
        {
            "move-instance-method", "./App.sln", "./a.cs", "Source", "Method",
            "--constructor-injections", "this", "Target"
        });

        Assert.Equal(new[] { "this" }, command.OptionValues("constructor-injections"));
        Assert.Equal(new[] { "./App.sln", "./a.cs", "Source", "Method", "Target" }, command.Positionals);
    }

    [Fact]
    public void RepeatedOptionsAccumulate()
    {
        var command = ParsedCommand.Parse(new[] { "probe", "--name", "a", "--name", "b" });

        Assert.Equal(new[] { "a", "b" }, command.OptionValues("name"));
    }

    [Fact]
    public void InlineValuesAreAccepted()
    {
        var command = ParsedCommand.Parse(new[] { "probe", "--count=3", "--use-property=false" });

        Assert.Equal("3", command.Option("count"));
        Assert.Equal("false", command.Option("use-property"));
    }

    [Fact]
    public void AFlagWithNoValueIsPresent()
    {
        var command = ParsedCommand.Parse(new[] { "probe", "--verbose" });

        Assert.True(command.HasOption("verbose"));
        Assert.Empty(command.OptionValues("verbose"));
    }

    [Fact]
    public void OptionNamesAreCaseInsensitive()
    {
        var command = ParsedCommand.Parse(new[] { "probe", "--Count", "3" });

        Assert.Equal("3", command.Option("count"));
    }

    [Theory]
    [InlineData("30s", 30)]
    [InlineData("10m", 600)]
    [InlineData("2h", 7200)]
    [InlineData("45", 45)]
    public void DurationsAreRead(string text, double seconds)
    {
        Assert.Equal(seconds, ParsedCommand.ParseDuration(text)!.Value.TotalSeconds);
    }

    [Fact]
    public void DurationsUnderstandMilliseconds()
    {
        Assert.Equal(250, ParsedCommand.ParseDuration("250ms")!.Value.TotalMilliseconds);
    }

    [Fact]
    public void UnreadableDurationsReturnNull()
    {
        Assert.Null(ParsedCommand.ParseDuration("soon"));
    }
}

public class ToolBindingTests
{
    private static readonly ToolDispatcher Probes = new(typeof(BindingProbeTools).Assembly);

    [Fact]
    public async Task FlagsBindToParametersByName()
    {
        var tool = Probes.Resolve("probe")!;
        var command = ParsedCommand.Parse(new[]
        {
            "probe",
            "--text", "hi",
            "--count", "2",
            "--flag",
            "--names", "a", "--names", "b",
            "--map", """{"x":"1"}""",
            "--suffix", "tail"
        });

        Assert.True(ToolCommand.TryBind(tool, command, out var arguments, out var error), error);

        var result = await Probes.InvokeAsync(tool, arguments);
        Assert.False(result.IsError);
        Assert.Equal("hi | 2 | True | a,b | x=1 | tail | False", result.Text);
    }

    [Fact]
    public async Task PositionalArgumentsFillParametersInDeclarationOrder()
    {
        var tool = Probes.Resolve("probe")!;
        var command = ParsedCommand.Parse(new[] { "probe", "hi", "2", "true", "a,b", """{"x":"1"}""" });

        Assert.True(ToolCommand.TryBind(tool, command, out var arguments, out var error), error);

        var result = await Probes.InvokeAsync(tool, arguments);
        Assert.False(result.IsError);
        Assert.Equal("hi | 2 | True | a,b | x=1 | default | False", result.Text);
    }

    [Fact]
    public async Task OptionsAndPositionalsMix()
    {
        var tool = Probes.Resolve("probe")!;
        var command = ParsedCommand.Parse(new[] { "probe", "hi", "--count", "2", "true", "a,b", "{}" });

        Assert.True(ToolCommand.TryBind(tool, command, out var arguments, out var error), error);

        var result = await Probes.InvokeAsync(tool, arguments);
        Assert.False(result.IsError);
        Assert.Equal("hi | 2 | True | a,b |  | default | False", result.Text);
    }

    [Fact]
    public void ThePathSuffixIsOptionalOnFlagNames()
    {
        var tool = Probes.Resolve("probe-path")!;
        var command = ParsedCommand.Parse(new[] { "probe-path", "--solution", "App.sln", "--file", "a.cs" });

        Assert.True(ToolCommand.TryBind(tool, command, out var arguments, out var error), error);
        Assert.True(arguments.ContainsKey("solutionPath"));
        Assert.True(arguments.ContainsKey("filePath"));
    }

    [Fact]
    public void UnknownOptionsAreRejectedWithTheListOfRealOnes()
    {
        var tool = Probes.Resolve("probe")!;
        var command = ParsedCommand.Parse(new[] { "probe", "--nope", "1" });

        Assert.False(ToolCommand.TryBind(tool, command, out _, out var error));
        Assert.Contains("--nope", error);
        Assert.Contains("--text", error);
    }

    [Fact]
    public void TooManyPositionalArgumentsAreRejected()
    {
        var tool = Probes.Resolve("probe")!;
        var command = ParsedCommand.Parse(new[] { "probe", "1", "2", "3", "4", "5", "6", "7" });

        Assert.False(ToolCommand.TryBind(tool, command, out _, out var error));
        Assert.Contains("takes 6 arguments", error);
    }

    [Fact]
    public async Task ValuesThatDoNotFitTheirParameterAreReported()
    {
        var tool = Probes.Resolve("probe")!;
        var command = ParsedCommand.Parse(new[] { "probe", "--text", "hi", "--count", "lots" });

        Assert.True(ToolCommand.TryBind(tool, command, out var arguments, out _));

        var result = await Probes.InvokeAsync(tool, arguments);
        Assert.True(result.IsError);
        Assert.Contains("count", result.Text);
    }

    [Fact]
    public void FlagsCanBeNamedWithTheirExactParameterName()
    {
        var tool = Probes.Resolve("probe")!;
        var command = ParsedCommand.Parse(new[] { "probe", "--text", "hi", "--names", "a,b" });

        Assert.True(ToolCommand.TryBind(tool, command, out var arguments, out var error), error);
        Assert.Equal(JsonValueKind.String, arguments["names"].ValueKind);
    }
}
