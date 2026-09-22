using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

/// <summary>
/// Where a daemon for one solution listens. The socket and the discovery file
/// live in a per user temporary directory, named after a hash of the solution
/// path so a client can find the daemon for a solution without being told.
/// </summary>
internal sealed class DaemonEndpoint
{
    public DaemonEndpoint(string solutionPath, string socketPath, string discoveryPath, string logPath)
    {
        SolutionPath = solutionPath;
        SocketPath = socketPath;
        DiscoveryPath = discoveryPath;
        LogPath = logPath;
    }

    /// <summary>Absolute path of the solution this daemon serves.</summary>
    public string SolutionPath { get; }

    /// <summary>Unix domain socket the daemon listens on.</summary>
    public string SocketPath { get; }

    /// <summary>Discovery file written once the daemon is ready to serve.</summary>
    public string DiscoveryPath { get; }

    /// <summary>Where a detached daemon's output is redirected.</summary>
    public string LogPath { get; }

    /// <summary>
    /// Where sockets and discovery files live. The user's name keeps two users
    /// on one machine apart, which matters on systems with a shared /tmp.
    /// </summary>
    public static string Directory
    {
        get
        {
            var runtimeDirectory = OperatingSystem.IsLinux()
                ? Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR")
                : null;

            var root = string.IsNullOrEmpty(runtimeDirectory) ? Path.GetTempPath() : runtimeDirectory;
            return Path.Combine(root, $"refactor-mcp-{Environment.UserName}");
        }
    }

    public static DaemonEndpoint For(string solutionPath)
    {
        var normalized = Path.GetFullPath(solutionPath);
        var hash = Hash(normalized);
        var directory = Directory;

        return new DaemonEndpoint(
            normalized,
            Path.Combine(directory, hash + ".sock"),
            Path.Combine(directory, hash + ".json"),
            Path.Combine(directory, hash + ".log"));
    }

    /// <summary>File name used for every socket and discovery file of a solution.</summary>
    private static string Hash(string solutionPath)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(solutionPath));
        return Convert.ToHexString(digest)[..16].ToLowerInvariant();
    }
}

/// <summary>Contents of a discovery file, used to find and validate a daemon.</summary>
internal sealed class DaemonDiscovery
{
    public int Pid { get; set; }

    public string SocketPath { get; set; } = string.Empty;

    public string SolutionPath { get; set; } = string.Empty;

    public DateTime StartedAtUtc { get; set; }

    [JsonIgnore]
    public bool IsRunning
    {
        get
        {
            try
            {
                using var process = System.Diagnostics.Process.GetProcessById(Pid);
                return !process.HasExited;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
