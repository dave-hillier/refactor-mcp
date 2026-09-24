using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;
using ModelContextProtocol;

/// <summary>
/// Everything that belongs to one loaded solution: the workspace that loaded
/// it and the current immutable <see cref="Solution"/>.
///
/// A session also owns the solution directory, so tools can resolve relative
/// file paths without the process's current directory being moved under them.
/// </summary>
internal sealed class SolutionSession : IDisposable
{
    private readonly object _workspaceLock = new();

    private readonly object _solutionLock = new();

    private MSBuildWorkspace? _workspace;
    private Solution? _solution;
    private SolutionWatcher? _watcher;
    private bool _reloadRequested;
    private bool _disposed;

    public SolutionSession(string solutionPath)
    {
        SolutionPath = Path.GetFullPath(solutionPath);
        SolutionDirectory = Path.GetDirectoryName(SolutionPath)
            ?? throw new ArgumentException($"'{solutionPath}' is not a file path", nameof(solutionPath));
    }

    /// <summary>Absolute path of the solution this session serves.</summary>
    public string SolutionPath { get; }

    /// <summary>Directory that relative paths in tool calls are resolved against.</summary>
    public string SolutionDirectory { get; }

    /// <summary>The loaded solution, or null if it has not been loaded yet.</summary>
    public Solution? Solution
    {
        get
        {
            lock (_solutionLock)
            {
                return _solution;
            }
        }
    }

    public bool IsLoaded => Solution is not null;

    /// <summary>When the solution was loaded, for status and diagnostics.</summary>
    public DateTime? LoadedAtUtc { get; private set; }

    public string SolutionFileName => Path.GetFileName(SolutionPath);

    /// <summary>Loads the solution if it is not already loaded, otherwise reuses it.</summary>
    public async Task<Solution> GetOrLoadAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        // A change to the project files marks the session for a reload, which
        // happens here, before anything is asked of the solution.
        if (_reloadRequested)
            Drop();

        var loaded = Solution;
        if (loaded is not null)
            return loaded;

        RefactoringHelpers.EnsureMsBuildRegistered();

        var workspace = GetOrCreateWorkspace();
        progress?.Report($"Loading {SolutionPath}");
        var solution = await workspace.OpenSolutionAsync(SolutionPath, progress: null, cancellationToken);

        lock (_solutionLock)
        {
            _solution = solution;
        }

        LoadedAtUtc = DateTime.UtcNow;
        StartWatching();
        return solution;
    }

    /// <summary>
    /// Applies a file change made by another process. An edited document is
    /// replaced in place; a change that touches the project graph asks for a
    /// reload before the next call.
    /// </summary>
    public void HandleFileChange(string filePath, WatcherChangeTypes change)
    {
        if (_disposed)
            return;

        var action = SolutionFileChanges.Classify(filePath, change);
        if (action == FileChangeAction.Ignore)
            return;

        try
        {
            if (action == FileChangeAction.Reload)
            {
                MarkForReload();
                return;
            }

            var solution = Solution;
            if (solution is null)
                return;

            // Only an edited document can be replaced in place; edits to files
            // the solution does not contain are none of its business.
            var document = RefactoringHelpers.GetDocumentByPath(solution, filePath);
            if (document is null)
                return;

            var (text, encoding) = RefactoringHelpers.ReadFileWithEncodingAsync(filePath).GetAwaiter().GetResult();
            if (string.Equals(text, document.GetTextAsync().GetAwaiter().GetResult().ToString(), StringComparison.Ordinal))
            {
                // The cached document already holds this text, which is what a
                // change written by one of our own tools looks like.
                return;
            }

            lock (_solutionLock)
            {
                // A tool may have replaced the solution while this change was
                // being read; its version is the newer one.
                if (ReferenceEquals(_solution, solution))
                    _solution = solution.WithDocumentText(document.Id, SourceText.From(text, encoding));
            }

        }
        catch (Exception)
        {
            // Refreshing is best effort. A reload is the safe answer when it
            // fails, because the cached text can no longer be trusted.
            MarkForReload();
        }
    }

    /// <summary>Reload the solution before the next call.</summary>
    public void MarkForReload() => _reloadRequested = true;

    private void StartWatching()
    {
        if (!SessionRegistry.WatchForFileChanges)
            return;

        lock (_workspaceLock)
        {
            if (_disposed || _watcher is not null || !Directory.Exists(SolutionDirectory))
                return;

            try
            {
                _watcher = new SolutionWatcher(this, SolutionDirectory);
            }
            catch (Exception)
            {
                // Watching is an optimisation: without it the session is simply
                // as fresh as it used to be.
            }
        }
    }

    /// <summary>Throws away the loaded solution and the workspace that loaded it.</summary>
    private void Drop()
    {
        lock (_workspaceLock)
        {
            _reloadRequested = false;
            _workspace?.Dispose();
            _workspace = null;
            LoadedAtUtc = null;
        }

        lock (_solutionLock)
        {
            _solution = null;
        }
    }

    /// <summary>
    /// Replaces the cached solution after a tool has rewritten documents. The
    /// solution is immutable, so a reader either sees the old or the new one.
    /// </summary>
    public void Replace(Solution solution)
    {
        lock (_solutionLock)
        {
            _solution = solution;
        }
    }

    /// <summary>
    /// Makes <paramref name="path"/> absolute, resolving relative paths against
    /// the solution directory rather than the process's current directory.
    /// </summary>
    public string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(SolutionDirectory, path));
    }

    private MSBuildWorkspace GetOrCreateWorkspace()
    {
        lock (_workspaceLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _workspace ??= RefactoringHelpers.CreateWorkspace();
        }
    }

    public void Dispose()
    {
        lock (_workspaceLock)
        {
            if (_disposed)
                return;

            _disposed = true;
            _watcher?.Dispose();
            _watcher = null;
            _workspace?.Dispose();
            _workspace = null;
        }

        lock (_solutionLock)
        {
            _solution = null;
        }
    }
}
