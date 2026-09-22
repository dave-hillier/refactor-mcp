using System;
using System.IO;
using System.Linq;

/// <summary>
/// The <c>status</c> and <c>stop</c> verbs: which solutions currently have a
/// daemon, and how to shut them down.
/// </summary>
internal static class StatusCommand
{
    public const string Usage = "Usage: status | stop [--solution <path> | --all]";

    public static int Show()
    {
        var running = DaemonClient.Running();

        if (running.Count == 0)
        {
            Console.WriteLine("No daemon is running.");
            return 0;
        }

        foreach (var (endpoint, discovery) in running)
        {
            Console.WriteLine(
                $"{discovery.SolutionPath} (pid {discovery.Pid}, since {discovery.StartedAtUtc.ToLocalTime():u})");

            // The daemon knows whether the solution is loaded and how many calls
            // it has answered; that is the part a user cannot see from the
            // discovery file alone.
            var status = DaemonClient.TrySend(endpoint, new DaemonRequest { Tool = DaemonCommands.Status })?.Result;
            if (status is not null)
                Console.WriteLine($"  {status}");
        }

        return 0;
    }

    public static int Stop(string[] args)
    {
        var command = ParsedCommand.Parse(args);
        var requested = command.Option("solution") ?? command.Positionals.FirstOrDefault();
        var all = command.HasOption("all");

        if (!all && string.IsNullOrWhiteSpace(requested))
        {
            Console.Error.WriteLine(Usage);
            return 1;
        }

        var running = DaemonClient.Running();
        var stopped = 0;

        foreach (var (endpoint, discovery) in running)
        {
            if (!all && !Matches(endpoint.SolutionPath, requested!))
                continue;

            if (DaemonClient.Stop(endpoint))
            {
                Console.WriteLine($"Stopped the daemon for {discovery.SolutionPath} (pid {discovery.Pid}).");
                stopped++;
            }
        }

        if (stopped == 0)
            Console.WriteLine("No matching daemon was running.");

        return 0;
    }

    private static bool Matches(string solutionPath, string requested)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(solutionPath),
                Path.GetFullPath(requested),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
