using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// One request per line: <c>{"tool":"extract-method","params":{...}}</c>.
/// The shape mirrors the <c>--json</c> command line mode so a call can be
/// moved between the two without translation.
/// </summary>
internal sealed class DaemonRequest
{
    [JsonPropertyName("tool")]
    public string? Tool { get; set; }

    [JsonPropertyName("params")]
    public Dictionary<string, JsonElement>? Params { get; set; }
}

/// <summary>
/// One response per line, carrying either <c>result</c> or <c>error</c>. Error
/// text is the same text the command line prints, so consumers keep working.
/// </summary>
internal sealed class DaemonResponse
{
    [JsonPropertyName("result")]
    public string? Result { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    public static DaemonResponse FromResult(ToolResult result)
        => result.IsError ? new DaemonResponse { Error = result.Text } : new DaemonResponse { Result = result.Text };
}

internal static class DaemonCommands
{
    /// <summary>Replies and then shuts the daemon down.</summary>
    public const string Stop = "stop";

    /// <summary>Replies without touching the session, for liveness checks.</summary>
    public const string Ping = "ping";

    /// <summary>Replies with what the daemon is holding and how busy it has been.</summary>
    public const string Status = "status";

    public static bool IsControl(string? tool)
        => tool is Stop or Ping or Status;
}
