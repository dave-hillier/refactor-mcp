using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// Runs one refactoring tool from the command line.
///
/// Options are matched to the tool's parameters by name (<c>--method-names</c>
/// for <c>methodNames</c>, <c>--file</c> for <c>filePath</c>) and any remaining
/// positional arguments fill the rest in declaration order. When the call names
/// a solution, it is sent to that solution's daemon, starting one if needed, so
/// the load cost is paid once rather than per call.
/// </summary>
internal static class ToolCommand
{
    /// <summary>Options the command line handles itself rather than passing on.</summary>
    private static readonly string[] CliOptions = { "no-daemon", "help" };

    public static async Task<int> RunJsonAsync(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine($"Usage: {Cli.ProgramName} --json <tool> '{{\"param\":\"value\"}}'");
            return 1;
        }

        var toolName = args[1];
        var useDaemon = !args.Contains("--no-daemon", StringComparer.OrdinalIgnoreCase);
        var json = string.Join(" ", args.Skip(2).Where(argument => !argument.Equals("--no-daemon", StringComparison.OrdinalIgnoreCase)));

        Dictionary<string, JsonElement>? arguments;
        try
        {
            arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Error parsing JSON: {ex.Message}");
            return 1;
        }

        if (arguments is null)
        {
            Console.Error.WriteLine("Error: Failed to parse parameters");
            return 1;
        }

