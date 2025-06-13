using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;

[McpServerToolType]
public static class MoveMultipleMethodsTool
{
    public class MoveOperation
    {
        public string SourceClass { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public string TargetClass { get; set; } = string.Empty;
        public string AccessMember { get; set; } = string.Empty;
        public string AccessMemberType { get; set; } = "field";
        public bool IsStatic { get; set; }
        public string? TargetFile { get; set; }
    }

    private static Dictionary<string, HashSet<string>> BuildDependencies(string sourceText, IEnumerable<MoveOperation> ops)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetRoot();

        // Build map keyed by "Class.Method" to support duplicate method names in different classes
        var opSet = ops.Select(o => ($"{o.SourceClass}.{o.Method}")).ToHashSet();
        var map = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .SelectMany(cls => cls.Members.OfType<MethodDeclarationSyntax>()
                .Select(m => new { Key = $"{cls.Identifier.ValueText}.{m.Identifier.ValueText}", Method = m }))
            .Where(x => opSet.Contains(x.Key))
            .ToDictionary(x => x.Key, x => x.Method);

        var deps = new Dictionary<string, HashSet<string>>();
        foreach (var op in ops)
        {
            var key = $"{op.SourceClass}.{op.Method}";
            if (!map.TryGetValue(key, out var method))
            {
                deps[key] = new HashSet<string>();
                continue;
            }

            var called = method.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Select(inv => inv.Expression switch
                {
                    IdentifierNameSyntax id => id.Identifier.ValueText,
                    MemberAccessExpressionSyntax ma => ma.Name.Identifier.ValueText,
                    _ => null
                })
                .Where(n => n != null)
                .Select(name => $"{op.SourceClass}.{name}")
                .Where(n => map.ContainsKey(n))
                .ToHashSet();

            deps[key] = called;
        }

        return deps;
    }

    private static List<MoveOperation> OrderOperations(string sourceText, List<MoveOperation> ops)
    {
        var deps = BuildDependencies(sourceText, ops);
        return ops.OrderBy(o => deps.TryGetValue($"{o.SourceClass}.{o.Method}", out var d) ? d.Count : 0).ToList();
    }

    public static string MoveMultipleMethodsInSource(string sourceText, string operationsJson)
    {
        var ops = JsonSerializer.Deserialize<List<MoveOperation>>(operationsJson);
        if (ops == null || ops.Count == 0)
            return RefactoringHelpers.ThrowMcpException("Error: No operations provided");

        var ordered = OrderOperations(sourceText, ops);
        var working = sourceText;
        foreach (var op in ordered)
        {
            if (op.IsStatic)
            {
                working = MoveMethodsTool.MoveStaticMethodInSource(working, op.Method, op.TargetClass);
            }
            else
            {
                working = MoveMethodsTool.MoveInstanceMethodInSource(working, op.SourceClass, op.Method, op.TargetClass, op.AccessMember, op.AccessMemberType);
            }
        }
        return working;
    }

    [McpServerTool, Description("Move multiple methods to target classes, automatically ordering by dependencies")]
    public static async Task<string> MoveMultipleMethods(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file containing the methods")] string filePath,
        [Description("JSON array describing the move operations")] string operationsJson)
    {
        var ops = JsonSerializer.Deserialize<List<MoveOperation>>(operationsJson);
        if (ops == null || ops.Count == 0)
            return RefactoringHelpers.ThrowMcpException("Error: No operations provided");

        var sourceText = await File.ReadAllTextAsync(filePath);
        var ordered = OrderOperations(sourceText, ops);
        var results = new List<string>();

        // Check if we're using a solution or single-file mode
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath);
        var document = RefactoringHelpers.GetDocumentByPath(solution, filePath);

        if (document != null)
        {
            // Solution-based: need to manage document state between operations
            var currentDocument = document;
            foreach (var op in ordered)
            {
                if (op.IsStatic)
                {
                    results.Add(await MoveMethodsTool.MoveStaticMethod(solutionPath, filePath, op.Method, op.TargetClass, op.TargetFile));
                }
                else
                {
                    // For instance methods, pass single method to avoid reloading solution
                    results.Add(await MoveMethodsTool.MoveInstanceMethod(solutionPath, filePath, op.SourceClass, op.Method, op.TargetClass, op.AccessMember, op.AccessMemberType, op.TargetFile));
                }

                // Clear solution cache to force reload of updated file state
                RefactoringHelpers.SolutionCache.Remove(solutionPath);
            }
        }
        else
        {
            // Single-file mode: use the more efficient string-based operations
            var working = sourceText;
            foreach (var op in ordered)
            {
                if (op.IsStatic)
                {
                    working = MoveMethodsTool.MoveStaticMethodInSource(working, op.Method, op.TargetClass);
                    results.Add($"Successfully moved static method '{op.Method}' to {op.TargetClass}");
                }
                else
                {
                    working = MoveMethodsTool.MoveInstanceMethodInSource(working, op.SourceClass, op.Method, op.TargetClass, op.AccessMember, op.AccessMemberType);
                    results.Add($"Successfully moved instance method '{op.Method}' from {op.SourceClass} to {op.TargetClass}");
                }
            }
            await File.WriteAllTextAsync(filePath, working);
        }

        return string.Join("\n", results);
    }
}
