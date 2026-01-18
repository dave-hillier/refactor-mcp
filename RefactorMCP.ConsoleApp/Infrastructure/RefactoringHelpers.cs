using ModelContextProtocol.Server;
using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Build.Locator;
using Microsoft.Extensions.Caching.Memory;
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
    // MemoryCache is thread-safe and Solution objects from Roslyn are immutable.
    // This allows us to store and access Solution instances across threads
    // without additional locking or synchronization.
    internal static MemoryCache SolutionCache = new(new MemoryCacheOptions());
    internal static MemoryCache SyntaxTreeCache = new(new MemoryCacheOptions());
    internal static MemoryCache ModelCache = new(new MemoryCacheOptions());

    internal static void ClearAllCaches()
    {
        SolutionCache.Dispose();
        SolutionCache = new MemoryCache(new MemoryCacheOptions());
        SyntaxTreeCache.Dispose();
        SyntaxTreeCache = new MemoryCache(new MemoryCacheOptions());
        ModelCache.Dispose();
        ModelCache = new MemoryCache(new MemoryCacheOptions());
    }

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

        if (SolutionCache.TryGetValue(solutionPath, out Solution? cachedSolution))
        {
            Directory.SetCurrentDirectory(Path.GetDirectoryName(solutionPath)!);
            return cachedSolution!;
        }
        using var workspace = CreateWorkspace();
        var solution = await workspace.OpenSolutionAsync(solutionPath, progress: null, cancellationToken);
        SolutionCache.Set(solutionPath, solution);
        Directory.SetCurrentDirectory(Path.GetDirectoryName(solutionPath)!);
        return solution;
    }

    // Solutions are immutable, so replacing the cached instance is safe even
    // when accessed concurrently by multiple threads.
    internal static void UpdateSolutionCache(Document updatedDocument)
    {
        var solutionPath = updatedDocument.Project.Solution.FilePath;
        if (!string.IsNullOrEmpty(solutionPath))
        {
            SolutionCache.Set(solutionPath!, updatedDocument.Project.Solution);
            if (!string.IsNullOrEmpty(updatedDocument.FilePath))
            {
                _ = MetricsProvider.RefreshFileMetrics(solutionPath!, updatedDocument.FilePath!);
            }
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
        return true;
    }


    internal static async Task<string> ApplySingleFileEdit(
        string filePath,
        Func<string, string> transform,
        string successMessage)
    {
        if (!File.Exists(filePath))
            throw new McpException($"Error: File {filePath} not found (current dir: {Directory.GetCurrentDirectory()})");

        var (sourceText, encoding) = await ReadFileWithEncodingAsync(filePath);
        var newText = transform(sourceText);

        if (newText.StartsWith("Error:"))
            return newText;

        await File.WriteAllTextAsync(filePath, newText, encoding);
        UpdateFileCaches(filePath, newText);
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

    internal static async Task<Document?> FindTypeInSolution(
        Solution solution,
        string typeName,
        params string[]? excludingFilePaths)
    {
        foreach (var doc in solution.Projects.SelectMany(p => p.Documents))
        {
            var docPath = doc.FilePath ?? string.Empty;
            if (excludingFilePaths != null && excludingFilePaths.Any(p => Path.GetFullPath(docPath) == Path.GetFullPath(p)))
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
            SolutionCache.Set(solutionPath!, newDoc.Project.Solution);
        }
    }

    private static CSharpCompilation CreateCompilation(SyntaxTree tree)
    {
        var refs = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p));
        return CSharpCompilation.Create(
            "SingleFile",
            new[] { tree },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    internal static async Task<SyntaxTree> GetOrParseSyntaxTreeAsync(string filePath)
    {
        if (SyntaxTreeCache.TryGetValue(filePath, out SyntaxTree? cached))
            return cached!;
        var (text, _) = await ReadFileWithEncodingAsync(filePath);
        var tree = CSharpSyntaxTree.ParseText(text);
        SyntaxTreeCache.Set(filePath, tree);
        return tree;
    }

    internal static async Task<SemanticModel> GetOrCreateSemanticModelAsync(string filePath)
    {
        if (ModelCache.TryGetValue(filePath, out SemanticModel? cached))
            return cached!;
        var tree = await GetOrParseSyntaxTreeAsync(filePath);
        var compilation = CreateCompilation(tree);
        var model = compilation.GetSemanticModel(tree);
        ModelCache.Set(filePath, model);
        return model;
    }

    internal static void UpdateFileCaches(string filePath, string newText)
    {
        var tree = CSharpSyntaxTree.ParseText(newText);
        SyntaxTreeCache.Set(filePath, tree);
        var compilation = CreateCompilation(tree);
        var model = compilation.GetSemanticModel(tree);
        ModelCache.Set(filePath, model);
    }

    internal static async Task<(string Text, Encoding Encoding)> ReadFileWithEncodingAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        var encoding = DetectEncoding(bytes);
        var text = encoding.GetString(bytes);
        return (text, encoding);
    }

    internal static async Task<Encoding> GetFileEncodingAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
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

    internal static async Task WriteFileWithEncodingAsync(
        string filePath,
        string text,
        Encoding encoding,
        CancellationToken cancellationToken = default)
    {
        await File.WriteAllTextAsync(filePath, text, encoding, cancellationToken);
        UpdateFileCaches(filePath, text);
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
