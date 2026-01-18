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
public static class MoveTypeToFileTool
{
    [McpServerTool, Description("Move a top-level type to a separate file with the same name")]
    public static async Task<string> MoveToSeparateFile(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file containing the type")] string filePath,
        [Description("Name of the type to move")] string typeName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var document = RefactoringHelpers.GetDocumentByPath(solution, filePath);

            var newFilePath = Path.Combine(Path.GetDirectoryName(filePath)!, $"{typeName}.cs");

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
            var typeNode = (MemberDeclarationSyntax?)root.DescendantNodes().FirstOrDefault(n =>
                n is BaseTypeDeclarationSyntax bt && bt.Identifier.Text == typeName ||
                n is EnumDeclarationSyntax en && en.Identifier.Text == typeName ||
                n is DelegateDeclarationSyntax dd && dd.Identifier.Text == typeName);
            if (typeNode == null)
                throw new McpException($"Error: Type {typeName} not found. Verify the type name and ensure the file is part of the loaded solution.");

            var duplicateDoc = await RefactoringHelpers.FindTypeInSolution(solution, typeName, filePath, newFilePath);
            if (duplicateDoc != null)
                throw new McpException($"Error: Type {typeName} already exists in {duplicateDoc.FilePath}");

            var rootWithoutType = (CompilationUnitSyntax)root.RemoveNode(typeNode, SyntaxRemoveOptions.KeepNoTrivia)!;
            rootWithoutType = (CompilationUnitSyntax)Formatter.Format(rootWithoutType, RefactoringHelpers.SharedWorkspace);
            var sourceEncoding = await RefactoringHelpers.GetFileEncodingAsync(filePath, cancellationToken);
            await File.WriteAllTextAsync(filePath, rootWithoutType.ToFullString(), sourceEncoding, cancellationToken);

            var usingStatements = root.Usings;
            CompilationUnitSyntax newRoot = SyntaxFactory.CompilationUnit().WithUsings(usingStatements);

            if (typeNode.Parent is NamespaceDeclarationSyntax ns)
            {
                var newNs = ns.WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(typeNode));
                newRoot = newRoot.AddMembers(newNs);
            }
            else if (typeNode.Parent is FileScopedNamespaceDeclarationSyntax fns)
            {
                var newFsNs = fns.WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(typeNode));
                newRoot = newRoot.AddMembers(newFsNs);
            }
            else
            {
                newRoot = newRoot.AddMembers(typeNode);
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

            return $"Successfully moved type '{typeName}' to {newFilePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error moving type: {ex.Message}", ex);
        }
    }
}
