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
using System;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("RefactorMCP.Tests")]

// Parse command line arguments
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
    .WithTools(RefactoringToolCatalog.ExposedToolTypes)
    .WithResourcesFromAssembly()
    .WithPromptsFromAssembly();

await builder.Build().RunAsync();

static async Task RunJsonMode(string[] args)
{
    if (args.Length < 3)
    {
        Console.WriteLine("Usage: --json <ToolName> '{\"param\":\"value\"}'");
        return;
    }

    ToolCallLogger.RestoreFromEnvironment();

    var toolName = args[1];
    var json = string.Join(" ", args.Skip(2));
    Dictionary<string, JsonElement>? paramDict;
    try
    {
        paramDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, new JsonSerializerOptions
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

    var method = RefactoringToolCatalog.GetExposedToolMethods()
        .FirstOrDefault(m => IsRequestedTool(m, toolName));

    // Keep JSON mode backward-compatible with the original fine-grained tool names,
    // while the MCP server advertises only the grouped surface above.
    method ??= RefactoringToolCatalog.GetLegacyToolMethods()
        .FirstOrDefault(m => IsRequestedTool(m, toolName));

    if (method == null)
    {
        Console.WriteLine($"Unknown tool: {toolName}. Use the ListTools tool to see available commands.");
        return;
    }

    var parameters = method.GetParameters();
    var invokeArgs = new object?[parameters.Length];
    var rawValues = new Dictionary<string, string?>();
    for (int i = 0; i < parameters.Length; i++)
    {
        var p = parameters[i];
        if (paramDict.TryGetValue(p.Name!, out var value))
        {
            rawValues[p.Name!] = value.ToString();
            if (value.ValueKind == JsonValueKind.String)
            {
                invokeArgs[i] = ConvertInput(value.GetString()!, p.ParameterType);
            }
            else
            {
                invokeArgs[i] = value.Deserialize(p.ParameterType, new JsonSerializerOptions());
            }
        }
        else if (p.HasDefaultValue)
        {
            rawValues[p.Name!] = null;
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
    finally
    {
        if (!string.Equals(method.Name, nameof(LoadSolutionTool.LoadSolution)))
            ToolCallLogger.Log(method.Name, rawValues);
    }
}

static bool IsRequestedTool(MethodInfo method, string toolName)
    => method.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase) ||
       RefactoringToolCatalog.ToKebabCase(method.Name).Equals(toolName, StringComparison.OrdinalIgnoreCase);

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
