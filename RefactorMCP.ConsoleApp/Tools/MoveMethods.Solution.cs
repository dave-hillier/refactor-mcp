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
        string[] methodNames,
        string targetClass,
        string accessMemberName,
        string accessMemberType)
    {
        var messages = new List<string>();
        var currentDocument = document;

        foreach (var methodName in methodNames)
        {
            var syntaxRoot = await currentDocument.GetSyntaxRootAsync();

            var moveResult = MoveInstanceMethodAst(
                syntaxRoot!,
                sourceClass,
                methodName,
                targetClass,
                accessMemberName,
                accessMemberType);

            var finalRoot = AddMethodToTargetClass(moveResult.NewSourceRoot, targetClass, moveResult.MovedMethod);

            var formatted = Formatter.Format(finalRoot, currentDocument.Project.Solution.Workspace);
            currentDocument = currentDocument.WithSyntaxRoot(formatted);
            var newText = await currentDocument.GetTextAsync();
            await File.WriteAllTextAsync(currentDocument.FilePath!, newText.ToString());
            RefactoringHelpers.UpdateSolutionCache(currentDocument);

            messages.Add($"Successfully moved {sourceClass}.{methodName} instance method to {targetClass} in {currentDocument.FilePath}");
        }

        return (string.Join("\n", messages), currentDocument);
    }

    internal static async Task<(string Message, Document UpdatedDocument)> MoveStaticMethodWithSolution(
        Document document,
        string[] methodNames,
        string targetClass,
        string? targetFilePath)
    {
        var messages = new List<string>();
        var currentDocument = document;

        foreach (var methodName in methodNames)
        {
            var targetPath = targetFilePath ?? Path.Combine(Path.GetDirectoryName(currentDocument.FilePath!)!, $"{targetClass}.cs");
            var sameFile = targetPath == currentDocument.FilePath;
            var sourceRoot = await currentDocument.GetSyntaxRootAsync();

            var context = new FileOperationContext
            {
                SourcePath = currentDocument.FilePath!,
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
                RefactoringHelpers.AddDocumentToProject(currentDocument.Project, context.TargetPath);
            }

            SyntaxNode newRoot = sameFile
                ? Formatter.Format(updatedTargetRoot, currentDocument.Project.Solution.Workspace)
                : Formatter.Format(moveResult.NewSourceRoot, currentDocument.Project.Solution.Workspace);

            currentDocument = currentDocument.WithSyntaxRoot(newRoot);
            RefactoringHelpers.UpdateSolutionCache(currentDocument);
            messages.Add($"Successfully moved static method '{methodName}' to {targetClass} in {context.TargetPath}");
        }

        return (string.Join("\n", messages), currentDocument);
    }

}
