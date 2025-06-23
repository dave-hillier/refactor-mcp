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

namespace RefactorMCP.ConsoleApp.Move;

public static class MoveMethodFileService
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
        MoveMethodTool.EnsureNotAlreadyMoved(filePath, methodName);
        ValidateFileExists(filePath);

        var targetPath = targetFilePath ?? Path.Combine(Path.GetDirectoryName(filePath)!, $"{targetClass}.cs");
        var sameFile = targetPath == filePath;

        var (sourceText, sourceEncoding) = await RefactoringHelpers.ReadFileWithEncodingAsync(filePath, cancellationToken);
        var sourceRoot = (await CSharpSyntaxTree.ParseText(sourceText).GetRootAsync(cancellationToken));

        var moveResult = MoveMethodAst.MoveStaticMethodAst(sourceRoot, methodName, targetClass);

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
            targetRoot = MoveMethodAst.PropagateUsings(sourceRoot, targetRoot, nsName);
        }

        targetRoot = MoveMethodAst.AddMethodToTargetClass(targetRoot, targetClass, moveResult.MovedMethod, moveResult.Namespace);

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



    internal static void ValidateFileExists(string filePath)
    {
        if (!File.Exists(filePath))
            throw new McpException($"Error: File {filePath} not found (current dir: {Directory.GetCurrentDirectory()})");
    }

    internal static async Task<SyntaxNode> LoadOrCreateTargetRoot(
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
        string[] constructorInjections,
        string[] parameterInjections,
        string targetClass,
        string accessMemberName,
        string accessMemberType,
        string? targetFilePath = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        MoveMethodTool.EnsureNotAlreadyMoved(filePath, methodName);
        ValidateFileExists(filePath);

        var targetPath = targetFilePath ?? filePath;
        var sameFile = targetPath == filePath;

        var (sourceText, sourceEncoding) = await RefactoringHelpers.ReadFileWithEncodingAsync(filePath, cancellationToken);
        var sourceRoot = (await CSharpSyntaxTree.ParseText(sourceText).GetRootAsync(cancellationToken));

        var moveResult = MoveMethodAst.MoveInstanceMethodAst(
            sourceRoot,
            sourceClass,
            methodName,
            targetClass,
            accessMemberName,
            accessMemberType,
            parameterInjections);


        SyntaxNode updatedSourceRoot = moveResult.NewSourceRoot;
        SyntaxNode updatedTargetRoot;

        if (sameFile)
        {
            updatedTargetRoot = MoveMethodAst.AddMethodToTargetClass(updatedSourceRoot, targetClass, moveResult.MovedMethod, moveResult.Namespace);
            updatedTargetRoot = ApplyConstructorInjection(updatedTargetRoot, methodName, constructorInjections, sourceClass);
            var formatted = Formatter.Format(updatedTargetRoot, RefactoringHelpers.SharedWorkspace);
            var targetEncoding = File.Exists(targetPath)
                ? await RefactoringHelpers.GetFileEncodingAsync(targetPath, cancellationToken)
                : sourceEncoding;
            await File.WriteAllTextAsync(targetPath, formatted.ToFullString(), targetEncoding, cancellationToken);
            progress?.Report(targetPath);
        }
        else
        {
            updatedSourceRoot = ApplyConstructorInjection(updatedSourceRoot, methodName, constructorInjections, sourceClass);
            var formattedSource = Formatter.Format(updatedSourceRoot, RefactoringHelpers.SharedWorkspace);
            await File.WriteAllTextAsync(filePath, formattedSource.ToFullString(), sourceEncoding, cancellationToken);
            progress?.Report(filePath);

            updatedTargetRoot = await LoadOrCreateTargetRoot(targetPath, cancellationToken);
            var nsName = sourceRoot.DescendantNodes()
                .OfType<BaseNamespaceDeclarationSyntax>()
                .FirstOrDefault()?.Name.ToString();
            updatedTargetRoot = MoveMethodAst.PropagateUsings(sourceRoot, updatedTargetRoot, nsName);
            updatedTargetRoot = MoveMethodAst.AddMethodToTargetClass(updatedTargetRoot, targetClass, moveResult.MovedMethod, moveResult.Namespace);
            updatedTargetRoot = ApplyConstructorInjection(updatedTargetRoot, methodName, constructorInjections, sourceClass);

            var formattedTarget = Formatter.Format(updatedTargetRoot, RefactoringHelpers.SharedWorkspace);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            var targetEncoding2 = File.Exists(targetPath)
                ? await RefactoringHelpers.GetFileEncodingAsync(targetPath, cancellationToken)
                : sourceEncoding;
            await File.WriteAllTextAsync(targetPath, formattedTarget.ToFullString(), targetEncoding2, cancellationToken);
            progress?.Report(targetPath);
        }

        var locationInfo = targetFilePath != null ? $" in {targetPath}" : string.Empty;
        var staticHint = moveResult.MovedMethod.Modifiers.Any(SyntaxKind.StaticKeyword)
            ? " It was made static."
            : string.Empty;
        return $"Successfully moved instance method {sourceClass}.{methodName} to {targetClass}{locationInfo}. A delegate method remains in the original class to preserve the interface.{staticHint}";
    }

    private static SyntaxNode ApplyConstructorInjection(
        SyntaxNode root,
        string methodName,
        IEnumerable<string> constructorInjections,
        string sourceClass)
    {
        foreach (var inj in constructorInjections)
        {
            var paramName = GetParameterName(inj, sourceClass);
            var fieldName = GetFieldName(inj, sourceClass);
            var method = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.ValueText == methodName);
            if (method == null)
                continue;
            var parameter = method.ParameterList.Parameters
                .FirstOrDefault(p => p.Identifier.ValueText == paramName);
            if (parameter == null)
                continue;
            var index = method.ParameterList.Parameters.IndexOf(parameter);
            var type = parameter.Type ?? SyntaxFactory.ParseTypeName("object");

            var rewriter = new ConstructorInjectionRewriter(
                methodName,
                paramName,
                index,
                type,
                fieldName,
                false);

            root = rewriter.Visit(root)!;
        }

        return root;
    }

    private static string GetFieldName(string inj, string sourceClass)
    {
        string baseName;
        if (inj == "this")
        {
            baseName = sourceClass;
            if (baseName.Length >= 2 && baseName[0] == 'c' && char.IsUpper(baseName[1]))
                baseName = baseName.Substring(1);
        }
        else
        {
            baseName = inj.TrimStart('_');
        }

        if (baseName.StartsWith("@"))
            baseName = baseName.Substring(1);

        if (baseName.Length > 0)
            baseName = char.ToLower(baseName[0]) + baseName.Substring(1);

        return "_" + baseName;
    }

    internal static string GetParameterName(string inj, string sourceClass)
    {
        string baseName;
        if (inj == "this")
        {
            baseName = sourceClass;
            if (baseName.Length >= 2 && baseName[0] == 'c' && char.IsUpper(baseName[1]))
                baseName = baseName.Substring(1);
        }
        else
        {
            baseName = inj.TrimStart('_');
        }

        if (baseName.StartsWith("@"))
            baseName = baseName.Substring(1);

        if (baseName.Length > 0)
            baseName = char.ToLower(baseName[0]) + baseName.Substring(1);

        return baseName;
    }


}
