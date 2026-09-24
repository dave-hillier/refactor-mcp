using ModelContextProtocol.Server;
using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.IO;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using Microsoft.CodeAnalysis.Host.Mef;
using System.Collections.Generic;



internal static class RefactoringHelpers
{
    // Solutions are owned by the SolutionSession that loaded them.
    internal static void ClearAllCaches()
    {
        SessionRegistry.Clear();
    }

    private static readonly Lazy<AdhocWorkspace> _workspace =
        new(() => new AdhocWorkspace());

    private static bool _msbuildRegistered;
    private static readonly object _msbuildLock = new();

    internal static AdhocWorkspace SharedWorkspace => _workspace.Value;

    /// <summary>
    /// Registers the MSBuild assemblies. Called once at process start, because
    /// MSBuildLocator must run before anything loads a Microsoft.Build type,
    /// and called again lazily for hosts that do not go through Program.
    /// </summary>
    internal static void EnsureMsBuildRegistered()
    {
        if (_msbuildRegistered) return;
        lock (_msbuildLock)
        {
            if (_msbuildRegistered) return;
            MSBuildLocator.RegisterDefaults();
            _msbuildRegistered = true;
        }
    }

    internal static MSBuildWorkspace CreateWorkspace()
    {
        EnsureMsBuildRegistered();
        var host = MefHostServices.Create(MSBuildMefHostServices.DefaultAssemblies);
        var workspace = MSBuildWorkspace.Create(host);
        workspace.WorkspaceFailed += (_, e) =>
            Console.Error.WriteLine(e.Diagnostic.Message);
        return workspace;
    }

    internal static async Task<Solution> GetOrLoadSolution(
        string solutionPath,
        CancellationToken cancellationToken = default)
    {
        var session = SessionRegistry.GetOrCreate(solutionPath);
        return await session.GetOrLoadAsync(progress: null, cancellationToken);
    }

    /// <summary>
    /// Resolves a file path against the loaded session's solution directory.
    /// Absolute paths pass through unchanged, and callers that have loaded no
    /// solution keep the process's current directory behaviour.
    /// </summary>
    internal static string? ResolvePath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return filePath;

