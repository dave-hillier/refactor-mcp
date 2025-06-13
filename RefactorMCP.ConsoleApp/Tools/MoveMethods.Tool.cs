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
            var updatedSources = UpdateSourceAndTargetForStaticMove(moveContext, method);
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
        var sourceText = await File.ReadAllTextAsync(filePath);
        var targetPath = targetFilePath ?? Path.Combine(Path.GetDirectoryName(filePath)!, $"{targetClass}.cs");

        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = await syntaxTree.GetRootAsync();
        var sourceUsings = syntaxRoot.DescendantNodes().OfType<UsingDirectiveSyntax>().ToList();

        return new StaticMethodMoveContext
        {
            SourcePath = filePath,
            TargetPath = targetPath,
            SameFile = targetPath == filePath,
            SourceRoot = syntaxRoot,
            SourceUsings = sourceUsings,
            TargetClassName = targetClass
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

    private static SourceAndTargetRoots UpdateSourceAndTargetForStaticMove(
        StaticMethodMoveContext context,
        MethodDeclarationSyntax method)
    {
        var newSourceRoot = context.SourceRoot.RemoveNode(method, SyntaxRemoveOptions.KeepNoTrivia);
        var targetRoot = PrepareTargetRootForStaticMove(context);
        var updatedTargetRoot = AddMethodToTargetClass(targetRoot, context.TargetClassName, method);

        return new SourceAndTargetRoots
        {
            UpdatedSourceRoot = newSourceRoot!,
            UpdatedTargetRoot = updatedTargetRoot
        };
    }

    private static SyntaxNode PrepareTargetRootForStaticMove(StaticMethodMoveContext context)
    {
        SyntaxNode targetRoot;

        if (context.SameFile)
        {
            targetRoot = context.SourceRoot;
        }
        else if (File.Exists(context.TargetPath))
        {
            var targetText = File.ReadAllText(context.TargetPath);
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
            await File.WriteAllTextAsync(context.SourcePath, formattedSource.ToFullString());
        }

        Directory.CreateDirectory(Path.GetDirectoryName(context.TargetPath)!);
        await File.WriteAllTextAsync(context.TargetPath, formattedTarget.ToFullString());
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

        var sourceText = await File.ReadAllTextAsync(filePath);

        if (sameFile)
        {
            // Same file operation - use multiple individual AST transformations
            var tree = CSharpSyntaxTree.ParseText(sourceText);
            var root = tree.GetRoot();

            foreach (var methodName in methodNames)
            {
                var moveResult = MoveInstanceMethodAst(root, sourceClass, methodName, targetClass, accessMemberName, accessMemberType);
                root = AddMethodToTargetClass(moveResult.NewSourceRoot, targetClass, moveResult.MovedMethod);
            }

            var formatted = Formatter.Format(root, RefactoringHelpers.SharedWorkspace);
            await File.WriteAllTextAsync(filePath, formatted.ToFullString());
            return $"Successfully moved {methodNames.Length} methods from {sourceClass} to {targetClass} in {filePath}";
        }
        else
        {
            // Cross-file operation - for now, fall back to individual moves
            // TODO: Implement efficient cross-file bulk move
            var results = new List<string>();
            foreach (var methodName in methodNames)
            {
                results.Add(await MoveInstanceMethodInFile(filePath, sourceClass, methodName, targetClass, accessMemberName, accessMemberType, targetFilePath));
            }
            return string.Join("\n", results);
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
            var sourceRoot = await currentDocument.GetSyntaxRootAsync();
            if (sourceRoot == null)
            {
                throw new McpException($"Could not get syntax root for document {currentDocument.FilePath}");
            }

            var moveResult = MoveInstanceMethodAst(
                sourceRoot,
                sourceClassName,
                methodName,
                targetClassName,
                accessMemberName,
                accessMemberType);
            
            var targetPath = targetFilePath ?? currentDocument.FilePath!;
            var sameFile = targetPath == currentDocument.FilePath;
            
            var context = new InstanceFileOperationContext
            {
                SourcePath = currentDocument.FilePath!,
                TargetPath = targetPath,
                SameFile = sameFile,
                SourceRoot = sourceRoot,
                TargetClassName = targetClassName,
                SourceClassName = sourceClassName,
                MethodName = methodName,
                AccessMemberName = accessMemberName,
                AccessMemberType = accessMemberType
            };

            await ProcessInstanceMethodFileOperations(context, moveResult);

            if (sameFile)
            {
                var newText = await File.ReadAllTextAsync(targetPath);
                var newRoot = await CSharpSyntaxTree.ParseText(newText).GetRootAsync();
                currentDocument = document.Project.Solution.WithDocumentSyntaxRoot(currentDocument.Id, newRoot).GetDocument(currentDocument.Id);
            }
            else
            {
                var newSourceText = await File.ReadAllTextAsync(context.SourcePath);
                var newSourceRoot = await CSharpSyntaxTree.ParseText(newSourceText).GetRootAsync();
                var solution = document.Project.Solution.WithDocumentSyntaxRoot(currentDocument.Id, newSourceRoot);

                var project = solution.GetProject(document.Project.Id);
                var targetDocument = project.Documents.FirstOrDefault(d => d.FilePath == targetPath);
                if (targetDocument == null)
                {
                    var targetText = await File.ReadAllTextAsync(targetPath);
                    var targetSourceText = SourceText.From(targetText, System.Text.Encoding.UTF8);
                    targetDocument = project.AddDocument(Path.GetFileName(targetPath), targetSourceText, filePath: targetPath);
                    solution = targetDocument.Project.Solution;
                }
                else
                {
                    var targetText = await File.ReadAllTextAsync(targetPath);
                    var targetSourceText = SourceText.From(targetText, System.Text.Encoding.UTF8);
                    solution = solution.WithDocumentText(targetDocument.Id, targetSourceText);
                }
                currentDocument = solution.GetDocument(currentDocument.Id);
            }
            
            RefactoringHelpers.UpdateSolutionCache(currentDocument);
            messages.Add(BuildInstanceMethodSuccessMessage(context, sourceClassName, methodName, targetClassName, targetFilePath));
        }

        return (string.Join("\n", messages), currentDocument);
    }
}
