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
    // Solution/Document operations that use the AST layer

    [McpServerTool, Description("Move multiple methods to target classes, automatically ordering by dependencies. " +
        "Wrapper methods remain at the original locations to delegate to the moved implementations.")]
    public static async Task<string> MoveMultipleMethods(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file containing the methods")] string filePath,
        [Description("Source class names")] string[] sourceClasses,
        [Description("Method names to move")] string[] methodNames,
        [Description("Target class names")] string[] targetClasses,
        [Description("Access member names")] string[] accessMembers,
        [Description("Access member types (field, property, variable)")] string[] accessMemberTypes,
        [Description("Whether methods are static")] bool[] isStatic,
        [Description("Target file paths (optional)")] string[]? targetFiles = null,
        [Description("Default target file path used when operations omit targetFile (optional)")] string? defaultTargetFilePath = null)
    {
        if (sourceClasses.Length == 0 || methodNames.Length == 0 || targetClasses.Length == 0 || 
            accessMembers.Length == 0 || accessMemberTypes.Length == 0 || isStatic.Length == 0)
            return RefactoringHelpers.ThrowMcpException("Error: No operations provided");

        if (sourceClasses.Length != methodNames.Length || methodNames.Length != targetClasses.Length || 
            targetClasses.Length != accessMembers.Length || accessMembers.Length != accessMemberTypes.Length || 
            accessMemberTypes.Length != isStatic.Length)
            return RefactoringHelpers.ThrowMcpException("Error: All arrays must have the same length");

        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath);
        var document = RefactoringHelpers.GetDocumentByPath(solution, filePath);

        var crossFile = targetFiles != null && targetFiles.Any(t => 
            !string.IsNullOrEmpty(t) && Path.GetFullPath(t) != Path.GetFullPath(filePath));

        if (document != null && !crossFile)
        {
            // Solution-based: need to manage document state between operations
            var sourceText = await File.ReadAllTextAsync(filePath);
            var results = new List<string>();
            var orderedIndices = OrderOperations(CSharpSyntaxTree.ParseText(sourceText).GetRoot(), 
                sourceClasses, methodNames, targetClasses, accessMembers, accessMemberTypes, isStatic);

            var currentDocument = document;
            for (int i = 0; i < orderedIndices.Count; i++)
            {
                var idx = orderedIndices[i];
                if (isStatic[idx])
                {
                    var (msg, updatedDoc) = await MoveMethodsTool.MoveStaticMethodWithSolution(
                        currentDocument,
                        new[] { methodNames[idx] },
                        targetClasses[idx],
                        targetFiles?[idx]);
                    results.Add(msg);
                    currentDocument = updatedDoc;
                    RefactoringHelpers.UpdateSolutionCache(updatedDoc);
                }
                else
                {
                    var (msg, updatedDoc) = await MoveMethodsTool.MoveInstanceMethodWithSolution(
                        currentDocument,
                        sourceClasses[idx],
                        new[] { methodNames[idx] },
                        targetClasses[idx],
                        accessMembers[idx],
                        accessMemberTypes[idx]);
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
            return await MoveMultipleMethodsInFile(filePath, sourceClasses, methodNames, targetClasses, 
                accessMembers, accessMemberTypes, isStatic, targetFiles);
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

        var sourceClasses = new string[methodNames.Length];
        var targetClasses = new string[methodNames.Length];
        var accessMembers = new string[methodNames.Length];
        var accessMemberTypes = new string[methodNames.Length];
        var isStatic = new bool[methodNames.Length];
        var targetFiles = new string[methodNames.Length];

        for (int i = 0; i < methodNames.Length; i++)
        {
            var method = classNode.Members.OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.ValueText == methodNames[i]);
            if (method == null)
                return RefactoringHelpers.ThrowMcpException($"Error: No method named '{methodNames[i]}' found");

            sourceClasses[i] = sourceClass;
            targetClasses[i] = targetClass;
            accessMembers[i] = accessMember;
            accessMemberTypes[i] = accessMemberType;
            isStatic[i] = method.Modifiers.Any(SyntaxKind.StaticKeyword);
            targetFiles[i] = targetFilePath;
        }

        return await MoveMultipleMethods(solutionPath, filePath, sourceClasses, methodNames, targetClasses,
            accessMembers, accessMemberTypes, isStatic, targetFiles, targetFilePath);
    }
}
