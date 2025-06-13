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

        var sourceClasses = Enumerable.Repeat(sourceClass, methodNames.Length).ToArray();
        var targetClasses = Enumerable.Repeat(targetClass, methodNames.Length).ToArray();
        var accessMembers = Enumerable.Repeat(accessMember, methodNames.Length).ToArray();
        var targetFiles = targetFilePath != null ? Enumerable.Repeat(targetFilePath, methodNames.Length).ToArray() : null;

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
                    var accessMemberName = accessMembers[i];
                    if (visitor.Members.TryGetValue(accessMemberName, out var memberInfo))
                    {
                        accessMemberTypes[i] = memberInfo.Type;
                    }
                    else
                    {
                        accessMemberTypes[i] = "field"; // Default to field if not found, consistent with original logic
                    }
                }
                else
                {
                    accessMemberTypes[i] = string.Empty; // Not used for static methods
                }
            }

            // Solution-based: need to manage document state between operations
            var results = new List<string>();
            var orderedIndices = OrderOperations(root, sourceClasses, methodNames, targetClasses, accessMembers, accessMemberTypes, isStatic);

            var currentDocument = document;
            for (int i = 0; i < orderedIndices.Count; i++)
            {
                var idx = orderedIndices[i];
                if (isStatic[idx])
                {
                    var (msg, updatedDoc) = await MoveMethodsTool.MoveStaticMethodWithSolution(
                        currentDocument,
                        new[] { methodNames[idx] },
                        targetClasses[idx],
                        targetFiles?[idx]);
                    results.Add(msg);
                    currentDocument = updatedDoc;
                    
                }
                else
                {
                    var (msg, updatedDoc) = await MoveMethodsTool.MoveInstanceMethodWithSolution(
                        currentDocument,
                        sourceClasses[idx],
                        new[] { methodNames[idx] },
                        targetClasses[idx],
                        accessMembers[idx],
                        accessMemberTypes[idx],
                        targetFiles?[idx]);
                    results.Add(msg);
                    currentDocument = updatedDoc;
                }
            }

            RefactoringHelpers.UpdateSolutionCache(currentDocument);

            return string.Join("\n", results);
        }
        
        // Fallback to AST-based approach for single-file mode or cross-file operations
        // This path is no longer needed after unification
        return RefactoringHelpers.ThrowMcpException("Error: Could not find document in solution and AST fallback is disabled.");
    }
}
