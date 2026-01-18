using ModelContextProtocol.Server;
using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Caching.Memory;
using RefactorMCP.ConsoleApp.SyntaxWalkers;
using System.Text.Json;
using System.IO;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1052:Static holder types should be Static or NotInheritable", Justification = "Utility class")]
public static class MetricsProvider
{
    private static readonly MemoryCache _cache = new(new MemoryCacheOptions());

    private static string GetMetricsFilePath(string solutionPath, string filePath)
    {
        var solutionDir = Path.GetDirectoryName(solutionPath)!;
        var relative = Path.GetRelativePath(solutionDir, filePath);
        var metricsPath = Path.Combine(solutionDir, ".refactor-mcp", "metrics", relative);
        return Path.ChangeExtension(metricsPath, ".json");
    }

    public static async Task<string> GetFileMetrics(
        string solutionPath,
        string filePath)
    {
        try
        {
            var key = $"{solutionPath}|{filePath}";

            if (_cache.TryGetValue(key, out string? cached))
                return cached!;

            var metricsFile = GetMetricsFilePath(solutionPath, filePath);
            if (File.Exists(metricsFile))
            {
                var fromDisk = await File.ReadAllTextAsync(metricsFile);
                _cache.Set(key, fromDisk);
                return fromDisk;
            }

            var (tree, model) = await LoadTreeAndModel(solutionPath, filePath);
            var root = await tree.GetRootAsync();
            var metrics = MetricsCalculator.Calculate(root, model);
            var json = JsonSerializer.Serialize(metrics, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            _cache.Set(key, json);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(metricsFile)!);
                await File.WriteAllTextAsync(metricsFile, json);
            }
            catch
            {
                // ignore disk cache errors
            }
            return json;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error analyzing metrics: {ex.Message}", ex);
        }
    }

    public static Task RefreshFileMetrics(string solutionPath, string filePath)
    {
        // recompute metrics and update cache/disk
        return GetFileMetrics(solutionPath, filePath);
    }

    private static async Task<(SyntaxTree tree, SemanticModel? model)> LoadTreeAndModel(string solutionPath, string filePath)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath);
        var doc = RefactoringHelpers.GetDocumentByPath(solution, filePath);
        if (doc != null)
        {
            var tree = await doc.GetSyntaxTreeAsync() ?? CSharpSyntaxTree.ParseText(await File.ReadAllTextAsync(filePath));
            var model = await doc.GetSemanticModelAsync();
            return (tree, model);
        }
        var syntaxTree = CSharpSyntaxTree.ParseText(await File.ReadAllTextAsync(filePath));
        return (syntaxTree, null);
    }

    private static class MetricsCalculator
    {
        public static FileMetrics Calculate(SyntaxNode root, SemanticModel? model)
        {
            var span = root.SyntaxTree.GetLineSpan(root.FullSpan);
            var fileLoc = span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
            var fileMetrics = new FileMetrics { LinesOfCode = fileLoc };

            var classNodes = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();
            fileMetrics.NumberOfClasses = classNodes.Count;
            foreach (var cls in classNodes)
            {
                var clsSpan = cls.GetLocation().GetLineSpan();
                var clsLoc = clsSpan.EndLinePosition.Line - clsSpan.StartLinePosition.Line + 1;
                var clsMetrics = new ClassMetrics { Name = cls.Identifier.Text, LinesOfCode = clsLoc };
                foreach (var method in cls.Members.OfType<MethodDeclarationSyntax>())
                {
                    var mSpan = method.GetLocation().GetLineSpan();
                    var mLoc = mSpan.EndLinePosition.Line - mSpan.StartLinePosition.Line + 1;
                    var walker = new ComplexityWalker();
                    walker.Visit(method);
                    clsMetrics.Methods.Add(new MethodMetrics
                    {
                        Name = method.Identifier.Text,
                        LinesOfCode = mLoc,
                        ParameterCount = method.ParameterList.Parameters.Count,
                        CyclomaticComplexity = walker.Complexity,
                        MaxNestingDepth = walker.MaxDepth
                    });
                    if (method.Modifiers.Any(SyntaxKind.PublicKeyword))
                        fileMetrics.NumberOfPublicMethods++;
                    else if (method.Modifiers.Any(SyntaxKind.PrivateKeyword) ||
                             (!method.Modifiers.Any(SyntaxKind.ProtectedKeyword) && !method.Modifiers.Any(SyntaxKind.InternalKeyword)))
                        fileMetrics.NumberOfPrivateMethods++;
                }
                fileMetrics.Classes.Add(clsMetrics);
            }
            return fileMetrics;
        }
    }


    private class FileMetrics
    {
        public int LinesOfCode { get; set; }
        public int NumberOfClasses { get; set; }
        public int NumberOfPublicMethods { get; set; }
        public int NumberOfPrivateMethods { get; set; }
        public List<ClassMetrics> Classes { get; } = new();
    }

    private class ClassMetrics
    {
        public string Name { get; set; } = string.Empty;
        public int LinesOfCode { get; set; }
        public List<MethodMetrics> Methods { get; } = new();
    }

    private class MethodMetrics
    {
        public string Name { get; set; } = string.Empty;
        public int LinesOfCode { get; set; }
        public int ParameterCount { get; set; }
        public int CyclomaticComplexity { get; set; }
        public int MaxNestingDepth { get; set; }
    }
}
