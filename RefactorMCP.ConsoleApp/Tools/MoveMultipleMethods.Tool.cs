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
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;
using RefactorMCP.ConsoleApp.SyntaxRewriters;

[McpServerToolType]
public static partial class MoveMultipleMethodsTool
{
    private static async Task<(string message, Document updatedDocument)> MoveSingleMethod(
        Document document,
        string sourceClass,
        string methodName,
        bool isStatic,
        string targetClass,
        string accessMember,
        string accessMemberType,
        string targetPath)
    {
        string message;
        if (isStatic)
        {
            message = await MoveMethodsTool.MoveStaticMethodInFile(document.FilePath!, methodName, targetClass, targetPath);
        }
        else
        {
            message = await MoveMethodsTool.MoveInstanceMethodInFile(
                document.FilePath!,
                sourceClass,
                methodName,
                targetClass,
                accessMember,
                accessMemberType,
                targetPath);
        }

        var (newText, _) = await RefactoringHelpers.ReadFileWithEncodingAsync(document.FilePath!);
        var newRoot = await CSharpSyntaxTree.ParseText(newText).GetRootAsync();
        var solution = document.Project.Solution.WithDocumentSyntaxRoot(document.Id, newRoot);

        var project = solution.GetProject(document.Project.Id)!;
        var targetDocument = project.Documents.FirstOrDefault(d => d.FilePath == targetPath);
        if (targetDocument == null)
        {
            var (targetText, targetEncoding) = await RefactoringHelpers.ReadFileWithEncodingAsync(targetPath);
            var targetSource = SourceText.From(targetText, targetEncoding);
            targetDocument = project.AddDocument(Path.GetFileName(targetPath), targetSource, filePath: targetPath);
            solution = targetDocument.Project.Solution;
        }
        else
        {
            var (targetText, targetEncoding) = await RefactoringHelpers.ReadFileWithEncodingAsync(targetPath);
            var targetSource = SourceText.From(targetText, targetEncoding);
            solution = solution.WithDocumentText(targetDocument.Id, targetSource);
        }

        var updatedDoc = solution.GetDocument(document.Id)!;
        RefactoringHelpers.UpdateSolutionCache(updatedDoc);

        MoveMethodsTool.MarkMoved(document.FilePath!, methodName);
        return (message, updatedDoc);
    }



    // Solution/Document operations that use the AST layer

    [McpServerTool, Description("Move multiple methods from a source class to a target class, automatically ordering by dependencies. " +
        "Wrapper methods remain at the original locations to delegate to the moved implementations." +
        "The target class will be automatically created if it doesn't exist.")]
    public static async Task<string> MoveMultipleMethods(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file containing the methods")] string filePath,
        [Description("Name of the source class containing the methods")] string sourceClass,
        [Description("Names of the methods to move")] string[] methodNames,
        [Description("Name of the target class")] string targetClass,
        [Description("Path to the target file (optional, target class will be automatically created if it doesnt exist or its unspecified)")] string? targetFilePath = null)
    {
        try
        {
            if (methodNames.Length == 0)
                throw new McpException("Error: No method names provided");

            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath);
            var document = RefactoringHelpers.GetDocumentByPath(solution, filePath);

            if (document != null)
            {
                var root = await document.GetSyntaxRootAsync();
                if (root == null)
                    throw new McpException("Error: Could not get syntax root");

                var collector = new ClassCollectorWalker();
                collector.Visit(root);
                var classNodes = collector.Classes;

                if (!classNodes.TryGetValue(sourceClass, out var sourceClassNode))
                    throw new McpException($"Error: Source class '{sourceClass}' not found");

                var visitor = new MethodAndMemberVisitor();
                visitor.Visit(sourceClassNode);

                var accessMemberName = MoveMethodsTool.GenerateAccessMemberName(visitor.Members.Keys, targetClass);

                var isStatic = new bool[methodNames.Length];
                var accessMemberTypes = new string[methodNames.Length];

                for (int i = 0; i < methodNames.Length; i++)
                {
                    var methodName = methodNames[i];
                    if (!visitor.Methods.TryGetValue(methodName, out var methodInfo))
                        return $"Error: No method named '{methodName}' in class '{sourceClass}'";

                    isStatic[i] = methodInfo.IsStatic;

                    if (!isStatic[i])
                    {
                        if (visitor.Members.TryGetValue(accessMemberName, out var memberInfo))
                        {
                            accessMemberTypes[i] = memberInfo.Type;
                        }
                        else
                        {
                            accessMemberTypes[i] = "field"; // Default to field if not found
                        }
                    }
                    else
                    {
                        accessMemberTypes[i] = string.Empty; // Not used for static methods
                    }
                }

                var sourceClasses = Enumerable.Repeat(sourceClass, methodNames.Length).ToArray();
                var orderedIndices = OrderOperations(root, sourceClasses, methodNames);

                var results = new List<string>();
                var currentDoc = document;
                var targetPath = targetFilePath ?? Path.Combine(Path.GetDirectoryName(document.FilePath!)!, $"{targetClass}.cs");

                foreach (var idx in orderedIndices)
                {
                    var result = await MoveSingleMethod(
                        currentDoc,
                        sourceClass,
                        methodNames[idx],
                        isStatic[idx],
                        targetClass,
                        accessMemberName,
                        accessMemberTypes[idx],
                        targetPath);
                    currentDoc = result.updatedDocument;
                    results.Add(result.message);
                }

                return string.Join("\n", results);
            }

            // Fallback to AST-based approach for single-file mode or cross-file operations
            // This path is no longer needed after unification
            throw new McpException("Error: Could not find document in solution and AST fallback is disabled.");
        }
        catch (Exception ex)
        {
            throw new McpException($"Error moving multiple methods: {ex.Message}", ex);
        }
    }
}
