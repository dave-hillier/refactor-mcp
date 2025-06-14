using ModelContextProtocol.Server;
using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Build.Locator;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.IO;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using System.Text;



internal static class RefactoringHelpers
{
    internal static MemoryCache SolutionCache = new(new MemoryCacheOptions());

    private static readonly Lazy<AdhocWorkspace> _workspace =
        new(() => new AdhocWorkspace());

    private static bool _msbuildRegistered;
    private static readonly object _msbuildLock = new();

    internal static AdhocWorkspace SharedWorkspace => _workspace.Value;

    private static void EnsureMsBuildRegistered()
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
        var workspace = MSBuildWorkspace.Create();
        workspace.WorkspaceFailed += (_, e) =>
            Console.Error.WriteLine(e.Diagnostic.Message);
        return workspace;
    }

    internal static async Task<Solution> GetOrLoadSolution(string solutionPath)
    {

        if (SolutionCache.TryGetValue(solutionPath, out Solution? cachedSolution))
        {
            Directory.SetCurrentDirectory(Path.GetDirectoryName(solutionPath)!);
            return cachedSolution!;
        }
        using var workspace = CreateWorkspace();
        var solution = await workspace.OpenSolutionAsync(solutionPath);
        SolutionCache.Set(solutionPath, solution);
        Directory.SetCurrentDirectory(Path.GetDirectoryName(solutionPath)!);
        return solution;
    }

    internal static void UpdateSolutionCache(Document updatedDocument)
    {
        var solutionPath = updatedDocument.Project.Solution.FilePath;
        if (!string.IsNullOrEmpty(solutionPath))
        {
            SolutionCache.Set(solutionPath!, updatedDocument.Project.Solution);
        }
    }

    internal static Document? GetDocumentByPath(Solution solution, string filePath)
    {
        var normalizedPath = Path.GetFullPath(filePath);
        return solution.Projects
            .SelectMany(p => p.Documents)
            .FirstOrDefault(d => Path.GetFullPath(d.FilePath ?? "") == normalizedPath);
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

    internal static string ThrowMcpException(string message)
    {
        throw new McpException(message);
    }

    internal static async Task<string> ApplySingleFileEdit(
        string filePath,
        Func<string, string> transform,
        string successMessage)
    {
        if (!File.Exists(filePath))
            return ThrowMcpException($"Error: File {filePath} not found (current dir: {Directory.GetCurrentDirectory()})");

        var (sourceText, encoding) = await ReadFileWithEncodingAsync(filePath);
        var newText = transform(sourceText);
        await File.WriteAllTextAsync(filePath, newText, encoding);
        return successMessage;
    }

    internal static async Task<Document?> FindClassInSolution(
        Solution solution,
        string className,
        params string[]? excludingFilePaths)
    {
        foreach (var doc in solution.Projects.SelectMany(p => p.Documents))
        {
            var docPath = doc.FilePath ?? string.Empty;
            if (excludingFilePaths != null && excludingFilePaths.Any(p => Path.GetFullPath(docPath) == Path.GetFullPath(p)))
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
            SolutionCache.Set(solutionPath!, newDoc.Project.Solution);
        }
    }

    internal static async Task<(string Text, Encoding Encoding)> ReadFileWithEncodingAsync(string filePath)
    {
        var bytes = await File.ReadAllBytesAsync(filePath);
        var encoding = DetectEncoding(bytes);
        var text = encoding.GetString(bytes);
        return (text, encoding);
    }

    internal static async Task<Encoding> GetFileEncodingAsync(string filePath)
    {
        var bytes = await File.ReadAllBytesAsync(filePath);
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

    internal static async Task WriteFileWithEncodingAsync(string filePath, string text, Encoding encoding)
    {
        await File.WriteAllTextAsync(filePath, text, encoding);
    }

    internal static async Task<string> RunWithSolutionOrFile(
        string solutionPath,
        string filePath,
        Func<Document, Task<string>> withSolution,
        Func<string, Task<string>> singleFile)
    {
        var solution = await GetOrLoadSolution(solutionPath);
        var document = GetDocumentByPath(solution, filePath);
        if (document != null)
            return await withSolution(document);

        return await singleFile(filePath);
    }
}
