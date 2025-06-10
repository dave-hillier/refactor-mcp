using ModelContextProtocol.Server;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

public static partial class RefactoringTools
{
    [McpServerTool, Description("Convert property setter to init-only setter (preferred for large-file refactoring)")]
    public static async Task<string> TransformSetterToInit(
        [Description("Path to the C# file")] string filePath,
        [Description("Name of the property to transform")] string propertyName,
        [Description("Path to the solution file (.sln) - optional for single file mode")] string? solutionPath = null)
    {
        try
        {
            if (solutionPath != null)
            {
                var solution = await GetOrLoadSolution(solutionPath);
                var document = GetDocumentByPath(solution, filePath);
                if (document == null)
                    return $"Error: File {filePath} not found in solution";

                return await TransformSetterToInitWithSolution(document, propertyName);
            }
            else
            {
                return await TransformSetterToInitSingleFile(filePath, propertyName);
            }
        }
        catch (Exception ex)
        {
            return $"Error transforming setter: {ex.Message}";
        }
    }

    private static async Task<string> TransformSetterToInitWithSolution(Document document, string propertyName)
    {
        var sourceText = await document.GetTextAsync();
        var syntaxRoot = await document.GetSyntaxRootAsync();

        var property = syntaxRoot!.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .FirstOrDefault(p => p.Identifier.ValueText == propertyName);
        if (property == null)
            return $"Error: No property named '{propertyName}' found";

        var setter = property.AccessorList?.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.SetAccessorDeclaration));
        if (setter == null)
            return $"Error: Property '{propertyName}' has no setter";

        var initAccessor = SyntaxFactory.AccessorDeclaration(SyntaxKind.InitAccessorDeclaration)
            .WithSemicolonToken(setter.SemicolonToken);
        var newProperty = property.ReplaceNode(setter, initAccessor);

        var newRoot = syntaxRoot.ReplaceNode(property, newProperty);
        var formatted = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        var newDocument = document.WithSyntaxRoot(formatted);
        var newText = await newDocument.GetTextAsync();
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString());

        return $"Successfully converted setter to init for '{propertyName}' in {document.FilePath} (solution mode)";
    }

    private static async Task<string> TransformSetterToInitSingleFile(string filePath, string propertyName)
    {
        if (!File.Exists(filePath))
            return $"Error: File {filePath} not found";

        var sourceText = await File.ReadAllTextAsync(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = await syntaxTree.GetRootAsync();

        var property = syntaxRoot.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .FirstOrDefault(p => p.Identifier.ValueText == propertyName);
        if (property == null)
            return $"Error: No property named '{propertyName}' found";

        var setter = property.AccessorList?.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.SetAccessorDeclaration));
        if (setter == null)
            return $"Error: Property '{propertyName}' has no setter";

        var initAccessor = SyntaxFactory.AccessorDeclaration(SyntaxKind.InitAccessorDeclaration)
            .WithSemicolonToken(setter.SemicolonToken);
        var newProperty = property.ReplaceNode(setter, initAccessor);

        var newRoot = syntaxRoot.ReplaceNode(property, newProperty);
        var formatted = Formatter.Format(newRoot, SharedWorkspace);
        await File.WriteAllTextAsync(filePath, formatted.ToFullString());

        return $"Successfully converted setter to init for '{propertyName}' in {filePath} (single file mode)";
    }
}
