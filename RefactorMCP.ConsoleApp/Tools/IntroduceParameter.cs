using ModelContextProtocol.Server;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

public static partial class RefactoringTools
{
    private static async Task<string> IntroduceParameterWithSolution(Document document, string methodName, string selectionRange, string parameterName)
    {
        var sourceText = await document.GetTextAsync();
        var syntaxRoot = await document.GetSyntaxRootAsync();
        var textLines = sourceText.Lines;

        var method = syntaxRoot!.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            return $"Error: No method named '{methodName}' found";

        if (!TryParseRange(selectionRange, out var startLine, out var startColumn, out var endLine, out var endColumn))
            return "Error: Invalid selection range format";

        var startPosition = textLines[startLine - 1].Start + startColumn - 1;
        var endPosition = textLines[endLine - 1].Start + endColumn - 1;
        var span = TextSpan.FromBounds(startPosition, endPosition);

        var selectedExpression = syntaxRoot.DescendantNodes(span).OfType<ExpressionSyntax>().FirstOrDefault();
        if (selectedExpression == null)
            return "Error: Selected code is not a valid expression";

        var semanticModel = await document.GetSemanticModelAsync();
        var typeInfo = semanticModel!.GetTypeInfo(selectedExpression);
        var typeName = typeInfo.Type?.ToDisplayString() ?? "object";

        var parameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameterName))
            .WithType(SyntaxFactory.ParseTypeName(typeName));
        var newMethod = method.AddParameterListParameters(parameter);

        var newRoot = syntaxRoot.ReplaceNode(method, newMethod);
        newRoot = newRoot.ReplaceNode(selectedExpression, SyntaxFactory.IdentifierName(parameterName));

        var formattedRoot = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        var newDocument = document.WithSyntaxRoot(formattedRoot);
        var newText = await newDocument.GetTextAsync();
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString());

        return $"Successfully introduced parameter '{parameterName}' from {selectionRange} in method '{methodName}' in {document.FilePath} (solution mode)";
    }

    private static async Task<string> IntroduceParameterSingleFile(string filePath, string methodName, string selectionRange, string parameterName)
    {
        if (!File.Exists(filePath))
            return $"Error: File {filePath} not found";

        var sourceText = await File.ReadAllTextAsync(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = await syntaxTree.GetRootAsync();
        var textLines = SourceText.From(sourceText).Lines;

        var method = syntaxRoot.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            return $"Error: No method named '{methodName}' found";

        if (!TryParseRange(selectionRange, out var startLine, out var startColumn, out var endLine, out var endColumn))
            return "Error: Invalid selection range format";

        var startPosition = textLines[startLine - 1].Start + startColumn - 1;
        var endPosition = textLines[endLine - 1].Start + endColumn - 1;
        var span = TextSpan.FromBounds(startPosition, endPosition);

        var selectedExpression = syntaxRoot.DescendantNodes()
            .OfType<ExpressionSyntax>()
            .FirstOrDefault(e => span.Contains(e.Span) || e.Span.Contains(span));
        if (selectedExpression == null)
            return "Error: Selected code is not a valid expression";

        var parameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameterName))
            .WithType(SyntaxFactory.ParseTypeName("object"));
        var newMethod = method.AddParameterListParameters(parameter);

        var newRoot = syntaxRoot.ReplaceNode(method, newMethod);
        newRoot = newRoot.ReplaceNode(selectedExpression, SyntaxFactory.IdentifierName(parameterName));

        var workspace = new AdhocWorkspace();
        var formattedRoot = Formatter.Format(newRoot, workspace);
        await File.WriteAllTextAsync(filePath, formattedRoot.ToFullString());

        return $"Successfully introduced parameter '{parameterName}' from {selectionRange} in method '{methodName}' in {filePath} (single file mode)";
    }
    [McpServerTool, Description("Create a new parameter from selected code (preferred for large-file refactoring)")]
    public static async Task<string> IntroduceParameter(
        [Description("Path to the C# file")] string filePath,
        [Description("Name of the method to add parameter to")] string methodName,
        [Description("Range in format 'startLine:startColumn-endLine:endColumn'")] string selectionRange,
        [Description("Name for the new parameter")] string parameterName,
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

                return await IntroduceParameterWithSolution(document, methodName, selectionRange, parameterName);
            }
            else
            {
                return await IntroduceParameterSingleFile(filePath, methodName, selectionRange, parameterName);
            }
        }
        catch (Exception ex)
        {
            return $"Error introducing parameter: {ex.Message}";
        }
    }
}
