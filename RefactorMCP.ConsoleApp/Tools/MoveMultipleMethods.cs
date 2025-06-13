using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Text.Json;

// ===============================================================================
// MULTIPLE METHOD MOVEMENT TOOL
// ===============================================================================
// This tool extends the single method movement tool to handle multiple methods
// with automatic dependency ordering. It follows the same layered architecture:
//
// 1. AST TRANSFORMATION LAYER: Pure syntax tree operations (no file I/O)
//    - MoveMultipleMethodsAst(): Coordinates multiple method moves
//    - OrderOperations(): Analyzes dependencies and orders moves
//    - BuildDependencies(): Maps method call dependencies
//
// 2. FILE OPERATION LAYER: File I/O operations using AST layer
//    - MoveMultipleMethodsInFile(): Handles file-based bulk moves
//
// 3. SOLUTION OPERATION LAYER: Solution/Document operations using AST layer
//    - MoveMultipleMethods(): Public API for solution-based bulk moves
//
// 4. LEGACY COMPATIBILITY: String-based methods for backward compatibility
//    - MoveMultipleMethodsInSource(): Legacy string-based bulk moves
// ===============================================================================

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

    // ===== AST TRANSFORMATION LAYER =====
    // Pure syntax tree operations with no file I/O

    public static SyntaxNode MoveMultipleMethodsAst(SyntaxNode sourceRoot, IEnumerable<MoveOperation> operations)
    {
        var orderedOps = OrderOperations(sourceRoot, operations.ToList());
        var workingRoot = sourceRoot;

        foreach (var op in orderedOps)
        {
            if (op.IsStatic)
            {
                var moveResult = MoveMethodsTool.MoveStaticMethodAst(workingRoot, op.Method, op.TargetClass);
                workingRoot = MoveMethodsTool.AddMethodToTargetClass(moveResult.NewSourceRoot, op.TargetClass, moveResult.MovedMethod);
            }
            else
            {
                var moveResult = MoveMethodsTool.MoveInstanceMethodAst(workingRoot, op.SourceClass, op.Method, op.TargetClass, op.AccessMember, op.AccessMemberType);
                workingRoot = MoveMethodsTool.AddMethodToTargetClass(moveResult.NewSourceRoot, op.TargetClass, moveResult.MovedMethod);
            }
        }

        return workingRoot;
    }

    // ===== FILE OPERATION LAYER =====
    // File I/O operations that use the AST layer

    public static async Task<string> MoveMultipleMethodsInFile(string filePath, string operationsJson)
    {
        var ops = JsonSerializer.Deserialize<List<MoveOperation>>(operationsJson);
        if (ops == null || ops.Count == 0)
            throw new McpException("Error: No operations provided");

        if (!File.Exists(filePath))
            throw new McpException($"Error: File {filePath} not found (current dir: {Directory.GetCurrentDirectory()})");

        var sourceText = await File.ReadAllTextAsync(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var sourceRoot = await syntaxTree.GetRootAsync();

        // Perform AST transformations
        var resultRoot = MoveMultipleMethodsAst(sourceRoot, ops);

        // Format and write back
        var formatted = Formatter.Format(resultRoot, RefactoringHelpers.SharedWorkspace);
        await File.WriteAllTextAsync(filePath, formatted.ToFullString());

        return $"Successfully moved {ops.Count} methods";
    }

    // ===== HELPER METHODS =====

    private static Dictionary<string, HashSet<string>> BuildDependencies(SyntaxNode sourceRoot, IEnumerable<MoveOperation> ops)
    {
        // Build map keyed by "Class.Method" to support duplicate method names in different classes
        var opSet = ops.Select(o => ($"{o.SourceClass}.{o.Method}")).ToHashSet();
        var map = sourceRoot.DescendantNodes().OfType<ClassDeclarationSyntax>()
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

    private static List<MoveOperation> OrderOperations(SyntaxNode sourceRoot, List<MoveOperation> ops)
    {
        var deps = BuildDependencies(sourceRoot, ops);
        return ops.OrderBy(o => deps.TryGetValue($"{o.SourceClass}.{o.Method}", out var d) ? d.Count : 0).ToList();
    }

    // ===== LEGACY STRING-BASED METHODS (for backward compatibility) =====

    public static string MoveMultipleMethodsInSource(string sourceText, string operationsJson)
    {
        var ops = JsonSerializer.Deserialize<List<MoveOperation>>(operationsJson);
        if (ops == null || ops.Count == 0)
            return RefactoringHelpers.ThrowMcpException("Error: No operations provided");

        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetRoot();

        var resultRoot = MoveMultipleMethodsAst(root, ops);
        var formatted = Formatter.Format(resultRoot, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }

    // ===== SOLUTION OPERATION LAYER =====
    // Solution/Document operations that use the AST layer

    [McpServerTool, Description("Move multiple methods to target classes, automatically ordering by dependencies")]
    public static async Task<string> MoveMultipleMethods(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file containing the methods")] string filePath,
        [Description("JSON array describing the move operations")] string operationsJson)
    {
        var ops = JsonSerializer.Deserialize<List<MoveOperation>>(operationsJson);
        if (ops == null || ops.Count == 0)
            return RefactoringHelpers.ThrowMcpException("Error: No operations provided");

        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath);
        var document = RefactoringHelpers.GetDocumentByPath(solution, filePath);

        if (document != null)
        {
            // Solution-based: need to manage document state between operations
            var sourceText = await File.ReadAllTextAsync(filePath);
            var results = new List<string>();
            var ordered = OrderOperations(CSharpSyntaxTree.ParseText(sourceText).GetRoot(), ops);
            
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
            
            return string.Join("\n", results);
        }
        else
        {
            // Single-file mode: use the more efficient AST-based operations
            return await MoveMultipleMethodsInFile(filePath, operationsJson);
        }
    }
}
