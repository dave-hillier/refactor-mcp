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

public class MethodAndMemberVisitor : CSharpSyntaxWalker
{
    public class MethodInfo
    {
        public bool IsStatic { get; set; }
    }

    public class MemberInfo
    {
        public string Type { get; set; } // "field" or "property"
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
    private class FileOperationContext
    {
        public Document Document { get; set; }
        public string SourcePath => Document.FilePath!;
        public string TargetPath { get; set; }
        public bool SameFile => SourcePath == TargetPath;
        public SyntaxNode SourceRoot { get; set; }
        public string SourceClassName { get; set; }
        public string TargetClassName { get; set; }
    }

    private static async Task<string> MoveSingleMethod(
        FileOperationContext context,
        string methodName,
        bool isStatic,
        string accessMember,
        string accessMemberType)
    {
        dynamic moveResult;
        if (isStatic)
        {
            moveResult = MoveMethodsTool.MoveStaticMethodAst(context.SourceRoot, methodName, context.TargetClassName);
        }
        else
        {
            moveResult = MoveMethodsTool.MoveInstanceMethodAst(
                context.SourceRoot,
                context.SourceClassName,
                methodName,
                context.TargetClassName,
                accessMember,
                accessMemberType);
        }

        var newSourceRoot = (SyntaxNode)moveResult.NewSourceRoot;
        var movedMethod = (MethodDeclarationSyntax)moveResult.MovedMethod;

        var updatedTargetRoot = await PrepareTargetRoot(context, newSourceRoot, movedMethod);
        await WriteTransformedFiles(context, newSourceRoot, updatedTargetRoot);

        if (!context.SameFile)
        {
            RefactoringHelpers.AddDocumentToProject(context.Document.Project, context.TargetPath);
        }
        
        var newRoot = context.SameFile
            ? Formatter.Format(updatedTargetRoot, RefactoringHelpers.SharedWorkspace)
            : Formatter.Format(newSourceRoot, RefactoringHelpers.SharedWorkspace);

        var updatedDoc = context.Document.WithSyntaxRoot(newRoot);
        RefactoringHelpers.UpdateSolutionCache(updatedDoc);
        context.Document = updatedDoc;
        context.SourceRoot = await updatedDoc.GetSyntaxRootAsync();

        return isStatic
            ? $"Successfully moved static method '{methodName}' to {context.TargetClassName} in {context.TargetPath}"
            : $"Successfully moved {context.SourceClassName}.{methodName} instance method to {context.TargetClassName} in {context.TargetPath}";
    }

    private static async Task<SyntaxNode> PrepareTargetRoot(FileOperationContext context, SyntaxNode newSourceRoot, MethodDeclarationSyntax methodToMove)
    {
        SyntaxNode targetRoot;

        if (context.SameFile)
        {
            targetRoot = newSourceRoot;
        }
        else
        {
            targetRoot = await LoadOrCreateTargetRoot(context.TargetPath);
            targetRoot = MoveMethodsTool.PropagateUsings(context.SourceRoot, targetRoot);
        }

        return MoveMethodsTool.AddMethodToTargetClass(targetRoot, context.TargetClassName, methodToMove);
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

    // Solution/Document operations that use the AST layer

    [McpServerTool, Description("Move multiple methods from a source class to a target class, automatically ordering by dependencies. " +
        "Wrapper methods remain at the original locations to delegate to the moved implementations.")]
    public static async Task<string> MoveMultipleMethods(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file containing the methods")] string filePath,
        [Description("Name of the source class containing the methods")] string sourceClass,
        [Description("Names of the methods to move")] string[] methodNames,
        [Description("Name of the target class")] string targetClass,
        [Description("Name for the access member")] string accessMember,
        [Description("Path to the target file (optional)")] string? targetFilePath = null)
    {
        if (methodNames.Length == 0)
            return RefactoringHelpers.ThrowMcpException("Error: No method names provided");

        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath);
        var document = RefactoringHelpers.GetDocumentByPath(solution, filePath);

        if (document != null)
        {
            var root = await document.GetSyntaxRootAsync();
            if (root == null)
                return RefactoringHelpers.ThrowMcpException("Error: Could not get syntax root");

            var classNodes = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToDictionary(c => c.Identifier.ValueText);

            if (!classNodes.TryGetValue(sourceClass, out var sourceClassNode))
                return RefactoringHelpers.ThrowMcpException($"Error: Source class '{sourceClass}' not found");

            var visitor = new MethodAndMemberVisitor();
            visitor.Visit(sourceClassNode);

            var isStatic = new bool[methodNames.Length];
            var accessMemberTypes = new string[methodNames.Length];

            for (int i = 0; i < methodNames.Length; i++)
            {
                var methodName = methodNames[i];
                if (!visitor.Methods.TryGetValue(methodName, out var methodInfo))
                    return RefactoringHelpers.ThrowMcpException($"Error: No method named '{methodName}' in class '{sourceClass}'");

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
            var context = new FileOperationContext
            {
                Document = document,
                SourceRoot = root,
                SourceClassName = sourceClass,
                TargetClassName = targetClass,
                TargetPath = targetFilePath ?? Path.Combine(Path.GetDirectoryName(document.FilePath!)!, $"{targetClass}.cs")
            };

            foreach (var idx in orderedIndices)
            {
                var resultMessage = await MoveSingleMethod(
                    context,
                    methodNames[idx],
                    isStatic[idx],
                    accessMember,
                    accessMemberTypes[idx]);
                results.Add(resultMessage);
            }

            return string.Join("\n", results);
        }
        
        // Fallback to AST-based approach for single-file mode or cross-file operations
        // This path is no longer needed after unification
        return RefactoringHelpers.ThrowMcpException("Error: Could not find document in solution and AST fallback is disabled.");
    }
}
