using ModelContextProtocol.Server;
using ModelContextProtocol;
using System;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Collections.Generic;
using System.Linq;
using System.IO;

[McpServerToolType]
public static partial class MoveMultipleMethodsTool
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
    // Solution/Document operations that use the AST layer

    [McpServerTool, Description("Move multiple methods to target classes, automatically ordering by dependencies. " +
        "Wrapper methods remain at the original locations to delegate to the moved implementations.")]
    public static async Task<string> MoveMultipleMethods(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file containing the methods")] string filePath,
        [Description("Move operations to perform")] IEnumerable<MoveOperation> operations,
        [Description("Default target file path used when operations omit targetFile (optional)")] string? defaultTargetFilePath = null)
    {
        var ops = operations.ToList();
        if (ops.Count == 0)
            return RefactoringHelpers.ThrowMcpException("Error: No operations provided");

        if (!string.IsNullOrEmpty(defaultTargetFilePath))
        {
            foreach (var op in ops)
            {
                if (string.IsNullOrEmpty(op.TargetFile))
                    op.TargetFile = defaultTargetFilePath;
            }
        }

        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath);
        var document = RefactoringHelpers.GetDocumentByPath(solution, filePath);

        var crossFile = ops.Any(o =>
            !string.IsNullOrEmpty(o.TargetFile) &&
            Path.GetFullPath(o.TargetFile!) != Path.GetFullPath(filePath));

        if (document != null && !crossFile)
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
                        new[] { op.Method },
                        op.TargetClass,
                        op.TargetFile);
                    results.Add(msg);
                    currentDocument = updatedDoc;
                    RefactoringHelpers.UpdateSolutionCache(updatedDoc);
                }
                else
                {
                    var (msg, updatedDoc) = await MoveMethodsTool.MoveInstanceMethodWithSolution(
                        currentDocument,
                        op.SourceClass,
                        new[] { op.Method },
                        op.TargetClass,
                        op.AccessMember,
                        op.AccessMemberType);
                    results.Add(msg);
                    currentDocument = updatedDoc;
                    RefactoringHelpers.UpdateSolutionCache(updatedDoc);
                }
            }

            RefactoringHelpers.UpdateSolutionCache(currentDocument);

            return string.Join("\n", results);
        }
        else
        {
            // Fallback to AST-based approach for single-file mode or cross-file operations
            return await MoveMultipleMethodsInFile(filePath, ops);
        }
    }

    [McpServerTool, Description("Move multiple methods using explicit parameters. " +
        "Each moved method leaves a delegating wrapper so existing calls continue to compile.")]
    public static async Task<string> MoveMultipleMethods(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file containing the methods")] string filePath,
        [Description("Name of the source class containing the methods")] string sourceClass,
        [Description("Names of the methods to move")] string[] methodNames,
        [Description("Name of the target class")] string targetClass,
        [Description("Name for the access member")] string accessMember,
        [Description("Type of access member (field, property, variable)")] string accessMemberType = "field",
        [Description("Path to the target file (optional)")] string? targetFilePath = null)
    {
        var sourceText = await File.ReadAllTextAsync(filePath);
        var root = await CSharpSyntaxTree.ParseText(sourceText).GetRootAsync();
        var classNode = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == sourceClass);
        if (classNode == null)
            return RefactoringHelpers.ThrowMcpException($"Error: Source class '{sourceClass}' not found");

        var ops = new List<MoveOperation>();
        foreach (var name in methodNames)
        {
            var method = classNode.Members.OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.ValueText == name);
            if (method == null)
                return RefactoringHelpers.ThrowMcpException($"Error: No method named '{name}' found");

            ops.Add(new MoveOperation
            {
                SourceClass = sourceClass,
                Method = name,
                TargetClass = targetClass,
                AccessMember = accessMember,
                AccessMemberType = accessMemberType,
                IsStatic = method.Modifiers.Any(SyntaxKind.StaticKeyword),
                TargetFile = targetFilePath
            });
        }

        return await MoveMultipleMethods(solutionPath, filePath, ops, targetFilePath);
    }
}
