using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using ModelContextProtocol.Server;

internal static class ToolCallLogger
{
    private const string LogFileEnvVar = "REFACTOR_MCP_LOG_FILE";
    private static string _logFile = "tool-call-log.jsonl";

    public static string DefaultLogFile => _logFile;

    public static void SetLogDirectory(string directory)
    {
        Directory.CreateDirectory(directory);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        _logFile = Path.Combine(directory, $"tool-call-log-{timestamp}.jsonl");
        Environment.SetEnvironmentVariable(LogFileEnvVar, _logFile);
    }

    public static void RestoreFromEnvironment()
    {
        var file = Environment.GetEnvironmentVariable(LogFileEnvVar);
        if (!string.IsNullOrEmpty(file))
            _logFile = file;
    }

    public static void Log(string toolName, Dictionary<string, string?> parameters, string? logFile = null)
    {
        var file = logFile ?? DefaultLogFile;
        var dir = Path.GetDirectoryName(file);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var record = new ToolCallRecord
        {
            Tool = toolName,
            Parameters = parameters,
            Timestamp = DateTime.UtcNow
        };
        var json = JsonSerializer.Serialize(record);
        File.AppendAllText(file, json + Environment.NewLine);
    }

    public static async Task Playback(string logFilePath)
    {
        if (!File.Exists(logFilePath))
        {
            Console.WriteLine($"Log file '{logFilePath}' not found");
            return;
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        foreach (var line in await File.ReadAllLinesAsync(logFilePath))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            ToolCallRecord? record = null;
            try
            {
                record = JsonSerializer.Deserialize<ToolCallRecord>(line, options);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Invalid log entry: {ex.Message}");
            }
            if (record != null)
                await InvokeTool(record.Tool, record.Parameters);
        }
    }

    private static async Task InvokeTool(string toolName, Dictionary<string, string?> parameters)
    {
        var method = GetToolMethod(toolName);
        if (method == null)
        {
            Console.WriteLine($"Unknown tool in log: {toolName}");
            return;
        }

        var paramInfos = method.GetParameters();
        var invokeArgs = new object?[paramInfos.Length];
        for (int i = 0; i < paramInfos.Length; i++)
        {
            var p = paramInfos[i];
            parameters.TryGetValue(p.Name!, out var raw);
            if (string.IsNullOrEmpty(raw))
            {
                if (p.HasDefaultValue)
                    invokeArgs[i] = p.DefaultValue;
                else
                {
                    Console.WriteLine($"Missing parameter {p.Name} for {toolName}");
                    return;
                }
            }
            else
            {
                invokeArgs[i] = ConvertInput(raw!, p.ParameterType);
            }
        }

        var result = method.Invoke(null, invokeArgs);
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
    }

    private static MethodInfo? GetToolMethod(string toolName)
    {
        return typeof(LoadSolutionTool).Assembly
            .GetTypes()
            .Where(t => t.GetCustomAttributes(typeof(McpServerToolTypeAttribute), false).Length > 0)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .FirstOrDefault(m => m.GetCustomAttributes(typeof(McpServerToolAttribute), false).Length > 0 &&
                                 m.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));
    }

    private static object? ConvertInput(string value, Type targetType)
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

    private class ToolCallRecord
    {
        public string Tool { get; set; } = string.Empty;
        public Dictionary<string, string?> Parameters { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }
}

