using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using ModelContextProtocol.Server;

/// <summary>
/// Reflection based entry point for invoking refactoring tools by name.
///
/// The CLI (<c>--json</c>, <c>--cli</c>, tool options) and the resident daemon
/// both dispatch through here, so name resolution and parameter binding behave
/// identically wherever a tool is invoked.
/// </summary>
internal sealed class ToolDispatcher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IReadOnlyList<ToolDescriptor> _tools;
    private readonly Dictionary<string, ToolDescriptor> _toolsByName;

    public static ToolDispatcher Default { get; } = new(typeof(ToolDispatcher).Assembly);

    public ToolDispatcher(Assembly assembly)
    {
        _tools = Discover(assembly);
        _toolsByName = new Dictionary<string, ToolDescriptor>(StringComparer.Ordinal);

        foreach (var tool in _tools)
        {
            var key = NormalizeName(tool.Name);
            if (_toolsByName.TryGetValue(key, out var existing))
            {
                throw new InvalidOperationException(
                    $"Tools '{existing.MethodName}' and '{tool.MethodName}' both answer to '{tool.Name}'. " +
                    "Give one of them a distinct method name.");
            }
            _toolsByName.Add(key, tool);
        }
    }

    /// <summary>All tools and prompts, ordered by name.</summary>
    public IReadOnlyList<ToolDescriptor> ListTools() => _tools;

    /// <summary>
    /// Finds a tool by name. <c>extract-method</c>, <c>ExtractMethod</c>,
    /// <c>extractMethod</c> and <c>extract_method</c> are all equivalent, and
    /// all forms are matched case insensitively.
    /// </summary>
    public ToolDescriptor? Resolve(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return _toolsByName.TryGetValue(NormalizeName(name), out var tool) ? tool : null;
    }

    public Task<ToolResult> InvokeAsync(
        string name,
        IReadOnlyDictionary<string, JsonElement> arguments,
        CancellationToken cancellationToken = default)
    {
        var tool = Resolve(name);
        if (tool == null)
            return Task.FromResult(ToolResult.Error(
                $"Unknown tool: {name}. Use the list-tools tool to see available commands."));

        return InvokeAsync(tool, arguments, cancellationToken);
    }

    internal static JsonElement ToJsonElement(string raw, Type targetType)
    {
        // A string parameter takes the text as written; a structured one may
        // carry JSON, which ConvertFromString would otherwise mangle.
        if (targetType != typeof(string))
        {
            var trimmed = raw.TrimStart();
            if (trimmed.StartsWith('[') || trimmed.StartsWith('{'))
            {
                try
                {
                    return JsonSerializer.Deserialize<JsonElement>(raw);
                }
                catch (JsonException)
                {
                    // Fall through and let binding report the conversion failure.
                }
            }
        }

        return JsonSerializer.SerializeToElement(raw);
    }

    public async Task<ToolResult> InvokeAsync(
        ToolDescriptor tool,
        IReadOnlyDictionary<string, JsonElement> arguments,
        CancellationToken cancellationToken = default)
    {
        var values = new object?[tool.Parameters.Count];
        for (var i = 0; i < tool.Parameters.Count; i++)
        {
            var parameter = tool.Parameters[i];

            if (parameter.IsInjected)
            {
                values[i] = parameter.ParameterType == typeof(CancellationToken) ? cancellationToken : null;
                continue;
            }

            if (!TryGetArgument(arguments, parameter.Name, out var element))
            {
                if (parameter.HasDefaultValue)
                {
                    values[i] = parameter.DefaultValue;
                    continue;
                }

                return ToolResult.Error($"Error: Missing required parameter '{parameter.Name}'");
            }

            if (!TryConvert(element, parameter, out var converted, out var error))
                return ToolResult.Error(error);

            values[i] = converted;
        }

        ResolvePaths(tool, values);

        ToolResult result;
        try
        {
            result = ToolResult.Ok(await RenderAsync(Invoke(tool, values)));
        }
        catch (Exception ex)
        {
            // Reflection wraps synchronous throws; async tools surface their real
            // exception on await. McpException messages are already user facing
            // ("Error: ..."), everything else is an unexpected failure.
            var cause = ex is TargetInvocationException { InnerException: not null } wrapped
                ? wrapped.InnerException!
                : ex;

            result = ToolResult.Error(cause is McpException
                ? cause.Message
                : $"Error executing tool: {cause.Message}");
        }

        // Recording is opt in, and belongs to the session that ran the tool.
        ToolCallLogger.Log(tool.Name, DescribeArguments(arguments));

        return result;
    }

    /// <summary>The arguments as the caller wrote them, for the call log.</summary>
    private static Dictionary<string, string?> DescribeArguments(IReadOnlyDictionary<string, JsonElement> arguments)
    {
        var described = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var (name, value) in arguments)
        {
            described[name] = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => value.ToString()
            };
        }

        return described;
    }

    /// <summary>
    /// Makes the file paths in a call absolute before the tool sees them.
    /// <c>solutionPath</c> is resolved against the process's current directory,
    /// every other path against that solution's directory. Tools therefore do
    /// not depend on the process's current directory being moved under them.
    /// </summary>
    private static void ResolvePaths(ToolDescriptor tool, object?[] values)
    {
        var session = SessionForCall(tool, values);

        for (var i = 0; i < tool.Parameters.Count; i++)
        {
            var parameter = tool.Parameters[i];
            if (!IsPathParameter(parameter) || values[i] is not string path || string.IsNullOrWhiteSpace(path))
                continue;

            values[i] = IsSolutionPath(parameter)
                ? Path.GetFullPath(path)
                : session?.ResolvePath(path) ?? Path.GetFullPath(path);
        }
    }

    private static SolutionSession? SessionForCall(ToolDescriptor tool, object?[] values)
    {
        var index = IndexOfParameter(tool, "solutionPath");
        if (index >= 0 && values[index] is string solutionPath && File.Exists(solutionPath))
            return SessionRegistry.GetOrCreate(solutionPath);

        return SessionRegistry.Current;
    }

    private static int IndexOfParameter(ToolDescriptor tool, string name)
    {
        for (var i = 0; i < tool.Parameters.Count; i++)
        {
            if (IsSolutionPath(tool.Parameters[i]) && string.Equals(tool.Parameters[i].Name, name, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private static bool IsSolutionPath(ToolParameterDescriptor parameter)
        => string.Equals(parameter.Name, "solutionPath", StringComparison.OrdinalIgnoreCase);

    private static bool IsPathParameter(ToolParameterDescriptor parameter)
        => parameter.ParameterType == typeof(string)
           && parameter.Name.EndsWith("Path", StringComparison.OrdinalIgnoreCase);

    private static object? Invoke(ToolDescriptor tool, object?[] values)
    {
        try
        {
            return tool.Method.Invoke(null, values);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            // Re-throw the tool's own exception with its stack intact so callers
            // see the real failure rather than a reflection wrapper.
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw; // unreachable
        }
    }

    private static async Task<string> RenderAsync(object? result)
    {
        switch (result)
        {
            case null:
                return string.Empty;
            case Task<string> taskOfString:
                return await taskOfString;
            case Task task:
                await task;
                return "Done";
            default:
                return result.ToString() ?? string.Empty;
        }
    }

    private static bool TryGetArgument(
        IReadOnlyDictionary<string, JsonElement> arguments,
        string name,
        out JsonElement value)
    {
        if (arguments.TryGetValue(name, out value))
            return true;

        foreach (var pair in arguments)
        {
            if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryConvert(
        JsonElement value,
        ToolParameterDescriptor parameter,
        out object? converted,
        out string error)
    {
        converted = null;
        error = string.Empty;

        if (value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Undefined)
        {
            if (parameter.IsRequired)
            {
                error = $"Error: Parameter '{parameter.Name}' cannot be null";
                return false;
            }
            return true;
        }

        try
        {
            converted = value.ValueKind == JsonValueKind.String
                ? ConvertFromString(value.GetString()!, parameter.ParameterType)
                : JsonSerializer.Deserialize(value, parameter.ParameterType, JsonOptions);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Error: Invalid value for parameter '{parameter.Name}' ({parameter.TypeName}): {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Converts a textual argument to the parameter type, which is how command
    /// line arguments arrive. A string aimed at a structured parameter is read
    /// as JSON.
    /// </summary>
    internal static object? ConvertFromString(string value, Type targetType)
    {
        if (Nullable.GetUnderlyingType(targetType) is { } underlying)
            return ConvertFromString(value, underlying);

        if (targetType == typeof(string))
            return value;

        var trimmed = value.Trim();
        if (trimmed.StartsWith('[') || trimmed.StartsWith('{'))
            return JsonSerializer.Deserialize(trimmed, targetType, JsonOptions);

        if (targetType == typeof(string[]))
            return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (targetType == typeof(int))
            return int.Parse(value, CultureInfo.InvariantCulture);
        if (targetType == typeof(bool))
            return bool.Parse(value.Trim());
        if (targetType.IsEnum)
            return Enum.Parse(targetType, value.Trim(), ignoreCase: true);

        return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
    }

    /// <summary>Lower case name with separators removed, so every spelling matches.</summary>
    internal static string NormalizeName(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (c is '-' or '_' or ' ')
                continue;
            builder.Append(char.ToLowerInvariant(c));
        }
        return builder.ToString();
    }

    internal static string ToKebabCase(string name)
    {
        var builder = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c) && i > 0)
                builder.Append('-');
            builder.Append(char.ToLowerInvariant(c));
        }
        return builder.ToString();
    }

    private static IReadOnlyList<ToolDescriptor> Discover(Assembly assembly)
    {
        var tools = new List<ToolDescriptor>();

        foreach (var type in assembly.GetTypes())
        {
            var isToolType = HasAttribute(type, typeof(McpServerToolTypeAttribute));
            var isPromptType = HasAttribute(type, typeof(McpServerPromptTypeAttribute));

            if (!isToolType && !isPromptType)
                continue;

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.IsGenericMethodDefinition || method.ContainsGenericParameters)
                    continue;

                var isTool = isToolType && HasAttribute(method, typeof(McpServerToolAttribute));
                var isPrompt = isPromptType && HasAttribute(method, typeof(McpServerPromptAttribute));

                if (!isTool && !isPrompt)
                    continue;

                tools.Add(new ToolDescriptor(method, isPrompt));
            }
        }

        return tools.OrderBy(t => t.Name, StringComparer.Ordinal).ToList();
    }

    private static bool HasAttribute(MemberInfo member, Type attributeType)
        => member.GetCustomAttributes(attributeType, false).Length > 0;
}
