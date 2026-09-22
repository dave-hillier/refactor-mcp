using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Xunit;

namespace RefactorMCP.Tests.Infrastructure;

/// <summary>
/// Starts a real daemon process and talks to it over its socket. These are the
/// slowest tests in the suite because a daemon loads the solution for real.
/// </summary>
public class DaemonIntegrationTests : IDisposable
{
    private static readonly string SolutionPath = TestUtilities.GetSolutionPath();
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromMinutes(2);

    private readonly DaemonEndpoint _endpoint = DaemonEndpoint.For(SolutionPath);

    /// <summary>
    /// Starts daemons the way the command line does: by running this program
    /// again. The test assembly holds the application's dll, so the dotnet host
    /// plus that dll is the equivalent of the application's own path.
    /// </summary>
    private readonly DaemonClient _client = new("dotnet", new[] { typeof(ToolDispatcher).Assembly.Location });

    public DaemonIntegrationTests()
    {
        DaemonClient.Stop(_endpoint);
        DaemonClient.RemoveStaleFiles(_endpoint);
    }

    public void Dispose()
    {
        DaemonClient.Stop(_endpoint);
        DaemonClient.RemoveStaleFiles(_endpoint);
    }

    [Fact]
    public void ClientStartsADaemonAndTheSecondRequestReusesTheLoadedSolution()
    {
        Assert.False(DaemonClient.IsRunning(_endpoint), "no daemon should be running to begin with");

        // This is the auto-spawn path: nothing is listening, so the client starts
        // a daemon and waits for it to load the solution.
        Assert.True(
            _client.EnsureRunning(_endpoint, idleTimeout: TimeSpan.FromMinutes(2), readyTimeout: ReadyTimeout),
            "the client could not start a daemon");

        var status = Status();
        Assert.Contains("loaded=true", status);
        Assert.Contains("requests=0", status);

        var first = Send("list-class-lengths", new Dictionary<string, string?> { ["solutionPath"] = SolutionPath });
        Assert.Null(first.Error);
        Assert.Contains("Class lengths", first.Result);

        var warm = Status();
        Assert.Contains("requests=1", warm);

        // The session is the same one: the solution was not loaded again.
        Assert.Equal(LoadStamp(status), LoadStamp(warm));

        var second = Send("list-class-lengths", new Dictionary<string, string?> { ["solutionPath"] = SolutionPath });
        Assert.Null(second.Error);
        Assert.Equal(first.Result, second.Result);
        Assert.Contains("requests=2", Status());
    }

    [Fact]
    public void ErrorsTravelBackToTheClientUnchanged()
    {
        Assert.True(_client.EnsureRunning(_endpoint, readyTimeout: ReadyTimeout), "the client could not start a daemon");

        var result = Send("no-such-tool", new Dictionary<string, string?>());

        Assert.Contains("Unknown tool: no-such-tool", result.Error);
        Assert.Null(result.Result);
    }

    [Fact]
    public void StopShutsTheDaemonDownAndCleansUpItsFiles()
    {
        Assert.True(_client.EnsureRunning(_endpoint, readyTimeout: ReadyTimeout), "the client could not start a daemon");
        Assert.True(File.Exists(_endpoint.SocketPath));
        Assert.True(File.Exists(_endpoint.DiscoveryPath));

        Assert.True(DaemonClient.Stop(_endpoint));

        Assert.False(DaemonClient.IsRunning(_endpoint));
        Assert.False(File.Exists(_endpoint.DiscoveryPath), "the discovery file should be removed on shutdown");
        Assert.False(File.Exists(_endpoint.SocketPath), "the socket file should be removed on shutdown");
    }

    [Fact]
    public void IdleDaemonShutsItselfDown()
    {
        var daemon = _client.Spawn(_endpoint, idleTimeout: TimeSpan.FromSeconds(2));
        Assert.NotNull(daemon);

        // The discovery file appears once the solution is loaded, and watching
        // for files rather than pinging matters: a request would reset the very
        // idle timer this test is about.
        Assert.True(
            WaitUntil(() => File.Exists(_endpoint.DiscoveryPath), TimeSpan.FromSeconds(90)),
            "the daemon never became ready");

        Assert.True(
            WaitUntil(() => !File.Exists(_endpoint.DiscoveryPath), TimeSpan.FromSeconds(90)),
            "the daemon did not shut down when idle");

        daemon!.WaitForExit(10_000);
        Assert.True(daemon.HasExited, "the daemon process should have exited");
        Assert.False(File.Exists(_endpoint.SocketPath), "the socket file should be removed on idle shutdown");
    }

    private string Status()
    {
        var response = DaemonClient.TrySend(_endpoint, new DaemonRequest { Tool = DaemonCommands.Status });
        Assert.NotNull(response);
        Assert.NotNull(response!.Result);
        return response.Result!;
    }

    private DaemonResponse Send(string tool, Dictionary<string, string?> parameters)
    {
        var request = new DaemonRequest
        {
            Tool = tool,
            Params = parameters.ToDictionary(
                pair => pair.Key,
                pair => JsonSerializer.SerializeToElement(pair.Value),
                StringComparer.OrdinalIgnoreCase)
        };

        var response = DaemonClient.TrySend(_endpoint, request, TimeSpan.FromMinutes(2));
        Assert.NotNull(response);
        return response!;
    }

    private static string LoadStamp(string status)
        => status.Split(' ').FirstOrDefault(field => field.StartsWith("loadedAt=", StringComparison.Ordinal)) ?? "-";

    private static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < timeout)
        {
            if (condition())
                return true;

            Thread.Sleep(100);
        }

        return condition();
    }
}
