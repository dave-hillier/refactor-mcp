using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.CommandLine;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("RefactorMCP.Tests")]

// Parse command line arguments
if (args.Length > 0 && args[0] == "--cli")
{
    await RunCliMode(args);
    return;
}
if (args.Length > 0 && args[0] == "--json")
{
    await RunJsonMode(args);
    return;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(consoleLogOptions =>
{
    // Configure all logs to go to stderr
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly()
    .WithResourcesFromAssembly()
    .WithPromptsFromAssembly();

await builder.Build().RunAsync();

static async Task RunCliMode(string[] args)
{
    if (args.Length < 2)
    {
        ShowCliHelp();
        return;
    }

    var root = BuildCliRoot();
    await root.InvokeAsync(args.Skip(1).ToArray());
}
static RootCommand BuildCliRoot()
{
    var root = new RootCommand("RefactorMCP CLI Mode");

    var toolMethods = typeof(LoadSolutionTool).Assembly
        .GetTypes()
        .Where(t => t.GetCustomAttributes(typeof(McpServerToolTypeAttribute), false).Length > 0)
        .SelectMany(t => t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
        .Where(m => m.GetCustomAttributes(typeof(McpServerToolAttribute), false).Length > 0)
        .ToArray();

    foreach (var method in toolMethods)
    {
        var commandName = ToKebabCase(method.Name);
        var description = method.GetCustomAttribute<DescriptionAttribute>()?.Description;
        var command = new Command(commandName, description ?? string.Empty);

        var parameterSymbols = new List<(ParameterInfo param, Argument<string>? arg, Option<string?>? opt)>();
        foreach (var p in method.GetParameters())
        {
            var desc = p.GetCustomAttribute<DescriptionAttribute>()?.Description;
            var kebab = ToKebabCase(p.Name!);
            if (p.HasDefaultValue)
            {
                var opt = new Option<string?>("--" + kebab, desc);
                command.AddOption(opt);
                parameterSymbols.Add((p, null, opt));
            }
            else
            {
                var arg = new Argument<string>(kebab, desc);
                command.AddArgument(arg);
                parameterSymbols.Add((p, arg, null));
            }
        }

        command.SetHandler(async ctx =>
        {
            var values = new object?[parameterSymbols.Count];
            for (int i = 0; i < parameterSymbols.Count; i++)
            {
                var (param, arg, opt) = parameterSymbols[i];
                string? input = arg != null
                    ? ctx.ParseResult.GetValueForArgument(arg)
                    : ctx.ParseResult.GetValueForOption(opt!);

                if (string.IsNullOrEmpty(input))
                {
                    values[i] = param.HasDefaultValue ? param.DefaultValue : null;
                }
                else
                {
                    values[i] = ConvertInput(input, param.ParameterType);
                }
            }

            var result = method.Invoke(null, values);
            if (result is Task<string> taskStr)
                Console.WriteLine(await taskStr);
            else if (result is Task task)
            {
                await task;
                Console.WriteLine("Done");
            }
            else if (result != null)
            {
                Console.WriteLine(result.ToString());
            }
        });

        root.AddCommand(command);
    }

    var listTools = new Command("list-tools", "List all available refactoring tools");
    listTools.SetHandler(() => Console.WriteLine(ListAvailableTools()));
    root.AddCommand(listTools);

    return root;
}

static object? ConvertInput(string value, Type targetType)
{
    if (targetType == typeof(string))
        return value;
    if (targetType == typeof(string[]))
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries);
    if (targetType == typeof(int))
        return int.Parse(value);
    if (targetType == typeof(bool))
        return bool.Parse(value);
    return Convert.ChangeType(value, targetType);
}


static void ShowCliHelp()
{
    BuildCliRoot().Invoke("--help");
}

static async Task RunJsonMode(string[] args)
{
    if (args.Length < 3)
    {
        Console.WriteLine("Usage: --json <ToolName> '{\"param\":\"value\"}'");
        return;
    }

    var toolName = args[1];
    var json = string.Join(" ", args.Skip(2));
    Dictionary<string, string>? paramDict;
    try
    {
        paramDict = JsonSerializer.Deserialize<Dictionary<string, string>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        if (paramDict == null)
        {
            Console.WriteLine("Error: Failed to parse parameters");
            return;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error parsing JSON: {ex.Message}");
        return;
    }

    var method = System.Reflection.Assembly.GetExecutingAssembly()
        .GetTypes()
        .Where(t => t.GetCustomAttributes(typeof(McpServerToolTypeAttribute), false).Length > 0)
        .SelectMany(t => t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
        .FirstOrDefault(m => m.GetCustomAttributes(typeof(McpServerToolAttribute), false).Length > 0 &&
                             m.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));

    if (method == null)
    {
        Console.WriteLine($"Unknown tool: {toolName}. Use --cli list-tools to see available commands.");
        return;
    }

    var parameters = method.GetParameters();
    var invokeArgs = new object?[parameters.Length];
    for (int i = 0; i < parameters.Length; i++)
    {
        var p = parameters[i];
        if (paramDict.TryGetValue(p.Name!, out var value))
        {
            invokeArgs[i] = value;
        }
        else if (p.HasDefaultValue)
        {
            invokeArgs[i] = p.DefaultValue;
        }
        else
        {
            Console.WriteLine($"Error: Missing required parameter '{p.Name}'");
            return;
        }
    }

    try
    {
        var result = method.Invoke(null, invokeArgs);
        if (result is Task<string> taskStr)
        {
            Console.WriteLine(await taskStr);
        }
        else if (result is Task task)
        {
            await task;
            Console.WriteLine("Done");
        }
        else if (result != null)
        {
            Console.WriteLine(result.ToString());
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error executing tool: {ex.Message}");
    }
}

static string ListAvailableTools()
{
    var toolNames = System.Reflection.Assembly.GetExecutingAssembly()
        .GetTypes()
        .Where(t => t.GetCustomAttributes(typeof(McpServerToolTypeAttribute), false).Length > 0)
        .SelectMany(t => t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
        .Where(m => m.GetCustomAttributes(typeof(McpServerToolAttribute), false).Length > 0)
        .Select(m => ToKebabCase(m.Name))
        .OrderBy(n => n)
        .ToArray();

    return "Available refactoring tools:\n" + string.Join("\n", toolNames);

}


static string ToKebabCase(string name)
{
    var sb = new StringBuilder();
    for (int i = 0; i < name.Length; i++)
    {
        var c = name[i];
        if (char.IsUpper(c) && i > 0)
            sb.Append('-');
        sb.Append(char.ToLowerInvariant(c));
    }
    return sb.ToString();
}
