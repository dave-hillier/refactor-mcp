using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// The client half of the daemon: find the daemon for a solution, start one if
/// there is none, and send a single request per connection.
/// </summary>
internal sealed class DaemonClient
{
    public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan DefaultReadyTimeout = TimeSpan.FromMinutes(2);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly TimeSpan PingTimeout = TimeSpan.FromSeconds(5);

    private readonly string _executable;
    private readonly IReadOnlyList<string> _leadingArguments;

    public DaemonClient(string executable, IReadOnlyList<string> leadingArguments)
    {
        _executable = executable;
        _leadingArguments = leadingArguments;
    }

    /// <summary>Client that starts daemons by running this program again.</summary>
    public static DaemonClient Default { get; } = CreateDefault();

    private static DaemonClient CreateDefault()
    {
        var (executable, leadingArguments) = ResolveSelfCommand();
        return new DaemonClient(executable, leadingArguments);
    }

    /// <summary>
    /// The command that runs this program again: the app host when there is
    /// one, otherwise the dotnet host plus the assembly.
    /// </summary>
    public static (string Executable, IReadOnlyList<string> LeadingArguments) ResolveSelfCommand()
    {
        var processPath = Environment.ProcessPath;
        var entryAssembly = Assembly.GetEntryAssembly()?.Location;

        if (processPath is null)
        {
            return entryAssembly is null
                ? throw new InvalidOperationException(
                    "Cannot tell how to start a daemon: neither the program path nor the entry assembly is known.")
                : ("dotnet", new[] { entryAssembly });
        }

        if (string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase)
            && entryAssembly is not null)
        {
            return (processPath, new[] { entryAssembly });
        }

        return (processPath, Array.Empty<string>());
    }

    /// <summary>
    /// Sends one request. Returns null when no daemon is listening and throws
    /// when a daemon accepted the request but the exchange failed.
    /// </summary>
    public static DaemonResponse? TrySend(DaemonEndpoint endpoint, DaemonRequest request, TimeSpan? timeout = null)
    {
        var milliseconds = (int)(timeout ?? DefaultRequestTimeout).TotalMilliseconds;

        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified)
        {
            ReceiveTimeout = milliseconds,
            SendTimeout = milliseconds
        };

        try
        {
            socket.Connect(new UnixDomainSocketEndPoint(endpoint.SocketPath));
        }
        catch (SocketException)
        {
            return null;
        }

        using var stream = new NetworkStream(socket, ownsSocket: false);
        using var writer = new StreamWriter(stream, Utf8) { AutoFlush = true };
        using var reader = new StreamReader(stream, Utf8);

        writer.WriteLine(JsonSerializer.Serialize(request, JsonOptions));
        var line = reader.ReadLine();

        if (string.IsNullOrWhiteSpace(line))
            throw new IOException($"The daemon for {endpoint.SolutionPath} closed the connection without replying.");

        return JsonSerializer.Deserialize<DaemonResponse>(line, JsonOptions)
            ?? throw new IOException($"The daemon for {endpoint.SolutionPath} sent an unreadable reply.");
    }

    public static bool IsRunning(DaemonEndpoint endpoint)
    {
        try
        {
            return TrySend(endpoint, new DaemonRequest { Tool = DaemonCommands.Ping }, PingTimeout)?.Result == "pong";
        }
        catch (Exception ex) when (ex is IOException or JsonException or SocketException)
        {
            return false;
        }
    }

    /// <summary>Starts a daemon and waits until it answers, or gives up.</summary>
    public bool EnsureRunning(DaemonEndpoint endpoint, TimeSpan? idleTimeout = null, TimeSpan? readyTimeout = null)
    {
        if (IsRunning(endpoint))
            return true;

        var process = Spawn(endpoint, idleTimeout);
        if (process is null)
            return false;

        var deadline = DateTime.UtcNow + (readyTimeout ?? DefaultReadyTimeout);
        while (DateTime.UtcNow < deadline)
        {
            if (process.HasExited)
                return false;

            if (IsRunning(endpoint))
                return true;

            System.Threading.Thread.Sleep(50);
        }

        return false;
    }

    /// <summary>
    /// Starts the daemon detached, so it outlives this process. Its output is
    /// kept in the daemon's log file, which is what a failed start reports.
    /// </summary>
    public Process? Spawn(DaemonEndpoint endpoint, TimeSpan? idleTimeout)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in _leadingArguments)
            startInfo.ArgumentList.Add(argument);

        startInfo.ArgumentList.Add("serve");
        startInfo.ArgumentList.Add("--solution");
        startInfo.ArgumentList.Add(endpoint.SolutionPath);

        if (idleTimeout is { } idle && idle > TimeSpan.Zero)
        {
            startInfo.ArgumentList.Add("--idle-timeout");
            startInfo.ArgumentList.Add($"{idle.TotalSeconds:0}s");
        }

        try
        {
            Directory.CreateDirectory(DaemonEndpoint.Directory);
            File.WriteAllText(endpoint.LogPath, string.Empty);
        }
        catch (IOException)
        {
            // A missing log file is not a reason to refuse to start.
        }

        var process = Process.Start(startInfo);
        if (process is null)
            return null;

        Pump(process.StandardOutput, endpoint.LogPath);
        Pump(process.StandardError, endpoint.LogPath);
        return process;
    }

    /// <summary>Stops the daemon for a solution. Returns false when none ran.</summary>
    public static bool Stop(DaemonEndpoint endpoint)
    {
        try
        {
            return TrySend(endpoint, new DaemonRequest { Tool = DaemonCommands.Stop }, TimeSpan.FromSeconds(30)) is not null;
        }
        catch (Exception ex) when (ex is IOException or SocketException or JsonException)
        {
            // A daemon that cannot answer is treated as gone.
            RemoveStaleFiles(endpoint);
            return false;
        }
    }

    /// <summary>Daemons that are alive right now, with their discovery data.</summary>
    public static IReadOnlyList<(DaemonEndpoint Endpoint, DaemonDiscovery Discovery)> Running()
    {
        var running = new List<(DaemonEndpoint, DaemonDiscovery)>();
        var directory = DaemonEndpoint.Directory;

        if (!Directory.Exists(directory))
            return running;

        foreach (var file in Directory.GetFiles(directory, "*.json"))
        {
            DaemonDiscovery? discovery = null;

            try
            {
                discovery = JsonSerializer.Deserialize<DaemonDiscovery>(File.ReadAllText(file), JsonOptions);
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
            }

            if (discovery is null || string.IsNullOrWhiteSpace(discovery.SolutionPath))
            {
                Delete(file);
                continue;
            }

            var endpoint = DaemonEndpoint.For(discovery.SolutionPath);
            if (!IsRunning(endpoint))
            {
                RemoveStaleFiles(endpoint);
                continue;
            }

            running.Add((endpoint, discovery));
        }

        return running;
    }

    /// <summary>Removes the files a daemon that is no longer running left behind.</summary>
    public static void RemoveStaleFiles(DaemonEndpoint endpoint)
    {
        Delete(endpoint.DiscoveryPath);
        Delete(endpoint.SocketPath);
    }

    private static void Delete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
    }

    private void Pump(StreamReader reader, string logPath)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                string? line;
                while ((line = await reader.ReadLineAsync()) is not null)
                    File.AppendAllText(logPath, line + Environment.NewLine);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                // The daemon exited; nothing left to record.
            }
        });
    }
}
