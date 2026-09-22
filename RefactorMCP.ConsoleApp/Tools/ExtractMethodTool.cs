using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

[McpServerToolType]
public static class ExtractMethodTool
{
    [McpServerTool, Description("Extract a code block into a new method (preferred for large C# file refactoring)")]
    public static async Task<string> ExtractMethod(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Range in format 'startLine:startColumn-endLine:endColumn'")] string selectionRange,
        [Description("Name for the new method")] string methodName)
    {
        try
        {
            return await RefactoringHelpers.RunWithSolutionOrFile(
                solutionPath,
                filePath,
                doc => ExtractMethodWithSolution(doc, selectionRange, methodName),
                path => ExtractMethodSingleFile(path, selectionRange, methodName));
        }
        catch (Exception ex)
        {
            throw new McpException($"Error extracting method: {ex.Message}", ex);
        }
    }

    private static async Task<string> ExtractMethodWithSolution(Document document, string selectionRange, string methodName)
    {
        var sourceText = await document.GetTextAsync();
        var syntaxRoot = await document.GetSyntaxRootAsync();
        var span = RefactoringHelpers.ParseSelectionRange(sourceText, selectionRange);

        var selectedNodes = syntaxRoot!.DescendantNodes()
            .Where(n => span.Contains(n.Span))
            .ToList();

        if (!selectedNodes.Any())
            throw new McpException("Error: No valid code selected");

        var containingMethod = selectedNodes.First().Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (containingMethod == null)
            throw new McpException("Error: Selected code is not within a method");
        if (containingMethod.Body == null)
        {
            if (containingMethod.ExpressionBody != null)
                throw new McpException("Error: Extraction from expression-bodied methods is not supported");

            throw new McpException("Error: Selected code is not within a block-bodied method");
        }

        var statementsToExtract = containingMethod.Body.Statements
            .Where(s => span.IntersectsWith(s.FullSpan))
            .ToList();

        if (!statementsToExtract.Any())
            throw new McpException("Error: Selected code does not contain extractable statements");

        var containingClass = containingMethod.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        var semanticModel = await document.GetSemanticModelAsync();
        var rewriter = new ExtractMethodRewriter(containingMethod, containingClass, statementsToExtract, methodName, semanticModel);
        var newRoot = rewriter.Visit(syntaxRoot);

        var formattedRoot = Formatter.Format(newRoot!, document.Project.Solution.Workspace);
        await RefactoringHelpers.WriteAndUpdateCachesAsync(document, formattedRoot);

        return $"Successfully extracted method '{methodName}' from {selectionRange} in {document.FilePath} (solution mode)";
    }

    private static async Task<string> ExtractMethodSingleFile(string filePath, string selectionRange, string methodName)
    {
        var semanticModel = await RefactoringHelpers.GetOrCreateSemanticModelAsync(filePath);
        return await RefactoringHelpers.ApplySingleFileEdit(
            filePath,
            text => ExtractMethodInSource(text, selectionRange, methodName, semanticModel),
            $"Successfully extracted method '{methodName}' from {selectionRange} in {filePath} (single file mode)");
    }

    public static string ExtractMethodInSource(string sourceText, string selectionRange, string methodName, SemanticModel? model = null)
    {
        var syntaxTree = model?.SyntaxTree ?? CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = syntaxTree.GetRoot();
        var text = syntaxTree.GetText();
        var span = RefactoringHelpers.ParseSelectionRange(text, selectionRange);

        var selectedNodes = syntaxRoot.DescendantNodes()
            .Where(n => span.Contains(n.Span))
            .ToList();

        if (!selectedNodes.Any())
            throw new McpException("Error: No valid code selected");

        var containingMethod = selectedNodes.First().Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (containingMethod == null)
            throw new McpException("Error: Selected code is not within a method");
        if (containingMethod.Body == null)
        {
            if (containingMethod.ExpressionBody != null)
                throw new McpException("Error: Extraction from expression-bodied methods is not supported");

            throw new McpException("Error: Selected code is not within a block-bodied method");
        }

        var statementsToExtract = containingMethod.Body.Statements
            .Where(s => span.IntersectsWith(s.FullSpan))
            .ToList();

        if (!statementsToExtract.Any())
            throw new McpException("Error: Selected code does not contain extractable statements");

        var containingClass = containingMethod.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        var rewriter = new ExtractMethodRewriter(containingMethod, containingClass, statementsToExtract, methodName, model);
        var newRoot = rewriter.Visit(syntaxRoot);

        var formattedRoot = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
        return formattedRoot.ToFullString();
    }

}
