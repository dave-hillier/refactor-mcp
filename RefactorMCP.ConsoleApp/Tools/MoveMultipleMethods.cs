using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.IO;

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
                // First update the source with the stub, then add the method to target
                workingRoot = moveResult.NewSourceRoot;
                workingRoot = MoveMethodsTool.AddMethodToTargetClass(workingRoot, op.TargetClass, moveResult.MovedMethod);
            }
            else
            {
                var moveResult = MoveMethodsTool.MoveInstanceMethodAst(workingRoot, op.SourceClass, op.Method, op.TargetClass, op.AccessMember, op.AccessMemberType);
                // First update the source with the stub, then add the method to target
                workingRoot = moveResult.NewSourceRoot;
                workingRoot = MoveMethodsTool.AddMethodToTargetClass(workingRoot, op.TargetClass, moveResult.MovedMethod);
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
        var originalSourceRoot = await syntaxTree.GetRootAsync();

        var ordered = OrderOperations(originalSourceRoot, ops);
        var workingSourceRoot = originalSourceRoot;
        var targetRoots = new Dictionary<string, SyntaxNode>();

        foreach (var op in ordered)
        {
            var targetPath = op.TargetFile ?? Path.Combine(Path.GetDirectoryName(filePath)!, $"{op.TargetClass}.cs");
            var sameFile = targetPath == filePath;

            if (op.IsStatic)
            {
                var moveResult = MoveMethodsTool.MoveStaticMethodAst(workingSourceRoot, op.Method, op.TargetClass);
                workingSourceRoot = moveResult.NewSourceRoot;

                if (sameFile)
                {
                    workingSourceRoot = MoveMethodsTool.AddMethodToTargetClass(workingSourceRoot, op.TargetClass, moveResult.MovedMethod);
                }
                else
                {
                    if (!targetRoots.TryGetValue(targetPath, out var targetRoot))
                    {
                        targetRoot = await LoadOrCreateTargetRoot(targetPath);
                        targetRoot = MoveMethodsTool.PropagateUsings(originalSourceRoot, targetRoot);
                    }

                    targetRoot = MoveMethodsTool.AddMethodToTargetClass(targetRoot, op.TargetClass, moveResult.MovedMethod);
                    targetRoots[targetPath] = targetRoot;
                }
            }
            else
            {
                var moveResult = MoveMethodsTool.MoveInstanceMethodAst(
                    workingSourceRoot,
                    op.SourceClass,
                    op.Method,
                    op.TargetClass,
                    op.AccessMember,
                    op.AccessMemberType);

                workingSourceRoot = moveResult.NewSourceRoot;

                if (sameFile)
                {
                    workingSourceRoot = MoveMethodsTool.AddMethodToTargetClass(workingSourceRoot, op.TargetClass, moveResult.MovedMethod);
                }
                else
                {
                    if (!targetRoots.TryGetValue(targetPath, out var targetRoot))
                    {
                        targetRoot = await LoadOrCreateTargetRoot(targetPath);
                        targetRoot = MoveMethodsTool.PropagateUsings(originalSourceRoot, targetRoot);
                    }

                    targetRoot = MoveMethodsTool.AddMethodToTargetClass(targetRoot, op.TargetClass, moveResult.MovedMethod);
                    targetRoots[targetPath] = targetRoot;
                }
            }
        }

        var formattedSource = Formatter.Format(workingSourceRoot, RefactoringHelpers.SharedWorkspace);
        await File.WriteAllTextAsync(filePath, formattedSource.ToFullString());

        foreach (var kvp in targetRoots)
        {
            var formatted = Formatter.Format(kvp.Value, RefactoringHelpers.SharedWorkspace);
            Directory.CreateDirectory(Path.GetDirectoryName(kvp.Key)!);
            await File.WriteAllTextAsync(kvp.Key, formatted.ToFullString());
        }

        return $"Successfully moved {ops.Count} methods";
    }

    private static async Task<SyntaxNode> LoadOrCreateTargetRoot(string targetPath)
    {
        if (File.Exists(targetPath))
        {
            var targetText = await File.ReadAllTextAsync(targetPath);
            return await CSharpSyntaxTree.ParseText(targetText).GetRootAsync();
        }

        return SyntaxFactory.CompilationUnit();
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
                    var (msg, updatedDoc) = await MoveMethodsTool.MoveStaticMethodWithSolution(
                        currentDocument,
                        op.Method,
                        op.TargetClass,
                        op.TargetFile);
                    results.Add(msg);
                    currentDocument = updatedDoc;
                }
                else
                {
                    var (msg, updatedDoc) = await MoveMethodsTool.MoveInstanceMethodWithSolution(
                        currentDocument,
                        op.SourceClass,
                        op.Method,
                        op.TargetClass,
                        op.AccessMember,
                        op.AccessMemberType);
                    results.Add(msg);
                    currentDocument = updatedDoc;
                }
            }

            // Clear cache after batch completes
            RefactoringHelpers.SolutionCache.Remove(solutionPath);

            return string.Join("\n", results);
        }
        else
        {
            // Single-file mode: use the more efficient AST-based operations
            return await MoveMultipleMethodsInFile(filePath, operationsJson);
        }
    }
}
