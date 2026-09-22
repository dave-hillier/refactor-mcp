using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

/// <summary>
/// Records the tool calls made in a session, to debug what a client asked for.
///
/// Logging is opt in. Set <c>REFACTOR_MCP_LOG</c> to a file path, or to
/// <c>1</c> / <c>true</c> to write into the loaded solution's
/// <c>.refactor-mcp</c> directory. With it unset nothing is written, so a one
/// shot CLI call leaves no trace behind.
/// </summary>
internal static class ToolCallLogger
{
    private const string LogEnvVar = "REFACTOR_MCP_LOG";

    // One log per process: a daemon writes one file for its session, a CLI
    // process writes one file per invocation, and the process id keeps
    // concurrent invocations apart.
    private static readonly string SessionLogFileName =
        $"tool-call-log-{DateTime.UtcNow:yyyyMMddHHmmss}-{Environment.ProcessId}.jsonl";

    public static bool IsEnabled => ResolveLogFile() is not null;

    public static void Log(string toolName, IReadOnlyDictionary<string, string?> parameters)
    {
        var file = ResolveLogFile();
        if (file is null)
            return;

        try
        {
            var directory = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var record = new ToolCallRecord
            {
                Tool = toolName,
                Parameters = new Dictionary<string, string?>(parameters),
                Timestamp = DateTime.UtcNow
            };

            File.AppendAllText(file, JsonSerializer.Serialize(record) + Environment.NewLine);
        }
        catch (Exception ex)
        {
            // Logging must never fail the tool call it is describing.
            Console.Error.WriteLine($"Error: could not write tool call log: {ex.Message}");
        }
    }

    /// <summary>The file calls are recorded in, or null when logging is off.</summary>
    public static string? ResolveLogFile()
    {
        var setting = Environment.GetEnvironmentVariable(LogEnvVar);
        if (string.IsNullOrWhiteSpace(setting) || setting is "0" or "false")
            return null;

        if (setting is "1" or "true")
        {
            var sessionDirectory = SessionRegistry.Current?.SolutionDirectory;
            return sessionDirectory is null
                ? null
                : Path.Combine(sessionDirectory, ".refactor-mcp", SessionLogFileName);
        }

        return setting;
    }

    private class ToolCallRecord
    {
        public string Tool { get; set; } = string.Empty;
        public Dictionary<string, string?> Parameters { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }
}