        return SessionRegistry.Current?.ResolvePath(filePath) ?? Path.GetFullPath(filePath);
    }

    // Solutions are immutable, so replacing the cached instance is safe even
    // when accessed concurrently by multiple threads.
    internal static void UpdateSolutionCache(Document updatedDocument)
    {
        var solutionPath = updatedDocument.Project.Solution.FilePath;
        if (!string.IsNullOrEmpty(solutionPath))
        {
            SessionRegistry.GetOrCreate(solutionPath!).Replace(updatedDocument.Project.Solution);
            if (!string.IsNullOrEmpty(updatedDocument.FilePath))
            {
                _ = MetricsProvider.RefreshFileMetrics(solutionPath!, updatedDocument.FilePath!);
            }
        }
    }

    /// <summary>
    /// Finds a document in a solution. Relative paths are resolved against the
    /// solution's own directory, not the process's current directory.
    /// </summary>
    internal static Document? GetDocumentByPath(Solution solution, string filePath)
    {
        var normalizedPath = NormalizeAgainstSolution(solution, filePath);
        return solution.Projects
            .SelectMany(p => p.Documents)
            .FirstOrDefault(d => NormalizeAgainstSolution(solution, d.FilePath ?? "") == normalizedPath);
    }

    private static string NormalizeAgainstSolution(Solution solution, string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return filePath;

        if (Path.IsPathRooted(filePath))
            return Path.GetFullPath(filePath);

        var solutionDirectory = solution.FilePath is null ? null : Path.GetDirectoryName(solution.FilePath);
        return solutionDirectory is null
            ? Path.GetFullPath(filePath)
            : Path.GetFullPath(Path.Combine(solutionDirectory, filePath));
    }

    internal static bool TryParseRange(string range, out int startLine, out int startColumn, out int endLine, out int endColumn)
    {
        startLine = startColumn = endLine = endColumn = 0;
        var parts = range.Split('-');
        if (parts.Length != 2) return false;
        var startParts = parts[0].Split(':');
        var endParts = parts[1].Split(':');
        if (startParts.Length != 2 || endParts.Length != 2) return false;
        return int.TryParse(startParts[0], out startLine) &&
               int.TryParse(startParts[1], out startColumn) &&
               int.TryParse(endParts[0], out endLine) &&
               int.TryParse(endParts[1], out endColumn);
    }

    internal static bool ValidateRange(
        SourceText text,
        int startLine,
        int startColumn,
        int endLine,
        int endColumn,
        out string error)
    {
        error = string.Empty;
        if (startLine <= 0 || startColumn <= 0 || endLine <= 0 || endColumn <= 0)
        {
            error = "Error: Range values must be positive";
            return false;
        }
        if (startLine > endLine || (startLine == endLine && startColumn >= endColumn))
        {
            error = "Error: Range start must precede end";
            return false;
        }
        if (startLine > text.Lines.Count || endLine > text.Lines.Count)
        {
            error = "Error: Range exceeds file length";
            return false;
        }
        // The end offset is exclusive, so a column one past the last character of
        // its line is how a selection running to the end of a line is written.
        // Anything further has crossed the newline into the next line, at either
        // end of the range: a start column past its line resolves into the next
        // line just as silently.
        if (startColumn > text.Lines[startLine - 1].Span.Length + 1)
        {
            error = "Error: Range exceeds line length";
            return false;
        }
        if (endColumn > text.Lines[endLine - 1].Span.Length + 1)
        {
            error = "Error: Range exceeds line length";
            return false;
        }
        return true;
    }

    /// <summary>
    /// Parses a selection range, validates it against the source text, and returns a TextSpan.
    /// Consolidates the common pattern of TryParseRange + ValidateRange + span calculation.
    /// </summary>
    internal static TextSpan ParseSelectionRange(SourceText sourceText, string selectionRange)
    {
        if (!TryParseRange(selectionRange, out var startLine, out var startColumn, out var endLine, out var endColumn))
            throw new McpException("Error: Invalid selection range format. Use 'startLine:startColumn-endLine:endColumn'");

        if (!ValidateRange(sourceText, startLine, startColumn, endLine, endColumn, out var error))
            throw new McpException(error);

        var startPosition = sourceText.Lines[startLine - 1].Start + startColumn - 1;
        var endPosition = sourceText.Lines[endLine - 1].Start + endColumn - 1;
        return TextSpan.FromBounds(startPosition, endPosition);
    }

    /// <summary>
    /// Writes file content with encoding and updates all caches.
    /// Consolidates the common pattern of GetFileEncoding + WriteAllText + UpdateSolutionCache.
    /// </summary>
    internal static async Task WriteAndUpdateCachesAsync(Document document, SyntaxNode newRoot)
    {
        var newDocument = document.WithSyntaxRoot(newRoot);
        var newText = await newDocument.GetTextAsync();
        var encoding = await GetFileEncodingAsync(document.FilePath!);
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString(), encoding);
        UpdateSolutionCache(newDocument);
    }


    internal static async Task<Document?> FindClassInSolution(
        Solution solution,
        string className,
        params string[]? excludingFilePaths)
    {
        foreach (var doc in solution.Projects.SelectMany(p => p.Documents))
        {
            var docPath = doc.FilePath ?? string.Empty;
            if (excludingFilePaths != null && excludingFilePaths.Any(p => NormalizeAgainstSolution(solution, docPath) == NormalizeAgainstSolution(solution, p)))
                continue;

            var root = await doc.GetSyntaxRootAsync();
            if (root != null && root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                    .Any(c => c.Identifier.Text == className))
            {
                return doc;
            }
        }

        return null;
    }

    internal static async Task<Document?> FindTypeInSolution(
        Solution solution,
        string typeName,
        params string[]? excludingFilePaths)
    {
        foreach (var doc in solution.Projects.SelectMany(p => p.Documents))
        {
            var docPath = doc.FilePath ?? string.Empty;
            if (excludingFilePaths != null && excludingFilePaths.Any(p => NormalizeAgainstSolution(solution, docPath) == NormalizeAgainstSolution(solution, p)))
                continue;

            var root = await doc.GetSyntaxRootAsync();
            if (root != null && root.DescendantNodes().Any(n =>
                    n is BaseTypeDeclarationSyntax bt && bt.Identifier.Text == typeName ||
                    n is EnumDeclarationSyntax en && en.Identifier.Text == typeName ||
                    n is DelegateDeclarationSyntax dd && dd.Identifier.Text == typeName))
            {
                return doc;
            }
        }

        return null;
    }

    internal static void AddDocumentToProject(Project project, string filePath)
    {
        if (project.Documents.Any(d =>
                Path.GetFullPath(d.FilePath ?? "") == Path.GetFullPath(filePath)))
            return;

        var text = SourceText.From(File.ReadAllText(filePath));
        var newDoc = project.AddDocument(Path.GetFileName(filePath), text, filePath: filePath);

        var solutionPath = project.Solution.FilePath;
        if (!string.IsNullOrEmpty(solutionPath))
        {
            SessionRegistry.GetOrCreate(solutionPath!).Replace(newDoc.Project.Solution);
        }
    }

    internal static async Task<(string Text, Encoding Encoding)> ReadFileWithEncodingAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        filePath = ResolvePath(filePath)!;

        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        var encoding = DetectEncoding(bytes);
        var text = encoding.GetString(bytes);
        return (text, encoding);
    }

    internal static async Task<Encoding> GetFileEncodingAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        filePath = ResolvePath(filePath)!;

        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        return DetectEncoding(bytes);
    }

    private static Encoding DetectEncoding(byte[] bytes)
    {
        if (bytes.Length >= 4)
        {
            if (bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF)
                return new UTF32Encoding(true, true);
            if (bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
                return new UTF32Encoding(false, true);
        }
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8;
        if (bytes.Length >= 2)
        {
            if (bytes[0] == 0xFE && bytes[1] == 0xFF)
                return Encoding.BigEndianUnicode;
            if (bytes[0] == 0xFF && bytes[1] == 0xFE)
                return Encoding.Unicode;
        }
        return Encoding.UTF8;
    }

    internal static async Task<string> RunWithSolution(
        string solutionPath,
        string filePath,
        Func<Document, Task<string>> withSolution)
    {
        var solution = await GetOrLoadSolution(solutionPath);
        var document = GetDocumentByPath(solution, filePath)
            ?? throw new McpException($"Error: File {filePath} not found in solution");
        return await withSolution(document);
    }
}
