using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Resident process that owns one loaded solution and answers tool calls over a
/// unix domain socket, so a command line client does not pay the MSBuild load
/// cost on every invocation.
///
/// Requests are handled one at a time: tools write files and replace the cached
/// solution, so concurrency is deliberately absent. The daemon shuts down once
/// it has been idle for its timeout.
/// </summary>
internal sealed class SolutionDaemon : IDisposable
{
    // Request and response member names are pinned by attributes; the camel
    // case policy only shapes the discovery file.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // Encoding.UTF8 writes a byte order mark, which would sit in front of the
    // JSON a client reads.
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>How long a socket file is assumed to belong to a daemon starting up.</summary>
    private static readonly TimeSpan StartupGrace = TimeSpan.FromSeconds(5);

    private readonly DaemonEndpoint _endpoint;
    private readonly TimeSpan _idleTimeout;
    private Socket? _listener;
    private int _requestsServed;

    public SolutionDaemon(DaemonEndpoint endpoint, TimeSpan idleTimeout)
    {
        _endpoint = endpoint;
        _idleTimeout = idleTimeout;
    }

    /// <summary>Loads the solution, then serves requests until stopped or idle.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_endpoint.SolutionPath))
        {
            Console.Error.WriteLine($"Error: Solution file not found at {_endpoint.SolutionPath}");
            return 1;
        }

        RefactoringHelpers.EnsureMsBuildRegistered();

        try
        {
            Bind();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: could not listen on {_endpoint.SocketPath}: {ex.Message}");
            return 1;
        }

        try
        {
            var session = SessionRegistry.GetOrCreate(_endpoint.SolutionPath);
            await session.GetOrLoadAsync(progress: null, cancellationToken);
            Console.Error.WriteLine($"Serving {_endpoint.SolutionPath} on {_endpoint.SocketPath}");

            WriteDiscovery();
            await AcceptUntilIdleAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutdown was requested; fall through to cleanup.
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        finally
        {
            Cleanup();
        }

        return 0;
    }

    private void Bind()
    {
        Directory.CreateDirectory(DaemonEndpoint.Directory);
        RemoveStaleSocket();

        var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(_endpoint.SocketPath));
        listener.Listen(backlog: 8);
        _listener = listener;

