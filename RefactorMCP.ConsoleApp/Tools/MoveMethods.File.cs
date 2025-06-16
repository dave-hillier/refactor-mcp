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

        var targetPath = targetFilePath ?? Path.Combine(Path.GetDirectoryName(filePath)!, $"{targetClass}.cs");
        var sameFile = targetPath == filePath;

        var (sourceText, sourceEncoding) = await RefactoringHelpers.ReadFileWithEncodingAsync(filePath);
        var sourceRoot = (await CSharpSyntaxTree.ParseText(sourceText).GetRootAsync());

        var moveResult = MoveStaticMethodAst(sourceRoot, methodName, targetClass);

        SyntaxNode targetRoot;
        if (sameFile)
        {
            targetRoot = moveResult.NewSourceRoot;
        }
        else
        {
            targetRoot = await LoadOrCreateTargetRoot(targetPath);
            targetRoot = PropagateUsings(sourceRoot, targetRoot);
        }

        targetRoot = AddMethodToTargetClass(targetRoot, targetClass, moveResult.MovedMethod, moveResult.Namespace);

        var formattedTarget = Formatter.Format(targetRoot, RefactoringHelpers.SharedWorkspace);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        var targetEncoding = File.Exists(targetPath)
            ? await RefactoringHelpers.GetFileEncodingAsync(targetPath)
            : sourceEncoding;
        await File.WriteAllTextAsync(targetPath, formattedTarget.ToFullString(), targetEncoding);

        if (!sameFile)
        {
            var formattedSource = Formatter.Format(moveResult.NewSourceRoot, RefactoringHelpers.SharedWorkspace);
            await File.WriteAllTextAsync(filePath, formattedSource.ToFullString(), sourceEncoding);
        }

        return $"Successfully moved static method '{methodName}' to {targetClass} in {targetPath}";
    }



    private static void ValidateFileExists(string filePath)
    {
        if (!File.Exists(filePath))
            throw new McpException($"Error: File {filePath} not found (current dir: {Directory.GetCurrentDirectory()})");
    }

    private static async Task<SyntaxNode> LoadOrCreateTargetRoot(string targetPath)
    {
        if (File.Exists(targetPath))
        {
            var (targetText, _) = await RefactoringHelpers.ReadFileWithEncodingAsync(targetPath);
            return await CSharpSyntaxTree.ParseText(targetText).GetRootAsync();
        }
        else
        {
            return SyntaxFactory.CompilationUnit();
        }
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

        var targetPath = targetFilePath ?? filePath;
        var sameFile = targetPath == filePath;

        var (sourceText, sourceEncoding) = await RefactoringHelpers.ReadFileWithEncodingAsync(filePath);
        var sourceRoot = (await CSharpSyntaxTree.ParseText(sourceText).GetRootAsync());

        var moveResult = MoveInstanceMethodAst(
            sourceRoot, sourceClass, methodName, targetClass, accessMemberName, accessMemberType);

        if (sameFile)
        {
            var targetRoot = AddMethodToTargetClass(moveResult.NewSourceRoot, targetClass, moveResult.MovedMethod, moveResult.Namespace, failIfStatic: true);
            var formatted = Formatter.Format(targetRoot, RefactoringHelpers.SharedWorkspace);
            var targetEncoding = File.Exists(targetPath)
                ? await RefactoringHelpers.GetFileEncodingAsync(targetPath)
                : sourceEncoding;
            await File.WriteAllTextAsync(targetPath, formatted.ToFullString(), targetEncoding);
        }
        else
        {
            var formattedSource = Formatter.Format(moveResult.NewSourceRoot, RefactoringHelpers.SharedWorkspace);
            await File.WriteAllTextAsync(filePath, formattedSource.ToFullString(), sourceEncoding);

            var targetRoot = await LoadOrCreateTargetRoot(targetPath);
            targetRoot = PropagateUsings(sourceRoot, targetRoot);
            targetRoot = AddMethodToTargetClass(targetRoot, targetClass, moveResult.MovedMethod, moveResult.Namespace, failIfStatic: true);

            var formattedTarget = Formatter.Format(targetRoot, RefactoringHelpers.SharedWorkspace);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            var targetEncoding2 = File.Exists(targetPath)
                ? await RefactoringHelpers.GetFileEncodingAsync(targetPath)
                : sourceEncoding;
            await File.WriteAllTextAsync(targetPath, formattedTarget.ToFullString(), targetEncoding2);
        }

        var locationInfo = targetFilePath != null ? $" in {targetPath}" : string.Empty;
        return $"Successfully moved instance method {sourceClass}.{methodName} to {targetClass}{locationInfo}";
    }


}
