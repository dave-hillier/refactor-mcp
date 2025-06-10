using ModelContextProtocol.Server;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

public static partial class RefactoringTools
{
    [McpServerTool, Description("Convert property setter to init-only setter")]
    public static async Task<string> TransformSetterToInit(
        [Description("Path to the C# file")] string filePath,
        [Description("Line number of the property to transform")] int propertyLine,
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

                return await TransformSetterToInitWithSolution(document, propertyLine);
            }
            else
            {
                return await TransformSetterToInitSingleFile(filePath, propertyLine);
            }
        }
        catch (Exception ex)
        {
            return $"Error transforming setter: {ex.Message}";
        }
    }

    private static async Task<string> TransformSetterToInitWithSolution(Document document, int propertyLine)
    {
        var sourceText = await document.GetTextAsync();
        var syntaxRoot = await document.GetSyntaxRootAsync();
        var linePos = sourceText.Lines[propertyLine - 1].Start;

        var property = syntaxRoot!.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .FirstOrDefault(p => p.Span.Contains(linePos));
        if (property == null)
            return $"Error: No property found at line {propertyLine}";

        var setter = property.AccessorList?.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.SetAccessorDeclaration));
        if (setter == null)
            return $"Error: Property at line {propertyLine} has no setter";

        var initAccessor = SyntaxFactory.AccessorDeclaration(SyntaxKind.InitAccessorDeclaration)
            .WithSemicolonToken(setter.SemicolonToken);
        var newProperty = property.ReplaceNode(setter, initAccessor);

        var newRoot = syntaxRoot.ReplaceNode(property, newProperty);
        var formatted = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        var newDocument = document.WithSyntaxRoot(formatted);
        var newText = await newDocument.GetTextAsync();
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString());

        return $"Successfully converted setter to init at line {propertyLine} in {document.FilePath} (solution mode)";
    }

    private static async Task<string> TransformSetterToInitSingleFile(string filePath, int propertyLine)
    {
        if (!File.Exists(filePath))
            return $"Error: File {filePath} not found";

        var sourceText = await File.ReadAllTextAsync(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = await syntaxTree.GetRootAsync();
        var textLines = SourceText.From(sourceText).Lines;
        var linePos = textLines[propertyLine - 1].Start;

        var property = syntaxRoot.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .FirstOrDefault(p => p.Span.Contains(linePos));
        if (property == null)
            return $"Error: No property found at line {propertyLine}";

        var setter = property.AccessorList?.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.SetAccessorDeclaration));
        if (setter == null)
            return $"Error: Property at line {propertyLine} has no setter";

        var initAccessor = SyntaxFactory.AccessorDeclaration(SyntaxKind.InitAccessorDeclaration)
            .WithSemicolonToken(setter.SemicolonToken);
        var newProperty = property.ReplaceNode(setter, initAccessor);

        var newRoot = syntaxRoot.ReplaceNode(property, newProperty);
        var workspace = new AdhocWorkspace();
        var formatted = Formatter.Format(newRoot, workspace);
        await File.WriteAllTextAsync(filePath, formatted.ToFullString());

        return $"Successfully converted setter to init at line {propertyLine} in {filePath} (single file mode)";
    }
}
