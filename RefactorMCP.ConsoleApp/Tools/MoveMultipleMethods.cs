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
        var map = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(m => ops.Any(o => o.Method == m.Identifier.ValueText))
            .ToDictionary(m => m.Identifier.ValueText, m => m);

        var deps = new Dictionary<string, HashSet<string>>();
        foreach (var op in ops)
        {
            if (!map.TryGetValue(op.Method, out var method))
            {
                deps[op.Method] = new HashSet<string>();
                continue;
            }
            var called = method.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Select(inv => inv.Expression switch
                {
                    IdentifierNameSyntax id => id.Identifier.ValueText,
                    MemberAccessExpressionSyntax ma => ma.Name.Identifier.ValueText,
                    _ => null
                })
                .Where(n => n != null && map.ContainsKey(n))
                .ToHashSet()!;
            deps[op.Method] = called;
        }
        return deps;
    }

    private static List<MoveOperation> OrderOperations(string sourceText, List<MoveOperation> ops)
    {
        var deps = BuildDependencies(sourceText, ops);
        return ops.OrderBy(o => deps.TryGetValue(o.Method, out var d) ? d.Count : 0).ToList();
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
        foreach (var op in ordered)
        {
            if (op.IsStatic)
            {
                results.Add(await MoveMethodsTool.MoveStaticMethod(solutionPath, filePath, op.Method, op.TargetClass, op.TargetFile));
            }
            else
            {
                results.Add(await MoveMethodsTool.MoveInstanceMethod(solutionPath, filePath, op.SourceClass, op.Method, op.TargetClass, op.AccessMember, op.AccessMemberType, op.TargetFile));
            }
            // Refresh sourceText after each move
            sourceText = await File.ReadAllTextAsync(filePath);
        }
        return string.Join("\n", results);
    }
}
