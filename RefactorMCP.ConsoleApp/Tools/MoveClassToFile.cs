using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

[McpServerToolType]
public static class MoveClassToFileTool
{
    [McpServerTool, Description("Move a class to a separate file with the same name")]
    public static async Task<string> MoveToSeparateFile(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file containing the class")] string filePath,
        [Description("Name of the class to move")] string className)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath);
            var document = RefactoringHelpers.GetDocumentByPath(solution, filePath);

            CompilationUnitSyntax root;
            if (document != null)
            {
                root = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync())!;
            }
            else
            {
                if (!File.Exists(filePath))
                    return RefactoringHelpers.ThrowMcpException($"Error: File {filePath} not found");

                var text = await File.ReadAllTextAsync(filePath);
                root = (CompilationUnitSyntax)CSharpSyntaxTree.ParseText(text).GetRoot();
            }
            var classNode = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.Text == className);
            if (classNode == null)
                return RefactoringHelpers.ThrowMcpException($"Error: Class {className} not found");

            var rootWithoutClass = (CompilationUnitSyntax)root.RemoveNode(classNode, SyntaxRemoveOptions.KeepNoTrivia);
            rootWithoutClass = (CompilationUnitSyntax)Formatter.Format(rootWithoutClass, RefactoringHelpers.SharedWorkspace);
            await File.WriteAllTextAsync(filePath, rootWithoutClass.ToFullString());

            var usingStatements = root.Usings;
            CompilationUnitSyntax newRoot = SyntaxFactory.CompilationUnit().WithUsings(usingStatements);

            if (classNode.Parent is NamespaceDeclarationSyntax ns)
            {
                var newNs = ns.WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(classNode));
                newRoot = newRoot.AddMembers(newNs);
            }
            else if (classNode.Parent is FileScopedNamespaceDeclarationSyntax fns)
            {
                var newFsNs = fns.WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(classNode));
                newRoot = newRoot.AddMembers(newFsNs);
            }
            else
            {
                newRoot = newRoot.AddMembers(classNode);
            }

            newRoot = (CompilationUnitSyntax)Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
            var newFilePath = Path.Combine(Path.GetDirectoryName(filePath)!, $"{className}.cs");
            await File.WriteAllTextAsync(newFilePath, newRoot.ToFullString());

            return $"Successfully moved class '{className}' to {newFilePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error moving class: {ex.Message}", ex);
        }
    }
}
