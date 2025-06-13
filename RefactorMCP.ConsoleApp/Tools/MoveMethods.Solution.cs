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
    // ===== SOLUTION OPERATION LAYER =====
    // Solution/Document operations that use the AST layer

    internal static async Task<(string Message, Document UpdatedDocument)> MoveInstanceMethodWithSolution(
        Document document,
        string sourceClass,
        string methodName,
        string targetClass,
        string accessMemberName,
        string accessMemberType,
        string? targetFilePath)
    {
        var syntaxRoot = await document.GetSyntaxRootAsync();

        var moveResult = MoveInstanceMethodAst(
            syntaxRoot!,
            sourceClass,
            methodName,
            targetClass,
            accessMemberName,
            accessMemberType);

        var finalRoot = AddMethodToTargetClass(moveResult.NewSourceRoot, targetClass, moveResult.MovedMethod);

        var formatted = Formatter.Format(finalRoot, document.Project.Solution.Workspace);
        var updatedDocument = document.WithSyntaxRoot(formatted);
        var newText = await updatedDocument.GetTextAsync();
        await File.WriteAllTextAsync(updatedDocument.FilePath!, newText.ToString());
        RefactoringHelpers.UpdateSolutionCache(updatedDocument);

        var message = $"Successfully moved {sourceClass}.{methodName} instance method to {targetClass} in {updatedDocument.FilePath}";

        return (message, updatedDocument);
    }

    internal static async Task<(string Message, Document UpdatedDocument)> MoveStaticMethodWithSolution(
        Document document,
        string methodName,
        string targetClass,
        string? targetFilePath)
    {
        var targetPath = targetFilePath ?? Path.Combine(Path.GetDirectoryName(document.FilePath!)!, $"{targetClass}.cs");
        var sameFile = targetPath == document.FilePath;
        var sourceRoot = await document.GetSyntaxRootAsync();

        var context = new FileOperationContext
        {
            SourcePath = document.FilePath!,
            TargetPath = targetPath,
            SameFile = sameFile,
            SourceRoot = sourceRoot!,
            TargetClassName = targetClass
        };

        var moveResult = MoveStaticMethodAst(context.SourceRoot, methodName, targetClass);
        var updatedTargetRoot = await PrepareTargetRoot(context, moveResult.MovedMethod);
        await WriteTransformedFiles(context, moveResult.NewSourceRoot, updatedTargetRoot);

        if (!sameFile)
        {
            RefactoringHelpers.AddDocumentToProject(document.Project, context.TargetPath);
        }

        SyntaxNode newRoot = sameFile
            ? Formatter.Format(updatedTargetRoot, document.Project.Solution.Workspace)
            : Formatter.Format(moveResult.NewSourceRoot, document.Project.Solution.Workspace);

        var updatedDocument = document.WithSyntaxRoot(newRoot);
        RefactoringHelpers.UpdateSolutionCache(updatedDocument);
        var message = $"Successfully moved static method '{methodName}' to {targetClass} in {context.TargetPath}";
        
        return (message, updatedDocument);
    }

}
