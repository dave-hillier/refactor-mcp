using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// The live <see cref="SolutionSession"/>s in this process, keyed by the
/// absolute path of the solution they serve. A daemon serves one solution and
/// therefore holds one session; a test run or the CLI may hold several.
/// </summary>
internal static class SessionRegistry
{
    private static readonly object Gate = new();

    private static readonly Dictionary<string, SolutionSession> Sessions =
        new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    /// <summary>
    /// The most recently created session. Tools that only receive a file path
    /// resolve it against this session's solution directory.
    /// </summary>
    public static SolutionSession? Current { get; private set; }

    /// <summary>
    /// Whether loaded sessions follow the files on disk. This is on for the
    /// long lived front ends (the daemon and the MCP server), whose solutions
    /// outlive the edit that a one shot command would have picked up anyway.
    /// </summary>
    public static bool WatchForFileChanges { get; set; }

    /// <summary>
    /// Returns the session for a solution, creating an empty one if this is the
    /// first time the solution has been seen. An existing session is reused,
    /// which is what makes the second tool call in a session cheap.
    /// </summary>
    public static SolutionSession GetOrCreate(string solutionPath)
    {
        var key = Normalize(solutionPath);

        lock (Gate)
        {
            if (Sessions.TryGetValue(key, out var existing))
            {
                Current = existing;
                return existing;
            }

            var session = new SolutionSession(key);
            Sessions.Add(key, session);
            Current = session;
            return session;
        }
    }

    /// <summary>Returns the session for a solution if one exists.</summary>
    public static SolutionSession? Find(string solutionPath)
    {
        if (!TryNormalize(solutionPath, out var key))
            return null;

        lock (Gate)
        {
            return Sessions.TryGetValue(key, out var session) ? session : null;
        }
    }

    /// <summary>Drops and disposes the session for a solution.</summary>
    public static bool Unload(string solutionPath)
    {
        if (!TryNormalize(solutionPath, out var key))
            return false;

        SolutionSession? session;

        lock (Gate)
        {
            if (!Sessions.Remove(key, out session))
                return false;

            if (ReferenceEquals(Current, session))
                Current = null;
        }

        session.Dispose();
        return true;
    }

    /// <summary>Drops and disposes every session.</summary>
    public static void Clear()
    {
        List<SolutionSession> sessions;

        lock (Gate)
        {
            sessions = Sessions.Values.ToList();
            Sessions.Clear();
            Current = null;
        }

        foreach (var session in sessions)
            session.Dispose();
    }

    /// <summary>A snapshot of the live sessions, for status reporting.</summary>
    public static IReadOnlyList<SolutionSession> All
    {
        get
        {
            lock (Gate)
            {
                return Sessions.Values.ToList();
            }
        }
    }

    /// <summary>Absolute, normalised key for a solution path.</summary>
    public static string Normalize(string solutionPath) => Path.GetFullPath(solutionPath);

    private static bool TryNormalize(string solutionPath, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(solutionPath))
            return false;

        try
        {
            normalized = Normalize(solutionPath);
            return true;
        }
        catch (ArgumentException)
        {
            // Not a usable path; treat it as unknown rather than throwing.
            return false;
        }
    }
}
