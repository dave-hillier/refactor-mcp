using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Editing;

[McpServerToolType]
public static class IntroduceParameterTool
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

        if (!RefactoringHelpers.TryParseRange(selectionRange, out var startLine, out var startColumn, out var endLine, out var endColumn))
            throw new McpException("Error: Invalid selection range format");

        if (!RefactoringHelpers.ValidateRange(sourceText, startLine, startColumn, endLine, endColumn, out var error))
            throw new McpException(error);

        var startPosition = textLines[startLine - 1].Start + startColumn - 1;
        var endPosition = textLines[endLine - 1].Start + endColumn - 1;
        var span = TextSpan.FromBounds(startPosition, endPosition);

        var selectedExpression = syntaxRoot.DescendantNodes(span).OfType<ExpressionSyntax>().FirstOrDefault();
        if (selectedExpression == null)
            throw new McpException("Error: Selected code is not a valid expression");

        var semanticModel = await document.GetSemanticModelAsync();
        var typeInfo = semanticModel!.GetTypeInfo(selectedExpression);
        var typeName = typeInfo.Type?.ToDisplayString() ?? "object";

        var parameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameterName))
            .WithType(SyntaxFactory.ParseTypeName(typeName));

        var parameterReference = SyntaxFactory.IdentifierName(parameterName);
        var generator = SyntaxGenerator.GetGenerator(document.Project.Solution.Workspace, LanguageNames.CSharp);
        var rewriter = new ParameterIntroductionRewriter(selectedExpression, methodName, parameter, parameterReference, generator);
        var newRoot = rewriter.Visit(syntaxRoot);

        var formattedRoot = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        var newDocument = document.WithSyntaxRoot(formattedRoot);
        var newText = await newDocument.GetTextAsync();
        var encoding = await RefactoringHelpers.GetFileEncodingAsync(document.FilePath!);
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString(), encoding);
        RefactoringHelpers.UpdateSolutionCache(newDocument);

        return $"Successfully introduced parameter '{parameterName}' from {selectionRange} in method '{methodName}' in {document.FilePath} (solution mode)";
    }

    private static Task<string> IntroduceParameterSingleFile(string filePath, string methodName, string selectionRange, string parameterName)
    {
        return RefactoringHelpers.ApplySingleFileEdit(
            filePath,
            text => IntroduceParameterInSource(text, methodName, selectionRange, parameterName),
            $"Successfully introduced parameter '{parameterName}' from {selectionRange} in method '{methodName}' in {filePath} (single file mode)");
    }

    public static string IntroduceParameterInSource(string sourceText, string methodName, string selectionRange, string parameterName)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = syntaxTree.GetRoot();
        var text = SourceText.From(sourceText);
        var textLines = text.Lines;

        var method = syntaxRoot.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            return $"Error: No method named '{methodName}' found";

        if (!RefactoringHelpers.TryParseRange(selectionRange, out var startLine, out var startColumn, out var endLine, out var endColumn))
            throw new McpException("Error: Invalid selection range format");

        if (!RefactoringHelpers.ValidateRange(text, startLine, startColumn, endLine, endColumn, out var error))
            throw new McpException(error);

        var startPosition = textLines[startLine - 1].Start + startColumn - 1;
        var endPosition = textLines[endLine - 1].Start + endColumn - 1;
        var span = TextSpan.FromBounds(startPosition, endPosition);

        var selectedExpression = syntaxRoot.DescendantNodes()
            .OfType<ExpressionSyntax>()
            .Where(e => span.Contains(e.Span) || e.Span.Contains(span))
            .OrderBy(e => Math.Abs(e.Span.Length - span.Length))
            .ThenBy(e => e.Span.Length)
            .FirstOrDefault();
        if (selectedExpression == null)
            throw new McpException("Error: Selected code is not a valid expression");

        var parameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameterName))
            .WithType(SyntaxFactory.ParseTypeName("object"));

        var parameterReference = SyntaxFactory.IdentifierName(parameterName);
        var generator = SyntaxGenerator.GetGenerator(RefactoringHelpers.SharedWorkspace, LanguageNames.CSharp);
        var rewriter = new ParameterIntroductionRewriter(selectedExpression, methodName, parameter, parameterReference, generator);
        var newRoot = rewriter.Visit(syntaxRoot);

        var formattedRoot = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
        return formattedRoot.ToFullString();
    }
    [McpServerTool, Description("Create a new parameter from selected code (preferred for large C# file refactoring)")]
    public static async Task<string> IntroduceParameter(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Name of the method to add parameter to")] string methodName,
        [Description("Range in format 'startLine:startColumn-endLine:endColumn'")] string selectionRange,
        [Description("Name for the new parameter")] string parameterName)
    {
        try
        {
            return await RefactoringHelpers.RunWithSolutionOrFile(
                solutionPath,
                filePath,
                doc => IntroduceParameterWithSolution(doc, methodName, selectionRange, parameterName),
                path => IntroduceParameterSingleFile(path, methodName, selectionRange, parameterName));
        }
        catch (Exception ex)
        {
            throw new McpException($"Error introducing parameter: {ex.Message}", ex);
        }
    }
}
