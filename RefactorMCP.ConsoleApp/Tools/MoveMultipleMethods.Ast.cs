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

public static partial class MoveMultipleMethodsTool
{
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

    public static async Task<string> MoveMultipleMethodsInFile(string filePath, IEnumerable<MoveOperation> operations)
    {
        var ops = operations.ToList();
        if (ops.Count == 0)
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
}
