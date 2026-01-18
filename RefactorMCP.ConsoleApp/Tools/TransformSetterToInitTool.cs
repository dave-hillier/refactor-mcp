using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

[McpServerToolType]
public static class TransformSetterToInitTool
{
    [McpServerTool, Description("Convert property setter to init-only setter (preferred for large C# file refactoring)")]
    public static async Task<string> TransformSetterToInit(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Name of the property to transform")] string propertyName)
    {
        try
        {
            return await RefactoringHelpers.RunWithSolutionOrFile(
                solutionPath,
                filePath,
                doc => TransformSetterToInitWithSolution(doc, propertyName),
                path => TransformSetterToInitSingleFile(path, propertyName));
        }
        catch (Exception ex)
        {
            throw new McpException($"Error transforming setter: {ex.Message}", ex);
        }
    }

    private static async Task<string> TransformSetterToInitWithSolution(Document document, string propertyName)
    {
        var syntaxRoot = await document.GetSyntaxRootAsync();

        var property = syntaxRoot!.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .FirstOrDefault(p => p.Identifier.ValueText == propertyName);
        if (property == null)
            throw new McpException($"Error: No property named '{propertyName}' found");

        var setter = property.AccessorList?.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.SetAccessorDeclaration));
        if (setter == null)
            throw new McpException($"Error: Property '{propertyName}' has no setter");

        var rewriter = new SetterToInitRewriter(propertyName);
        var newRoot = rewriter.Visit(syntaxRoot);
        var formatted = Formatter.Format(newRoot!, document.Project.Solution.Workspace);
        var newDocument = document.WithSyntaxRoot(formatted);
        var newText = await newDocument.GetTextAsync();
        var encoding = await RefactoringHelpers.GetFileEncodingAsync(document.FilePath!);
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString(), encoding);
        RefactoringHelpers.UpdateSolutionCache(newDocument);

        return $"Successfully converted setter to init for '{propertyName}' in {document.FilePath} (solution mode)";
    }

    private static Task<string> TransformSetterToInitSingleFile(string filePath, string propertyName)
    {
        return RefactoringHelpers.ApplySingleFileEdit(
            filePath,
            text => TransformSetterToInitInSource(text, propertyName),
            $"Successfully converted setter to init for '{propertyName}' in {filePath} (single file mode)");
    }

    public static string TransformSetterToInitInSource(string sourceText, string propertyName)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = syntaxTree.GetRoot();

        var property = syntaxRoot.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .FirstOrDefault(p => p.Identifier.ValueText == propertyName);
        if (property == null)
            throw new McpException($"Error: No property named '{propertyName}' found");

        var setter = property.AccessorList?.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.SetAccessorDeclaration));
        if (setter == null)
            throw new McpException($"Error: Property '{propertyName}' has no setter");

        var rewriter = new SetterToInitRewriter(propertyName);
        var newRoot = rewriter.Visit(syntaxRoot);
        var formatted = Formatter.Format(newRoot!, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }
}
