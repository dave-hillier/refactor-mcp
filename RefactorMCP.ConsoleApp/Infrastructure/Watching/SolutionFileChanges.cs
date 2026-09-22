using System;
using System.IO;

/// <summary>
/// What a file system change under the solution directory means for the cached
/// solution.
/// </summary>
public enum FileChangeAction
{
    /// <summary>Not part of the solution: ignore it.</summary>
    Ignore,

    /// <summary>Replace that document's text, which is cheap and incremental.</summary>
    UpdateDocument,

    /// <summary>The project graph may have changed, so reload before the next call.</summary>
    Reload
}

internal static class SolutionFileChanges
{
    private static readonly string[] ProjectFiles =
    {
        ".csproj", ".sln", ".slnf", ".props", ".targets", ".csx"
    };

    private static readonly string[] ProjectFileNames =
    {
        "global.json", "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "nuget.config"
    };

    public static bool IsSource(string path)
        => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);

    /// <summary>Files that decide what the solution contains and how it builds.</summary>
    public static bool IsProjectFile(string path)
    {
        var name = Path.GetFileName(path);

        foreach (var extension in ProjectFiles)
        {
            if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return Array.Exists(ProjectFileNames, known => string.Equals(known, name, StringComparison.OrdinalIgnoreCase));
    }

    public static FileChangeAction Classify(string path, WatcherChangeTypes change)
    {
        if (IsProjectFile(path))
            return FileChangeAction.Reload;

        if (!IsSource(path))
            return FileChangeAction.Ignore;

        // A new or removed file changes which documents the project has; an
        // edited one is just new text for a document that already exists.
        return change == WatcherChangeTypes.Changed
            ? FileChangeAction.UpdateDocument
            : FileChangeAction.Reload;
    }
}
