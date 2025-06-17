using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Editing;

[McpServerToolType]
public static class IntroduceVariableTool
{
    [McpServerTool, Description("Introduce a new variable from selected expression (preferred for large C# file refactoring)")]
    public static async Task<string> IntroduceVariable(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Range in format 'startLine:startColumn-endLine:endColumn'")] string selectionRange,
        [Description("Name for the new variable")] string variableName)
    {
        try
        {
            return await RefactoringHelpers.RunWithSolutionOrFile(
                solutionPath,
                filePath,
                doc => IntroduceVariableWithSolution(doc, selectionRange, variableName),
                path => IntroduceVariableSingleFile(path, selectionRange, variableName));
        }
        catch (Exception ex)
        {
            throw new McpException($"Error introducing variable: {ex.Message}", ex);
        }
    }

    private static async Task<string> IntroduceVariableWithSolution(Document document, string selectionRange, string variableName)
    {
        var sourceText = await document.GetTextAsync();
        var syntaxRoot = await document.GetSyntaxRootAsync();

        if (!RefactoringHelpers.TryParseRange(selectionRange, out var startLine, out var startColumn, out var endLine, out var endColumn))
            throw new McpException("Error: Invalid selection range format");

        if (!RefactoringHelpers.ValidateRange(sourceText, startLine, startColumn, endLine, endColumn, out var error))
            throw new McpException(error);

        var startPosition = sourceText.Lines[startLine - 1].Start + startColumn - 1;
        var endPosition = sourceText.Lines[endLine - 1].Start + endColumn - 1;
        var span = TextSpan.FromBounds(startPosition, endPosition);

        var selectedExpression = syntaxRoot!.DescendantNodes()
            .OfType<ExpressionSyntax>()
            .Where(e => span.Contains(e.Span) || e.Span.Contains(span))
            .OrderBy(e => Math.Abs(e.Span.Length - span.Length))
            .ThenBy(e => e.Span.Length)
            .FirstOrDefault();
        var initializerExpression = selectedExpression;
        if (selectedExpression?.Parent is ParenthesizedExpressionSyntax paren && paren.Span.Contains(span))
            selectedExpression = paren;

        if (selectedExpression == null)
            throw new McpException("Error: Selected code is not a valid expression");

        // Get the semantic model to determine the type
        var semanticModel = await document.GetSemanticModelAsync();
        var typeInfo = semanticModel!.GetTypeInfo(selectedExpression);
        var typeName = typeInfo.Type?.ToDisplayString() ?? "var";

        // Create the variable declaration
        var variableDeclaration = SyntaxFactory.LocalDeclarationStatement(
            SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName(typeName))
            .WithVariables(SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.VariableDeclarator(variableName)
                            .WithInitializer(SyntaxFactory.EqualsValueClause(initializerExpression!)))));

        var variableReference = SyntaxFactory.IdentifierName(variableName);

        var containingStatement = selectedExpression.Ancestors().OfType<StatementSyntax>().FirstOrDefault();
        var containingBlock = containingStatement?.Parent as BlockSyntax;
        var rewriter = new VariableIntroductionRewriter(
            selectedExpression,
            variableReference,
            variableDeclaration,
            containingStatement,
            containingBlock);
        var newRoot = rewriter.Visit(syntaxRoot);

        var formattedRoot = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        var editor = await DocumentEditor.CreateAsync(document);
        editor.ReplaceNode(syntaxRoot, formattedRoot);
        var newDocument = editor.GetChangedDocument();
        var newText = await newDocument.GetTextAsync();
        var encoding = await RefactoringHelpers.GetFileEncodingAsync(document.FilePath!);
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString(), encoding);
        RefactoringHelpers.UpdateSolutionCache(newDocument);

        return $"Successfully introduced variable '{variableName}' from {selectionRange} in {document.FilePath} (solution mode)";
    }

    private static async Task<string> IntroduceVariableSingleFile(string filePath, string selectionRange, string variableName)
    {
        if (!File.Exists(filePath))
            throw new McpException($"Error: File {filePath} not found");

        var (sourceText, encoding) = await RefactoringHelpers.ReadFileWithEncodingAsync(filePath);
        var model = await RefactoringHelpers.GetOrCreateSemanticModelAsync(filePath);
        var newText = IntroduceVariableInSource(sourceText, selectionRange, variableName, model);
        await File.WriteAllTextAsync(filePath, newText, encoding);
        RefactoringHelpers.UpdateFileCaches(filePath, newText);
        return $"Successfully introduced variable '{variableName}' from {selectionRange} in {filePath} (single file mode)";
    }

    public static string IntroduceVariableInSource(string sourceText, string selectionRange, string variableName, SemanticModel? model = null)
    {
        var syntaxTree = model?.SyntaxTree ?? CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = syntaxTree.GetRoot();
        var text = syntaxTree.GetText();
        var textLines = text.Lines;

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
        var initializerExpression = selectedExpression;
        if (selectedExpression?.Parent is ParenthesizedExpressionSyntax paren && paren.Span.Contains(span))
            selectedExpression = paren;

        if (selectedExpression == null)
            throw new McpException("Error: Selected code is not a valid expression");

        var typeName = "var";
        if (model != null)
        {
            var typeInfo = model.GetTypeInfo(initializerExpression ?? selectedExpression!);
            if (typeInfo.Type != null)
                typeName = typeInfo.Type.ToDisplayString();
        }

        var variableDeclaration = SyntaxFactory.LocalDeclarationStatement(
            SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName(typeName))
                .WithVariables(SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.VariableDeclarator(variableName)
                        .WithInitializer(SyntaxFactory.EqualsValueClause(initializerExpression!)))));

        var variableReference = SyntaxFactory.IdentifierName(variableName);
        var containingStatement = selectedExpression.Ancestors().OfType<StatementSyntax>().FirstOrDefault();
        var containingBlock = containingStatement?.Parent as BlockSyntax;
        var rewriter = new VariableIntroductionRewriter(
            selectedExpression,
            variableReference,
            variableDeclaration,
            containingStatement,
            containingBlock);
        var newRoot = rewriter.Visit(syntaxRoot);

        var formattedRoot = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
        return formattedRoot.ToFullString();
    }

}
