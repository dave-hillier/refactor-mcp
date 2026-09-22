using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// The <c>serve</c> verb: keep one solution loaded and answer tool calls over a
/// local socket until the daemon is stopped or has been idle for a while.
/// </summary>
internal static class ServeCommand
{
    public const string Usage = "Usage: serve --solution <path> [--idle-timeout 10m]";

    public static async Task<int> RunAsync(string[] args)
    {
        // A daemon holds its solution for a long time, so it has to notice
        // files changing underneath it.
        SessionRegistry.WatchForFileChanges = true;

        var command = ParsedCommand.Parse(args);
        var solutionPath = command.Option("solution") ?? command.Positionals.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(solutionPath))
        {
            Console.Error.WriteLine(Usage);
            return 1;
        }

        var idleSetting = command.Option("idle-timeout");
        var idleTimeout = ParsedCommand.ParseDuration(idleSetting) ?? TimeSpan.FromMinutes(10);

        if (idleSetting is not null && ParsedCommand.ParseDuration(idleSetting) is null)
        {
            Console.Error.WriteLine($"Error: could not read idle timeout '{idleSetting}'. Try 30s, 10m or 0 to never expire.");
            return 1;
        }

        var endpoint = DaemonEndpoint.For(solutionPath);

        using var daemon = new SolutionDaemon(endpoint, idleTimeout);
        using var shutdown = new CancellationTokenSource();

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        // A detached daemon is asked to stop with SIGTERM.
        using var terminate = RegisterSigterm(shutdown);

        return await daemon.RunAsync(shutdown.Token);
    }

    private static IDisposable? RegisterSigterm(CancellationTokenSource shutdown)
    {
        if (OperatingSystem.IsWindows())
            return null;

        return PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ => shutdown.Cancel());
    }
}
