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

    public static SyntaxNode MoveMultipleMethodsAst(
        SyntaxNode sourceRoot, 
        string[] sourceClasses,
        string[] methodNames,
        string[] targetClasses,
        string[] accessMembers,
        string[] accessMemberTypes,
        bool[] isStatic)
    {
        var orderedIndices = OrderOperations(sourceRoot, sourceClasses, methodNames, targetClasses, accessMembers, accessMemberTypes, isStatic);
        var workingRoot = sourceRoot;

        foreach (var idx in orderedIndices)
        {
            if (isStatic[idx])
            {
                var moveResult = MoveMethodsTool.MoveStaticMethodAst(workingRoot, methodNames[idx], targetClasses[idx]);
                // First update the source with the stub, then add the method to target
                workingRoot = moveResult.NewSourceRoot;
                workingRoot = MoveMethodsTool.AddMethodToTargetClass(workingRoot, targetClasses[idx], moveResult.MovedMethod);
            }
            else
            {
                var moveResult = MoveMethodsTool.MoveInstanceMethodAst(
                    workingRoot, 
                    sourceClasses[idx], 
                    methodNames[idx], 
                    targetClasses[idx], 
                    accessMembers[idx], 
                    accessMemberTypes[idx]);
                // First update the source with the stub, then add the method to target
                workingRoot = moveResult.NewSourceRoot;
                workingRoot = MoveMethodsTool.AddMethodToTargetClass(workingRoot, targetClasses[idx], moveResult.MovedMethod);
            }
        }

        return workingRoot;
    }

    // ===== FILE OPERATION LAYER =====
    // File I/O operations that use the AST layer

    public static async Task<string> MoveMultipleMethodsInFile(
        string filePath, 
        string[] sourceClasses,
        string[] methodNames,
        string[] targetClasses,
        string[] accessMembers,
        string[] accessMemberTypes,
        bool[] isStatic,
        string[]? targetFiles = null)
    {
        if (sourceClasses.Length == 0 || methodNames.Length == 0 || targetClasses.Length == 0 || 
            accessMembers.Length == 0 || accessMemberTypes.Length == 0 || isStatic.Length == 0)
            throw new McpException("Error: No operations provided");

        if (sourceClasses.Length != methodNames.Length || methodNames.Length != targetClasses.Length || 
            targetClasses.Length != accessMembers.Length || accessMembers.Length != accessMemberTypes.Length || 
            accessMemberTypes.Length != isStatic.Length)
            throw new McpException("Error: All arrays must have the same length");

        if (!File.Exists(filePath))
            throw new McpException($"Error: File {filePath} not found (current dir: {Directory.GetCurrentDirectory()})");

        var sourceText = await File.ReadAllTextAsync(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var originalSourceRoot = await syntaxTree.GetRootAsync();

        var orderedIndices = OrderOperations(originalSourceRoot, sourceClasses, methodNames, targetClasses, accessMembers, accessMemberTypes, isStatic);
        var workingSourceRoot = originalSourceRoot;
        var targetRoots = new Dictionary<string, SyntaxNode>();

        foreach (var idx in orderedIndices)
        {
            var targetPath = targetFiles?[idx] ?? Path.Combine(Path.GetDirectoryName(filePath)!, $"{targetClasses[idx]}.cs");
            var sameFile = targetPath == filePath;

            if (isStatic[idx])
            {
                var moveResult = MoveMethodsTool.MoveStaticMethodAst(workingSourceRoot, methodNames[idx], targetClasses[idx]);
                workingSourceRoot = moveResult.NewSourceRoot;

                if (sameFile)
                {
                    workingSourceRoot = MoveMethodsTool.AddMethodToTargetClass(workingSourceRoot, targetClasses[idx], moveResult.MovedMethod);
                }
                else
                {
                    if (!targetRoots.TryGetValue(targetPath, out var targetRoot))
                    {
                        targetRoot = await LoadOrCreateTargetRoot(targetPath);
                        targetRoot = MoveMethodsTool.PropagateUsings(originalSourceRoot, targetRoot);
                    }

                    targetRoot = MoveMethodsTool.AddMethodToTargetClass(targetRoot, targetClasses[idx], moveResult.MovedMethod);
                    targetRoots[targetPath] = targetRoot;
                }
            }
            else
            {
                var moveResult = MoveMethodsTool.MoveInstanceMethodAst(
                    workingSourceRoot,
                    sourceClasses[idx],
                    methodNames[idx],
                    targetClasses[idx],
                    accessMembers[idx],
                    accessMemberTypes[idx]);

                workingSourceRoot = moveResult.NewSourceRoot;

                if (sameFile)
                {
                    workingSourceRoot = MoveMethodsTool.AddMethodToTargetClass(workingSourceRoot, targetClasses[idx], moveResult.MovedMethod);
                }
                else
                {
                    if (!targetRoots.TryGetValue(targetPath, out var targetRoot))
                    {
                        targetRoot = await LoadOrCreateTargetRoot(targetPath);
                        targetRoot = MoveMethodsTool.PropagateUsings(originalSourceRoot, targetRoot);
                    }

                    targetRoot = MoveMethodsTool.AddMethodToTargetClass(targetRoot, targetClasses[idx], moveResult.MovedMethod);
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

        return $"Successfully moved {sourceClasses.Length} methods";
    }

    private static async Task<SyntaxNode> LoadOrCreateTargetRoot(string targetPath)
    {
        if (File.Exists(targetPath))
        {
            var targetText = await File.ReadAllTextAsync(targetPath);
            return await CSharpSyntaxTree.ParseText(targetText).GetRootAsync();
        }
        else
        {
            return SyntaxFactory.CompilationUnit();
        }
    }
}
