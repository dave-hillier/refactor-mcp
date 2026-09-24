using System;
using System.IO;

/// <summary>
/// Where RefactorMCP keeps what it writes about a solution: the metrics cache
/// and, when asked for, the tool call log. It lives under the user's home
/// directory rather than beside the solution, so working on someone else's
/// repository leaves nothing behind in it.
///
/// The root is <c>~/.refactor-mcp</c>, or <c>REFACTOR_MCP_HOME</c> when set.
/// Each solution gets its own folder, named after the solution with a hash of
/// its full path so two checkouts of the same solution stay apart.
/// </summary>
internal static class StateDirectory
{
    private const string HomeEnvVar = "REFACTOR_MCP_HOME";

    public static string Root
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable(HomeEnvVar);
            if (!string.IsNullOrWhiteSpace(configured))
                return Path.GetFullPath(configured);

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".refactor-mcp");
        }
    }

    /// <summary>The folder for one solution's state.</summary>
    public static string For(string solutionPath)
    {
        var normalized = Path.GetFullPath(solutionPath);
        var name = Path.GetFileNameWithoutExtension(normalized);
        return Path.Combine(Root, $"{name}-{DaemonEndpoint.Hash(normalized)}");
    }

    /// <summary>The metrics cache folder for a solution.</summary>
    public static string Metrics(string solutionPath) => Path.Combine(For(solutionPath), "metrics");
}
