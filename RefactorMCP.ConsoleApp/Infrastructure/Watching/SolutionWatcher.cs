using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Watches the solution directory so a long lived session does not serve stale
/// text after an editor, a formatter or another process has written a file.
///
/// Events are collected and applied after a quiet period, because a single save
/// often produces several events and a file can be caught mid write.
/// </summary>
internal sealed class SolutionWatcher : IDisposable
{
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromMilliseconds(300);

    private readonly SolutionSession _session;
    private readonly FileSystemWatcher _watcher;
    private readonly ConcurrentDictionary<string, PendingChange> _pending;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Task _pump;
    private bool _disposed;

    public SolutionWatcher(SolutionSession session, string directory)
    {
        _session = session;
        _pending = new ConcurrentDictionary<string, PendingChange>(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        _watcher = new FileSystemWatcher(directory)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName
        };

        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
        _watcher.Deleted += OnChanged;
        _watcher.Renamed += OnRenamed;
        _watcher.Error += (_, _) => _session.MarkForReload();
        _watcher.EnableRaisingEvents = true;

        _pump = Task.Run(PumpAsync);
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
        => Remember(e.FullPath, e.ChangeType);

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        // Both ends matter: the old path is a removal, the new one an addition.
        Remember(e.OldFullPath, WatcherChangeTypes.Deleted);
        Remember(e.FullPath, WatcherChangeTypes.Created);
    }

    private void Remember(string path, WatcherChangeTypes change)
    {
        if (SolutionFileChanges.Classify(path, change) == FileChangeAction.Ignore)
            return;

        _pending[path] = new PendingChange(change, DateTime.UtcNow);
    }

    private async Task PumpAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(QuietPeriod, _stopping.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                // Disposed while this loop was between checks.
                return;
            }

            foreach (var (path, seen) in _pending.ToArray())
            {
                if (DateTime.UtcNow - seen.AtUtc < QuietPeriod)
                    continue;

                // Only the last event for a path is applied, and only once it
                // has gone quiet.
                if (_pending.TryRemove(new KeyValuePair<string, PendingChange>(path, seen)))
                    _session.HandleFileChange(path, seen.Kind);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();

        _stopping.Cancel();
        _stopping.Dispose();
    }

    private readonly record struct PendingChange(WatcherChangeTypes Kind, DateTime AtUtc);
}
