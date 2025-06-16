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
using System.Text;

[McpServerToolType]
public static partial class MoveMethodsTool
{
    [McpServerTool, Description("Move a static method to another class (preferred for large C# file refactoring). " +
        "Leaves a delegating method in the original class to preserve the interface.")]
    public static async Task<string> MoveStaticMethod(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file containing the method")] string filePath,
        [Description("Name of the static method to move")] string methodName,
        [Description("Name of the target class")] string targetClass,
        [Description("Path to the target file (optional, will create if doesn't exist)")] string? targetFilePath = null)
    {
        try
        {
            ValidateFileExists(filePath);

            var moveContext = await PrepareStaticMethodMove(filePath, targetFilePath, targetClass);
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath);
            var duplicateDoc = await RefactoringHelpers.FindClassInSolution(
                solution,
                targetClass,
                filePath,
                moveContext.TargetPath);
            if (duplicateDoc != null)
                return RefactoringHelpers.ThrowMcpException($"Error: Class {targetClass} already exists in {duplicateDoc.FilePath}");
            var method = ExtractStaticMethodFromSource(moveContext.SourceRoot, methodName);
            var updatedSources = await UpdateSourceAndTargetForStaticMove(moveContext, method);
            await WriteStaticMethodMoveResults(moveContext, updatedSources);

            return $"Successfully moved static method '{methodName}' to {targetClass} in {moveContext.TargetPath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error moving static method: {ex.Message}", ex);
        }
    }

    private class StaticMethodMoveContext
    {
        public string SourcePath { get; set; }
        public string TargetPath { get; set; }
        public bool SameFile { get; set; }
        public SyntaxNode SourceRoot { get; set; }
        public List<UsingDirectiveSyntax> SourceUsings { get; set; }
        public string TargetClassName { get; set; }
        public Encoding SourceEncoding { get; set; }
        public string? Namespace { get; set; }
    }

    private class SourceAndTargetRoots
    {
        public SyntaxNode UpdatedSourceRoot { get; set; }
        public SyntaxNode UpdatedTargetRoot { get; set; }
    }

    private static async Task<StaticMethodMoveContext> PrepareStaticMethodMove(
        string filePath,
        string? targetFilePath,
        string targetClass)
    {
        var (sourceText, sourceEncoding) = await RefactoringHelpers.ReadFileWithEncodingAsync(filePath);
        var targetPath = targetFilePath ?? Path.Combine(Path.GetDirectoryName(filePath)!, $"{targetClass}.cs");

        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = await syntaxTree.GetRootAsync();
        var sourceUsings = syntaxRoot.DescendantNodes().OfType<UsingDirectiveSyntax>().ToList();
        var ns = syntaxRoot.DescendantNodes()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault()?.Name.ToString();

        return new StaticMethodMoveContext
        {
            SourcePath = filePath,
            TargetPath = targetPath,
            SameFile = targetPath == filePath,
            SourceRoot = syntaxRoot,
            SourceUsings = sourceUsings,
            TargetClassName = targetClass,
            SourceEncoding = sourceEncoding,
            Namespace = ns
        };
    }

    private static MethodDeclarationSyntax ExtractStaticMethodFromSource(SyntaxNode sourceRoot, string methodName)
    {
        var method = sourceRoot.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName &&
                                m.Modifiers.Any(SyntaxKind.StaticKeyword));

        if (method == null)
            throw new McpException($"Error: Static method '{methodName}' not found");

        return method;
    }

    private static async Task<SourceAndTargetRoots> UpdateSourceAndTargetForStaticMove(
        StaticMethodMoveContext context,
        MethodDeclarationSyntax method)
    {
        var newSourceRoot = context.SourceRoot.RemoveNode(method, SyntaxRemoveOptions.KeepNoTrivia);
        var targetRoot = await PrepareTargetRootForStaticMove(context);
        var updatedTargetRoot = AddMethodToTargetClass(targetRoot, context.TargetClassName, method, context.Namespace);

        return new SourceAndTargetRoots
        {
            UpdatedSourceRoot = newSourceRoot!,
            UpdatedTargetRoot = updatedTargetRoot
        };
    }

    private static async Task<SyntaxNode> PrepareTargetRootForStaticMove(StaticMethodMoveContext context)
    {
        SyntaxNode targetRoot;

        if (context.SameFile)
        {
            targetRoot = context.SourceRoot;
        }
        else if (File.Exists(context.TargetPath))
        {
            var (targetText, _) = await RefactoringHelpers.ReadFileWithEncodingAsync(context.TargetPath);
            targetRoot = CSharpSyntaxTree.ParseText(targetText).GetRoot();
        }
        else
        {
            targetRoot = SyntaxFactory.CompilationUnit();
        }

        return PropagateUsingsToTarget(context, targetRoot);
    }

    private static SyntaxNode PropagateUsingsToTarget(StaticMethodMoveContext context, SyntaxNode targetRoot)
    {
        var targetCompilationUnit = (CompilationUnitSyntax)targetRoot;
        var targetUsingNames = targetCompilationUnit.Usings
            .Select(u => u.Name.ToString())
            .ToHashSet();

        var missingUsings = context.SourceUsings
            .Where(u => !targetUsingNames.Contains(u.Name.ToString()))
            .ToArray();

        if (missingUsings.Length > 0)
        {
            targetCompilationUnit = targetCompilationUnit.AddUsings(missingUsings);
            return targetCompilationUnit;
        }

        return targetRoot;
    }

    private static async Task WriteStaticMethodMoveResults(
        StaticMethodMoveContext context,
        SourceAndTargetRoots updatedRoots)
    {
        var formattedTarget = Formatter.Format(updatedRoots.UpdatedTargetRoot, RefactoringHelpers.SharedWorkspace);

        if (!context.SameFile)
        {
            var formattedSource = Formatter.Format(updatedRoots.UpdatedSourceRoot, RefactoringHelpers.SharedWorkspace);
            await File.WriteAllTextAsync(context.SourcePath, formattedSource.ToFullString(), context.SourceEncoding);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(context.TargetPath)!);
        var targetEncoding = File.Exists(context.TargetPath)
            ? await RefactoringHelpers.GetFileEncodingAsync(context.TargetPath)
            : context.SourceEncoding;
        await File.WriteAllTextAsync(context.TargetPath, formattedTarget.ToFullString(), targetEncoding);
    }

    [McpServerTool, Description("Move one or more instance methods to another class (preferred for large C# file refactoring). " +
        "Each original method is replaced with a wrapper that calls the moved version to maintain the public API.")]
    public static async Task<string> MoveInstanceMethod(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file containing the method")] string filePath,
        [Description("Name of the source class containing the method")] string sourceClass,
        [Description("Comma separated names of the methods to move")] string methodNames,
        [Description("Name of the target class")] string targetClass,
        [Description("Name for the access member")] string accessMemberName,
        [Description("Type of access member (field, property, variable)")] string accessMemberType = "field",
        [Description("Path to the target file (optional, will create if doesn't exist)")] string? targetFilePath = null)
    {
        try
        {
            var methodList = methodNames.Split(',').Select(m => m.Trim()).Where(m => m.Length > 0).ToArray();
            if (methodList.Length == 0)
                return RefactoringHelpers.ThrowMcpException("Error: No method names provided");

            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath);
            var document = RefactoringHelpers.GetDocumentByPath(solution, filePath);

            var duplicateDoc = await RefactoringHelpers.FindClassInSolution(
                solution,
                targetClass,
                filePath,
                targetFilePath ?? Path.Combine(Path.GetDirectoryName(filePath)!, $"{targetClass}.cs"));
            if (duplicateDoc != null)
                return RefactoringHelpers.ThrowMcpException($"Error: Class {targetClass} already exists in {duplicateDoc.FilePath}");

            if (document != null)
            {
                var (msg, _) = await MoveInstanceMethodWithSolution(
                    document,
                    sourceClass,
                    methodList,
                    targetClass,
                    accessMemberName,
                    accessMemberType,
                    targetFilePath);
                return msg;
            }
            else
            {
                // For single-file operations, use the bulk move method for better efficiency
                if (methodList.Length == 1)
                {
                    return await MoveInstanceMethodInFile(filePath, sourceClass, methodList[0], targetClass, accessMemberName, accessMemberType, targetFilePath);
                }
                else
                {
                    return await MoveBulkInstanceMethodsInFile(filePath, sourceClass, methodList, targetClass, accessMemberName, accessMemberType, targetFilePath);
                }
            }
        }
        catch (Exception ex)
        {
            throw new McpException($"Error moving instance method: {ex.Message}", ex);
        }
    }

    private static async Task<string> MoveBulkInstanceMethodsInFile(string filePath, string sourceClass, string[] methodNames, string targetClass, string accessMemberName, string accessMemberType, string? targetFilePath)
    {
        if (!File.Exists(filePath))
            return RefactoringHelpers.ThrowMcpException($"Error: File {filePath} not found (current dir: {Directory.GetCurrentDirectory()})");

        var targetPath = targetFilePath ?? filePath;
        var sameFile = targetPath == filePath;

        var (sourceText, sourceEncoding) = await RefactoringHelpers.ReadFileWithEncodingAsync(filePath);

        if (sameFile)
        {
            // Same file operation - use multiple individual AST transformations
            var tree = CSharpSyntaxTree.ParseText(sourceText);
            var root = await tree.GetRootAsync();

            foreach (var methodName in methodNames)
            {
                var moveResult = MoveInstanceMethodAst(root, sourceClass, methodName, targetClass, accessMemberName, accessMemberType);
                root = AddMethodToTargetClass(moveResult.NewSourceRoot, targetClass, moveResult.MovedMethod, moveResult.Namespace, failIfStatic: true);
            }

            var formatted = Formatter.Format(root, RefactoringHelpers.SharedWorkspace);
            await File.WriteAllTextAsync(filePath, formatted.ToFullString(), sourceEncoding);
            return $"Successfully moved {methodNames.Length} methods from {sourceClass} to {targetClass} in {filePath}";
        }
        else
        {
            // Cross-file operation - update both files in memory and write once
            var sourceTree = CSharpSyntaxTree.ParseText(sourceText);
            var sourceRoot = await sourceTree.GetRootAsync();

            var targetRoot = await LoadOrCreateTargetRoot(targetPath);
            targetRoot = PropagateUsings(sourceRoot, targetRoot);

            foreach (var methodName in methodNames)
            {
                var moveResult = MoveInstanceMethodAst(sourceRoot, sourceClass, methodName, targetClass, accessMemberName, accessMemberType);
                sourceRoot = moveResult.NewSourceRoot;
                targetRoot = AddMethodToTargetClass(targetRoot, targetClass, moveResult.MovedMethod, moveResult.Namespace, failIfStatic: true);
            }

            var formattedSource = Formatter.Format(sourceRoot, RefactoringHelpers.SharedWorkspace);
            await File.WriteAllTextAsync(filePath, formattedSource.ToFullString(), sourceEncoding);

            var formattedTarget = Formatter.Format(targetRoot, RefactoringHelpers.SharedWorkspace);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            var targetEncoding = File.Exists(targetPath)
                ? await RefactoringHelpers.GetFileEncodingAsync(targetPath)
                : sourceEncoding;
            await File.WriteAllTextAsync(targetPath, formattedTarget.ToFullString(), targetEncoding);

            return $"Successfully moved {methodNames.Length} methods from {sourceClass} to {targetClass} in {targetPath}";
        }
    }

    public static async Task<(string, Document)> MoveInstanceMethodWithSolution(
        Document document,
        string sourceClassName,
        string[] methodNames,
        string targetClassName,
        string accessMemberName,
        string accessMemberType,
        string? targetFilePath = null)
    {
        var messages = new List<string>();
        var currentDocument = document;

        foreach (var methodName in methodNames)
        {

            var targetPath = targetFilePath ?? currentDocument.FilePath!;
            var sameFile = targetPath == currentDocument.FilePath;

            var message = await MoveInstanceMethodInFile(
                currentDocument.FilePath!,
                sourceClassName,
                methodName,
                targetClassName,
                accessMemberName,
                accessMemberType,
                targetFilePath);

            if (sameFile)
            {
                var (newText, _) = await RefactoringHelpers.ReadFileWithEncodingAsync(targetPath);
                var newRoot = await CSharpSyntaxTree.ParseText(newText).GetRootAsync();
                currentDocument = document.Project.Solution.WithDocumentSyntaxRoot(currentDocument.Id, newRoot).GetDocument(currentDocument.Id);
            }
            else
            {
                var (newSourceText, _) = await RefactoringHelpers.ReadFileWithEncodingAsync(currentDocument.FilePath!);
                var newSourceRoot = await CSharpSyntaxTree.ParseText(newSourceText).GetRootAsync();
                var solution = document.Project.Solution.WithDocumentSyntaxRoot(currentDocument.Id, newSourceRoot);

                var project = solution.GetProject(document.Project.Id);
                var targetDocument = project.Documents.FirstOrDefault(d => d.FilePath == targetPath);
                if (targetDocument == null)
                {
                    var (targetText, targetEnc) = await RefactoringHelpers.ReadFileWithEncodingAsync(targetPath);
                    var targetSourceText = SourceText.From(targetText, targetEnc);
                    targetDocument = project.AddDocument(Path.GetFileName(targetPath), targetSourceText, filePath: targetPath);
                    solution = targetDocument.Project.Solution;
                }
                else
                {
                    var (targetText, targetEnc) = await RefactoringHelpers.ReadFileWithEncodingAsync(targetPath);
                    var targetSourceText = SourceText.From(targetText, targetEnc);
                    solution = solution.WithDocumentText(targetDocument.Id, targetSourceText);
                }
                currentDocument = solution.GetDocument(currentDocument.Id);
            }

            RefactoringHelpers.UpdateSolutionCache(currentDocument);
            messages.Add(message);
        }

        return (string.Join("\n", messages), currentDocument);
    }
}
