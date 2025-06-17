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
using System.Threading;

public static partial class MoveMethodsTool
{
    // ===== FILE OPERATION LAYER =====
    // File I/O operations that use the AST layer

    public static async Task<string> MoveStaticMethodInFile(
        string filePath,
        string methodName,
        string targetClass,
        string? targetFilePath = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        EnsureNotAlreadyMoved(filePath, methodName);
        ValidateFileExists(filePath);

        var targetPath = targetFilePath ?? Path.Combine(Path.GetDirectoryName(filePath)!, $"{targetClass}.cs");
        var sameFile = targetPath == filePath;

        var (sourceText, sourceEncoding) = await RefactoringHelpers.ReadFileWithEncodingAsync(filePath, cancellationToken);
        var sourceRoot = (await CSharpSyntaxTree.ParseText(sourceText).GetRootAsync(cancellationToken));

        var moveResult = MoveStaticMethodAst(sourceRoot, methodName, targetClass);

        SyntaxNode targetRoot;
        if (sameFile)
        {
            targetRoot = moveResult.NewSourceRoot;
        }
        else
        {
            targetRoot = await LoadOrCreateTargetRoot(targetPath, cancellationToken);
            var nsName = sourceRoot.DescendantNodes()
                .OfType<BaseNamespaceDeclarationSyntax>()
                .FirstOrDefault()?.Name.ToString();
            targetRoot = PropagateUsings(sourceRoot, targetRoot, nsName);
        }

        targetRoot = AddMethodToTargetClass(targetRoot, targetClass, moveResult.MovedMethod, moveResult.Namespace);

        var formattedTarget = Formatter.Format(targetRoot, RefactoringHelpers.SharedWorkspace);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        var targetEncoding = File.Exists(targetPath)
            ? await RefactoringHelpers.GetFileEncodingAsync(targetPath)
            : sourceEncoding;
        await File.WriteAllTextAsync(targetPath, formattedTarget.ToFullString(), targetEncoding, cancellationToken);
        progress?.Report(targetPath);

        if (!sameFile)
        {
            var formattedSource = Formatter.Format(moveResult.NewSourceRoot, RefactoringHelpers.SharedWorkspace);
            await File.WriteAllTextAsync(filePath, formattedSource.ToFullString(), sourceEncoding, cancellationToken);
            progress?.Report(filePath);
        }

        return $"Successfully moved static method '{methodName}' to {targetClass} in {targetPath}. A delegate method remains in the original class to preserve the interface.";
    }



    private static void ValidateFileExists(string filePath)
    {
        if (!File.Exists(filePath))
            throw new McpException($"Error: File {filePath} not found (current dir: {Directory.GetCurrentDirectory()})");
    }

    private static async Task<SyntaxNode> LoadOrCreateTargetRoot(
        string targetPath,
        CancellationToken cancellationToken)
    {
        if (File.Exists(targetPath))
        {
            var (targetText, _) = await RefactoringHelpers.ReadFileWithEncodingAsync(targetPath, cancellationToken);
            return await CSharpSyntaxTree.ParseText(targetText).GetRootAsync(cancellationToken);
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
        string? targetFilePath = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        EnsureNotAlreadyMoved(filePath, methodName);
        ValidateFileExists(filePath);

        var targetPath = targetFilePath ?? filePath;
        var sameFile = targetPath == filePath;

        var (sourceText, sourceEncoding) = await RefactoringHelpers.ReadFileWithEncodingAsync(filePath, cancellationToken);
        var sourceRoot = (await CSharpSyntaxTree.ParseText(sourceText).GetRootAsync(cancellationToken));

        var moveResult = MoveInstanceMethodAst(
            sourceRoot, sourceClass, methodName, targetClass, accessMemberName, accessMemberType);

        if (sameFile)
        {
            var targetRoot = AddMethodToTargetClass(moveResult.NewSourceRoot, targetClass, moveResult.MovedMethod, moveResult.Namespace);
            var formatted = Formatter.Format(targetRoot, RefactoringHelpers.SharedWorkspace);
            var targetEncoding = File.Exists(targetPath)
                ? await RefactoringHelpers.GetFileEncodingAsync(targetPath, cancellationToken)
                : sourceEncoding;
            await File.WriteAllTextAsync(targetPath, formatted.ToFullString(), targetEncoding, cancellationToken);
            progress?.Report(targetPath);
        }
        else
        {
            var formattedSource = Formatter.Format(moveResult.NewSourceRoot, RefactoringHelpers.SharedWorkspace);
            await File.WriteAllTextAsync(filePath, formattedSource.ToFullString(), sourceEncoding, cancellationToken);
            progress?.Report(filePath);

            var targetRoot = await LoadOrCreateTargetRoot(targetPath, cancellationToken);
            var nsName = sourceRoot.DescendantNodes()
                .OfType<BaseNamespaceDeclarationSyntax>()
                .FirstOrDefault()?.Name.ToString();
            targetRoot = PropagateUsings(sourceRoot, targetRoot, nsName);
            targetRoot = AddMethodToTargetClass(targetRoot, targetClass, moveResult.MovedMethod, moveResult.Namespace);

            var formattedTarget = Formatter.Format(targetRoot, RefactoringHelpers.SharedWorkspace);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            var targetEncoding2 = File.Exists(targetPath)
                ? await RefactoringHelpers.GetFileEncodingAsync(targetPath, cancellationToken)
                : sourceEncoding;
            await File.WriteAllTextAsync(targetPath, formattedTarget.ToFullString(), targetEncoding2, cancellationToken);
            progress?.Report(targetPath);
        }

        var locationInfo = targetFilePath != null ? $" in {targetPath}" : string.Empty;
        return $"Successfully moved instance method {sourceClass}.{methodName} to {targetClass}{locationInfo}. A delegate method remains in the original class to preserve the interface.";
    }


}