        if (!OperatingSystem.IsWindows())
        {
            // Only the user who started the daemon may talk to it.
            File.SetUnixFileMode(_endpoint.SocketPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    /// <summary>
    /// Clears a socket file left behind by a daemon that died without cleaning
    /// up. A socket that still answers belongs to a live daemon, which is an
    /// error rather than something to steal.
    /// </summary>
    private void RemoveStaleSocket()
    {
        if (IsServing(_endpoint.SocketPath))
        {
            throw new InvalidOperationException(
                $"a daemon is already serving {_endpoint.SolutionPath}. Use stop to shut it down first.");
        }

        // Two clients starting a daemon at once must not delete each other's
        // socket, so a socket file that has only just appeared is left alone.
        if (IsFreshSocketFile())
        {
            throw new InvalidOperationException(
                $"another daemon is starting for {_endpoint.SolutionPath}.");
        }

        // Deleting a socket that is not there is a no-op, so no existence check.
        File.Delete(_endpoint.SocketPath);
    }

    private bool IsFreshSocketFile()
    {
        try
        {
            return File.Exists(_endpoint.SocketPath)
                && DateTime.UtcNow - File.GetLastWriteTimeUtc(_endpoint.SocketPath) < StartupGrace;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static bool IsServing(string socketPath)
    {
        if (!File.Exists(socketPath))
            return false;

        using var probe = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            probe.Connect(new UnixDomainSocketEndPoint(socketPath));
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private void WriteDiscovery()
    {
        var discovery = new DaemonDiscovery
        {
            Pid = Environment.ProcessId,
            SocketPath = _endpoint.SocketPath,
            SolutionPath = _endpoint.SolutionPath,
            StartedAtUtc = DateTime.UtcNow
        };

        File.WriteAllText(_endpoint.DiscoveryPath, JsonSerializer.Serialize(discovery, JsonOptions));
    }

    private async Task AcceptUntilIdleAsync(CancellationToken cancellationToken)
    {
        var listener = _listener!;
        var idleTimeout = _idleTimeout;

        while (true)
        {
            Socket connection;

            using (var idle = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                if (idleTimeout > TimeSpan.Zero)
                    idle.CancelAfter(idleTimeout);

                try
                {
                    connection = await listener.AcceptAsync(idle.Token);
                }
                catch (OperationCanceledException)
                {
                    // Idle expiry, or a shutdown request from the client.
                    break;
                }
                catch (SocketException)
                {
                    break;
                }
            }

            // Handling is not subject to the idle timer: a slow refactoring
            // must be allowed to finish.
            bool stopRequested;
            using (connection)
            {
                stopRequested = await HandleAsync(connection, cancellationToken);
            }

            if (stopRequested)
                break;
        }
    }

    /// <summary>Handles one request. Returns true when the daemon should stop.</summary>
    private async Task<bool> HandleAsync(Socket connection, CancellationToken cancellationToken)
    {
        using var stream = new NetworkStream(connection, ownsSocket: false);
        using var reader = new StreamReader(stream, Utf8);
        using var writer = new StreamWriter(stream, Utf8) { AutoFlush = true };

        try
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line))
            {
                // A liveness probe that only connected, or a client that gave up.
                return false;
            }

            DaemonRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<DaemonRequest>(line, JsonOptions);
            }
            catch (JsonException ex)
            {
                await WriteAsync(writer, new DaemonResponse { Error = $"Error: invalid request: {ex.Message}" });
                return false;
            }

            if (string.IsNullOrWhiteSpace(request?.Tool))
            {
                await WriteAsync(writer, new DaemonResponse { Error = "Error: request has no tool name" });
                return false;
            }

            if (DaemonCommands.IsControl(request.Tool))
            {
                var stopping = request.Tool == DaemonCommands.Stop;
                var reply = request.Tool switch
                {
                    DaemonCommands.Stop => "Stopping",
                    DaemonCommands.Status => DescribeStatus(),
                    _ => "pong"
                };

                await WriteAsync(writer, new DaemonResponse { Result = reply });
                return stopping;
            }

            var arguments = request.Params ?? new Dictionary<string, JsonElement>();
            var result = await ToolDispatcher.Default.InvokeAsync(request.Tool, arguments, cancellationToken);
            _requestsServed++;
            await WriteAsync(writer, DaemonResponse.FromResult(result));
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException)
        {
            // The client hung up; nothing useful to do about it.
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }

        return false;
    }

    /// <summary>
    /// What this daemon is holding. Clients use it to tell a warm session from
    /// one that has just reloaded the solution.
    /// </summary>
    private string DescribeStatus()
    {
        var session = SessionRegistry.Find(_endpoint.SolutionPath);

        return $"solution={_endpoint.SolutionPath} " +
               $"loaded={session?.IsLoaded.ToString().ToLowerInvariant() ?? "false"} " +
               $"loadedAt={session?.LoadedAtUtc?.ToString("u") ?? "-"} " +
               $"requests={_requestsServed}";
    }

    private static async Task WriteAsync(StreamWriter writer, DaemonResponse response)
    {
        await writer.WriteLineAsync(JsonSerializer.Serialize(response, JsonOptions));
    }

    private void Cleanup()
    {
        Delete(_endpoint.DiscoveryPath);
        Delete(_endpoint.SocketPath);
        SessionRegistry.Unload(_endpoint.SolutionPath);
    }

    private static void Delete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"Error: could not remove {path}: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _listener?.Dispose();
        _listener = null;

        if (_endpoint.SocketPath is not null)
            Delete(_endpoint.SocketPath);
    }
}
