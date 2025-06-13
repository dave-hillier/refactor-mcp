using ModelContextProtocol.Server;
using ModelContextProtocol;
using System;
using System.ComponentModel;
using System.Linq;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using System.IO;

public static partial class MoveMethodsTool
{
    // ===== FILE OPERATION LAYER =====
    // File I/O operations that use the AST layer

    public static async Task<string> MoveStaticMethodInFile(
        string filePath,
        string methodName,
        string targetClass,
        string? targetFilePath = null)
    {
        ValidateFileExists(filePath);

        var context = await CreateFileOperationContext(filePath, targetFilePath, targetClass);
        var moveResult = MoveStaticMethodAst(context.SourceRoot, methodName, targetClass);

        var updatedTargetRoot = await PrepareTargetRoot(context, moveResult.MovedMethod);
        await WriteTransformedFiles(context, moveResult.NewSourceRoot, updatedTargetRoot);

        return $"Successfully moved static method '{methodName}' to {targetClass} in {context.TargetPath}";
    }

    private class FileOperationContext
    {
        public string SourcePath { get; set; }
        public string TargetPath { get; set; }
        public bool SameFile { get; set; }
        public SyntaxNode SourceRoot { get; set; }
        public string TargetClassName { get; set; }
    }

    private static void ValidateFileExists(string filePath)
    {
        if (!File.Exists(filePath))
            throw new McpException($"Error: File {filePath} not found (current dir: {Directory.GetCurrentDirectory()})");
    }

    private static async Task<FileOperationContext> CreateFileOperationContext(
        string filePath,
        string? targetFilePath,
        string targetClass)
    {
        var targetPath = targetFilePath ?? Path.Combine(Path.GetDirectoryName(filePath)!, $"{targetClass}.cs");
        var sourceText = await File.ReadAllTextAsync(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var sourceRoot = await syntaxTree.GetRootAsync();

        return new FileOperationContext
        {
            SourcePath = filePath,
            TargetPath = targetPath,
            SameFile = targetPath == filePath,
            SourceRoot = sourceRoot,
            TargetClassName = targetClass
        };
    }

    private static async Task<SyntaxNode> PrepareTargetRoot(FileOperationContext context, MethodDeclarationSyntax methodToMove)
    {
        SyntaxNode targetRoot;

        if (context.SameFile)
        {
            targetRoot = context.SourceRoot;
        }
        else
        {
            targetRoot = await LoadOrCreateTargetRoot(context.TargetPath);
            targetRoot = PropagateUsings(context.SourceRoot, targetRoot);
        }

        return AddMethodToTargetClass(targetRoot, context.TargetClassName, methodToMove);
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

    private static async Task WriteTransformedFiles(
        FileOperationContext context,
        SyntaxNode newSourceRoot,
        SyntaxNode targetRoot)
    {
        var formattedTarget = Formatter.Format(targetRoot, RefactoringHelpers.SharedWorkspace);

        if (!context.SameFile)
        {
            var formattedSource = Formatter.Format(newSourceRoot, RefactoringHelpers.SharedWorkspace);
            await File.WriteAllTextAsync(context.SourcePath, formattedSource.ToFullString());
        }

        Directory.CreateDirectory(Path.GetDirectoryName(context.TargetPath)!);
        await File.WriteAllTextAsync(context.TargetPath, formattedTarget.ToFullString());
    }

    public static async Task<string> MoveInstanceMethodInFile(
        string filePath,
        string sourceClass,
        string methodName,
        string targetClass,
        string accessMemberName,
        string accessMemberType,
        string? targetFilePath = null)
    {
        ValidateFileExists(filePath);

        var context = await CreateInstanceFileOperationContext(
            filePath, targetFilePath ?? filePath, targetClass, sourceClass, methodName, accessMemberName, accessMemberType);

        var moveResult = MoveInstanceMethodAst(
            context.SourceRoot, sourceClass, methodName, targetClass, accessMemberName, accessMemberType);

        await ProcessInstanceMethodFileOperations(context, moveResult);

        return BuildInstanceMethodSuccessMessage(context, sourceClass, methodName, targetClass, targetFilePath);
    }

    private class InstanceFileOperationContext : FileOperationContext
    {
        public string SourceClassName { get; set; }
        public string MethodName { get; set; }
        public string AccessMemberName { get; set; }
        public string AccessMemberType { get; set; }
    }

    private static async Task<InstanceFileOperationContext> CreateInstanceFileOperationContext(
        string filePath,
        string targetPath,
        string targetClass,
        string sourceClass,
        string methodName,
        string accessMemberName,
        string accessMemberType)
    {
        var sourceText = await File.ReadAllTextAsync(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var sourceRoot = await syntaxTree.GetRootAsync();

        return new InstanceFileOperationContext
        {
            SourcePath = filePath,
            TargetPath = targetPath,
            SameFile = targetPath == filePath,
            SourceRoot = sourceRoot,
            TargetClassName = targetClass,
            SourceClassName = sourceClass,
            MethodName = methodName,
            AccessMemberName = accessMemberName,
            AccessMemberType = accessMemberType
        };
    }

    private static async Task ProcessInstanceMethodFileOperations(
        InstanceFileOperationContext context,
        MoveInstanceMethodResult moveResult)
    {
        if (context.SameFile)
        {
            await ProcessSameFileInstanceMove(context, moveResult);
        }
        else
        {
            await ProcessCrossFileInstanceMove(context, moveResult);
        }
    }

    private static async Task ProcessSameFileInstanceMove(
        InstanceFileOperationContext context,
        MoveInstanceMethodResult moveResult)
    {
        var targetRoot = AddMethodToTargetClass(moveResult.NewSourceRoot, context.TargetClassName, moveResult.MovedMethod);
        var formatted = Formatter.Format(targetRoot, RefactoringHelpers.SharedWorkspace);
        await File.WriteAllTextAsync(context.TargetPath, formatted.ToFullString());
    }

    private static async Task ProcessCrossFileInstanceMove(
        InstanceFileOperationContext context,
        MoveInstanceMethodResult moveResult)
    {
        // Handle source file
        var formattedSource = Formatter.Format(moveResult.NewSourceRoot, RefactoringHelpers.SharedWorkspace);
        await File.WriteAllTextAsync(context.SourcePath, formattedSource.ToFullString());

        // Handle target file
        var targetRoot = await LoadOrCreateTargetRoot(context.TargetPath);
        targetRoot = PropagateUsings(context.SourceRoot, targetRoot);
        targetRoot = AddMethodToTargetClass(targetRoot, context.TargetClassName, moveResult.MovedMethod);

        var formattedTarget = Formatter.Format(targetRoot, RefactoringHelpers.SharedWorkspace);
        Directory.CreateDirectory(Path.GetDirectoryName(context.TargetPath)!);
        await File.WriteAllTextAsync(context.TargetPath, formattedTarget.ToFullString());
    }

    private static string BuildInstanceMethodSuccessMessage(
        InstanceFileOperationContext context,
        string sourceClass,
        string methodName,
        string targetClass,
        string? targetFilePath)
    {
        var locationInfo = targetFilePath != null ? $" in {context.TargetPath}" : "";
        return $"Successfully moved instance method {sourceClass}.{methodName} to {targetClass}{locationInfo}";
    }
}
