using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Linq;
using System.Threading;

[McpServerToolType]
public static class MoveClassToFileTool
{
    [McpServerTool, Description("Move a class to a separate file with the same name")]
    public static async Task<string> MoveToSeparateFile(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file containing the class")] string filePath,
        [Description("Name of the class to move")] string className,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var document = RefactoringHelpers.GetDocumentByPath(solution, filePath);

            var newFilePath = Path.Combine(Path.GetDirectoryName(filePath)!, $"{className}.cs");

            CompilationUnitSyntax root;
            if (document != null)
            {
                root = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync(cancellationToken))!;
            }
            else
            {
                if (!File.Exists(filePath))
                    throw new McpException($"Error: File {filePath} not found. Verify the file path and ensure the file is part of the loaded solution.");

                var (text, _) = await RefactoringHelpers.ReadFileWithEncodingAsync(filePath, cancellationToken);
                root = (CompilationUnitSyntax)CSharpSyntaxTree.ParseText(text).GetRoot();
            }
            var classNode = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.Text == className);
            if (classNode == null)
                throw new McpException($"Error: Class {className} not found. Verify the class name and ensure the file is part of the loaded solution.");

            var duplicateDoc = await RefactoringHelpers.FindClassInSolution(solution, className, filePath, newFilePath);
            if (duplicateDoc != null)
                throw new McpException($"Error: Class {className} already exists in {duplicateDoc.FilePath}");

            var rootWithoutClass = (CompilationUnitSyntax)root.RemoveNode(classNode, SyntaxRemoveOptions.KeepNoTrivia)!;
            rootWithoutClass = (CompilationUnitSyntax)Formatter.Format(rootWithoutClass, RefactoringHelpers.SharedWorkspace);
            var sourceEncoding = await RefactoringHelpers.GetFileEncodingAsync(filePath, cancellationToken);
            await File.WriteAllTextAsync(filePath, rootWithoutClass.ToFullString(), sourceEncoding, cancellationToken);

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
            if (File.Exists(newFilePath))
                throw new McpException($"Error: File {newFilePath} already exists");
            var newFileEncoding = await RefactoringHelpers.GetFileEncodingAsync(filePath, cancellationToken);
            await File.WriteAllTextAsync(newFilePath, newRoot.ToFullString(), newFileEncoding, cancellationToken);

            if (document != null)
            {
                RefactoringHelpers.AddDocumentToProject(document.Project, newFilePath);
            }
            else
            {
                UnloadSolutionTool.ClearSolutionCache();
            }

            return $"Successfully moved class '{className}' to {newFilePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error moving class: {ex.Message}", ex);
        }
    }
}