        return await ExecuteAsync(toolName, arguments, useDaemon);
    }

    public static async Task<int> RunAsync(string[] args)
    {
        var command = ParsedCommand.Parse(args);
        var tool = ToolDispatcher.Default.Resolve(command.Verb);

        if (tool is null)
        {
            Console.Error.WriteLine($"Unknown tool: {command.Verb}. Run '{Cli.ProgramName} list-tools' to see what is available.");
            return 1;
        }

        if (command.HasOption("help"))
        {
            Console.WriteLine(Help(tool));
            return 0;
        }

        if (!TryBind(tool, command, out var arguments, out var error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine();
            Console.Error.WriteLine(Help(tool));
            return 1;
        }

        return await ExecuteAsync(tool.Name, arguments, useDaemon: !command.HasOption("no-daemon"));
    }

    /// <summary>Turns command line options and positionals into tool arguments.</summary>
    internal static bool TryBind(
        ToolDescriptor tool,
        ParsedCommand command,
        out Dictionary<string, JsonElement> arguments,
        out string error)
    {
        arguments = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        error = string.Empty;

        foreach (var option in command.Options)
        {
            if (CliOptions.Contains(option.Key, StringComparer.OrdinalIgnoreCase))
                continue;

            var parameter = tool.FindParameter(option.Key);
            if (parameter is null)
            {
                error = $"Error: {tool.Name} has no option '--{option.Key}'. " +
                        $"It takes: {string.Join(", ", tool.CallerParameters.Select(p => "--" + ToolDispatcher.ToKebabCase(p.Name)))}";
                return false;
            }

            if (!TryConvertOption(parameter, option.Value, out var value, out error))
                return false;

            arguments[parameter.Name] = value;
        }

        var bound = arguments;
        var unbound = tool.CallerParameters.Where(p => !bound.ContainsKey(p.Name)).ToList();
        if (command.Positionals.Count > unbound.Count)
        {
            error = $"Error: {tool.Name} takes {tool.CallerParameters.Count()} arguments " +
                    $"({string.Join(", ", tool.CallerParameters.Select(p => p.Name))}); got {command.Positionals.Count}.";
            return false;
        }

        for (var i = 0; i < command.Positionals.Count; i++)
        {
            var parameter = unbound[i];
            if (!TryConvertOption(parameter, new[] { command.Positionals[i] }, out var value, out error))
                return false;

            arguments[parameter.Name] = value;
        }

        return true;
    }

    private static bool TryConvertOption(
        ToolParameterDescriptor parameter,
        IReadOnlyList<string> values,
        out JsonElement value,
        out string error)
    {
        value = default;
        error = string.Empty;

        if (parameter.ParameterType == typeof(bool))
        {
            var text = values.Count == 0 ? "true" : values[^1];
            if (!bool.TryParse(text, out var flag))
            {
                error = $"Error: option '--{ToolDispatcher.ToKebabCase(parameter.Name)}' takes true or false, got '{text}'";
                return false;
            }

            value = JsonSerializer.SerializeToElement(flag);
            return true;
        }

        if (values.Count == 0)
        {
            error = $"Error: option '--{ToolDispatcher.ToKebabCase(parameter.Name)}' needs a value";
            return false;
        }

        // Repeated options, and comma separated values, both mean a list.
        var raw = values.Count == 1 ? values[0] : string.Join(",", values);
        value = ToolDispatcher.ToJsonElement(raw, parameter.ParameterType);
        return true;
    }

    private static async Task<int> ExecuteAsync(
        string toolName,
        Dictionary<string, JsonElement> arguments,
        bool useDaemon = true)
    {
        var tool = ToolDispatcher.Default.Resolve(toolName);
        if (tool is null)
        {
            var unknown = await ToolDispatcher.Default.InvokeAsync(toolName, arguments);
            Console.Error.WriteLine(unknown.Text);
            return 1;
        }

        if (useDaemon && SolutionPathOf(tool, arguments) is { } solutionPath && File.Exists(solutionPath))
        {
            var endpoint = DaemonEndpoint.For(solutionPath);

            if (DaemonClient.Default.EnsureRunning(endpoint))
            {
                var request = new DaemonRequest
                {
                    Tool = tool.Name,
                    Params = new Dictionary<string, JsonElement>(arguments, StringComparer.OrdinalIgnoreCase)
                };

                var response = DaemonClient.TrySend(endpoint, request);
                if (response is not null)
                    return Report(response);
            }
            else
            {
                Console.Error.WriteLine(
                    $"Warning: could not start a daemon for {solutionPath}; running the tool in this process.");
                Console.Error.WriteLine($"         Daemon output: {endpoint.LogPath}");
            }
        }

        return Report(await ToolDispatcher.Default.InvokeAsync(tool, arguments));
    }

    private static string? SolutionPathOf(ToolDescriptor tool, Dictionary<string, JsonElement> arguments)
    {
        var parameter = tool.FindParameter("solutionPath");
        if (parameter is null || !arguments.TryGetValue(parameter.Name, out var value))
            return null;

        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static int Report(DaemonResponse response)
    {
        if (response.Error is { Length: > 0 } error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        Console.WriteLine(response.Result ?? string.Empty);
        return 0;
    }

    private static int Report(ToolResult result)
    {
        if (result.IsError)
        {
            Console.Error.WriteLine(result.Text);
            return 1;
        }

        Console.WriteLine(result.Text);
        return 0;
    }

    public static string Help(ToolDescriptor tool)
    {
        var options = tool.CallerParameters.Select(parameter =>
        {
            var names = string.Join(", ", new[] { "--" + ToolDispatcher.ToKebabCase(parameter.Name) }
                .Concat(parameter.Name.EndsWith("Path", StringComparison.OrdinalIgnoreCase)
                    ? new[] { "--" + ToolDispatcher.ToKebabCase(parameter.Name[..^4]) }
                    : Array.Empty<string>()));

            var requirement = parameter.IsRequired ? " (required)" : $" (default: {Describe(parameter.DefaultValue)})";
            return $"  {names,-40} {parameter.TypeName,-18}{requirement}\n      {parameter.Description}";
        });

        return $"""
            {tool.Name}{(tool.IsPrompt ? " (prompt)" : string.Empty)} - {tool.Description}

            Usage: {Cli.ProgramName} {tool.Name} [options] [arguments]

            Options:
            {string.Join("\n", options)}
            """;
    }

    private static string Describe(object? value)
        => value switch
        {
            null => "null",
            string text => $"\"{text}\"",
            bool flag => flag ? "true" : "false",
            _ => value.ToString() ?? string.Empty
        };
}
