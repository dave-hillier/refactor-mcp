using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

/// <summary>
/// Metadata for a single refactoring tool or prompt, derived from its
/// <c>[McpServerTool]</c> / <c>[McpServerPrompt]</c> attributes so the
/// attributes stay the single source of truth for CLI help and MCP schema.
/// </summary>
internal sealed class ToolDescriptor
{
    public ToolDescriptor(MethodInfo method, bool isPrompt)
    {
        Method = method;
        IsPrompt = isPrompt;
        MethodName = method.Name;
        Name = ToolDispatcher.ToKebabCase(method.Name);
        Description = method.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .OfType<System.ComponentModel.DescriptionAttribute>()
            .FirstOrDefault()?.Description;
        Parameters = method.GetParameters().Select(p => new ToolParameterDescriptor(p)).ToList();
    }

    /// <summary>Kebab-case name used on the command line, e.g. <c>extract-method</c>.</summary>
    public string Name { get; }

    /// <summary>Method name as written in C#, e.g. <c>ExtractMethod</c>.</summary>
    public string MethodName { get; }

    public string? Description { get; }

    public bool IsPrompt { get; }

    public MethodInfo Method { get; }

    public IReadOnlyList<ToolParameterDescriptor> Parameters { get; }

    /// <summary>Parameters a caller is expected to supply, in declaration order.</summary>
    public IEnumerable<ToolParameterDescriptor> CallerParameters => Parameters.Where(p => !p.IsInjected);

    /// <summary>
    /// Finds a parameter by the name a caller uses on the command line. Any
    /// spelling works (<c>--methodNames</c>, <c>--method-names</c>) and a
    /// redundant <c>Path</c> suffix may be dropped, so <c>--file</c> means
    /// <c>filePath</c>.
    /// </summary>
    public ToolParameterDescriptor? FindParameter(string name)
    {
        var wanted = ToolDispatcher.NormalizeName(name);
        var callers = CallerParameters.ToList();

        return callers.FirstOrDefault(p => ToolDispatcher.NormalizeName(p.Name) == wanted)
            ?? callers.FirstOrDefault(p => WithoutPathSuffix(ToolDispatcher.NormalizeName(p.Name)) == wanted);
    }

    private static string WithoutPathSuffix(string name)
        => name.EndsWith("path", StringComparison.Ordinal) ? name[..^4] : name;

    public override string ToString() => Name;
}
