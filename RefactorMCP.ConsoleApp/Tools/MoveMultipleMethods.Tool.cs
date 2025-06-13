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

            var isStatic = new bool[methodNames.Length];
            var accessMemberTypes = new string[methodNames.Length];

            if (!classNodes.TryGetValue(sourceClass, out var sourceClassNode))
                return RefactoringHelpers.ThrowMcpException($"Error: Source class '{sourceClass}' not found");

            for (int i = 0; i < methodNames.Length; i++)
            {
                var method = sourceClassNode.Members.OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(m => m.Identifier.ValueText == methodNames[i]);
                if (method == null)
                    return RefactoringHelpers.ThrowMcpException($"Error: No method named '{methodNames[i]}' in class '{sourceClass}'");

                isStatic[i] = method.Modifiers.Any(SyntaxKind.StaticKeyword);
                
                var accessMemberName = accessMembers[i];
                var accessMemberNode = sourceClassNode.Members.FirstOrDefault(m =>
                    (m is FieldDeclarationSyntax fd && fd.Declaration.Variables.Any(v => v.Identifier.ValueText == accessMemberName)) ||
                    (m is PropertyDeclarationSyntax pd && pd.Identifier.ValueText == accessMemberName));
                
                accessMemberTypes[i] = accessMemberNode switch
                {
                    PropertyDeclarationSyntax => "property",
                    FieldDeclarationSyntax => "field",
                    _ => "field" // Default to field if not found
                };
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
                    RefactoringHelpers.UpdateSolutionCache(updatedDoc);
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
                    RefactoringHelpers.UpdateSolutionCache(updatedDoc);
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
