using ModelContextProtocol.Server;
using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Caching.Memory;
using RefactorMCP.ConsoleApp.SyntaxRewriters;
using System.Text.Json;
using System.IO;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1052:Static holder types should be Static or NotInheritable", Justification = "Utility class")]
public static class MetricsProvider
{
    private static readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private static string CacheFilePath => Path.Combine(Directory.GetCurrentDirectory(), "codeMetricsCache.json");

    static MetricsProvider()
    {
        if (File.Exists(CacheFilePath))
        {
            try
            {
                var text = File.ReadAllText(CacheFilePath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(text) ?? new();
                foreach (var kv in dict)
                    _cache.Set(kv.Key, kv.Value);
            }
            catch
            {
                // ignore corrupted cache
            }
        }
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
                Dictionary<string, string> disk;
                if (File.Exists(CacheFilePath))
                {
                    var text = await File.ReadAllTextAsync(CacheFilePath);
                    disk = JsonSerializer.Deserialize<Dictionary<string, string>>(text) ?? new();
                }
                else
                {
                    disk = new();
                }
                disk[key] = json;
                var diskJson = JsonSerializer.Serialize(disk, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(CacheFilePath, diskJson);
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
