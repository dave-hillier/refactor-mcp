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

public class MethodAndMemberVisitor : CSharpSyntaxWalker
{
    public class MethodInfo
    {
        public bool IsStatic { get; set; }
    }

    public class MemberInfo
    {
        public string Type { get; set; } = string.Empty; // "field" or "property"
    }

    public Dictionary<string, MethodInfo> Methods { get; } = new();
    public Dictionary<string, MemberInfo> Members { get; } = new();

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var methodName = node.Identifier.ValueText;
        if (!Methods.ContainsKey(methodName))
        {
            Methods[methodName] = new MethodInfo
            {
                IsStatic = node.Modifiers.Any(SyntaxKind.StaticKeyword)
            };
        }
    }

    public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        foreach (var variable in node.Declaration.Variables)
        {
            var fieldName = variable.Identifier.ValueText;
            if (!Members.ContainsKey(fieldName))
            {
                Members[fieldName] = new MemberInfo { Type = "field" };
            }
        }
    }

    public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        var propertyName = node.Identifier.ValueText;
        if (!Members.ContainsKey(propertyName))
        {
            Members[propertyName] = new MemberInfo { Type = "property" };
        }
    }
}

[McpServerToolType]
public static partial class MoveMultipleMethodsTool
{
    private static async Task<(string message, Document updatedDocument)> MoveSingleMethod(
        Document document,
        string sourceClass,
        string methodName,
        bool isStatic,
        string targetClass,
        string accessMemberType,
        string targetPath,
        string accessMemberName)
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
                accessMemberType,
                targetPath,
                accessMemberName);
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

        return (message, updatedDoc);
    }



    // Solution/Document operations that use the AST layer

    [McpServerTool, Description("Move multiple methods from a source class to a target class, automatically ordering by dependencies. " +
        "Wrapper methods remain at the original locations to delegate to the moved implementations.")]
    public static async Task<string> MoveMultipleMethods(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file containing the methods")] string filePath,
        [Description("Name of the source class containing the methods")] string sourceClass,
        [Description("Names of the methods to move")] string[] methodNames,
        [Description("Name of the target class")] string targetClass,
        [Description("Path to the target file (optional)")] string? targetFilePath = null)
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

                var classNodes = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToDictionary(c => c.Identifier.ValueText);

                if (!classNodes.TryGetValue(sourceClass, out var sourceClassNode))
                    throw new McpException($"Error: Source class '{sourceClass}' not found");

                var visitor = new MethodAndMemberVisitor();
                visitor.Visit(sourceClassNode);

                var accessMember = GenerateAccessMemberName(sourceClassNode, targetClass);
                var isStatic = new bool[methodNames.Length];
                var accessMemberTypes = new string[methodNames.Length];

                for (int i = 0; i < methodNames.Length; i++)
                {
                    var methodName = methodNames[i];
                    if (!visitor.Methods.TryGetValue(methodName, out var methodInfo))
                        throw new McpException($"Error: No method named '{methodName}' in class '{sourceClass}'");

                    isStatic[i] = methodInfo.IsStatic;

                    if (!isStatic[i])
                    {
                        if (visitor.Members.TryGetValue(accessMember, out var memberInfo))
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
                    var (msg, updatedDoc) = await MoveSingleMethod(
                        currentDoc,
                        sourceClass,
                        methodNames[idx],
                        isStatic[idx],
                        targetClass,
                        accessMemberTypes[idx],
                        targetPath,
                        accessMember);
                    currentDoc = updatedDoc;
                    results.Add(msg);
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
