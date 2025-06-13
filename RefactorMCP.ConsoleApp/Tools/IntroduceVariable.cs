using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

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
            return RefactoringHelpers.ThrowMcpException("Error: Invalid selection range format");

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
            return RefactoringHelpers.ThrowMcpException("Error: Selected code is not a valid expression");

        // Get the semantic model to determine the type
        var semanticModel = await document.GetSemanticModelAsync();
        var typeInfo = semanticModel!.GetTypeInfo(selectedExpression);
        var typeName = typeInfo.Type?.ToDisplayString() ?? "var";

        // Create the variable declaration
        var variableDeclaration = SyntaxFactory.LocalDeclarationStatement(
            SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName(typeName))
            .WithVariables(SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.VariableDeclarator(variableName)
                .WithInitializer(SyntaxFactory.EqualsValueClause(initializerExpression)))));

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
        var newDocument = document.WithSyntaxRoot(formattedRoot);
        var newText = await newDocument.GetTextAsync();
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString());
        RefactoringHelpers.UpdateSolutionCache(newDocument);

        return $"Successfully introduced variable '{variableName}' from {selectionRange} in {document.FilePath} (solution mode)";
    }

    private static Task<string> IntroduceVariableSingleFile(string filePath, string selectionRange, string variableName)
    {
        return RefactoringHelpers.ApplySingleFileEdit(
            filePath,
            text => IntroduceVariableInSource(text, selectionRange, variableName),
            $"Successfully introduced variable '{variableName}' from {selectionRange} in {filePath} (single file mode)");
    }

    public static string IntroduceVariableInSource(string sourceText, string selectionRange, string variableName)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = syntaxTree.GetRoot();
        var textLines = SourceText.From(sourceText).Lines;

        if (!RefactoringHelpers.TryParseRange(selectionRange, out var startLine, out var startColumn, out var endLine, out var endColumn))
            return RefactoringHelpers.ThrowMcpException("Error: Invalid selection range format");

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
            return RefactoringHelpers.ThrowMcpException("Error: Selected code is not a valid expression");

        var typeName = "var";

        var variableDeclaration = SyntaxFactory.LocalDeclarationStatement(
            SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName(typeName))
                .WithVariables(SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.VariableDeclarator(variableName)
                        .WithInitializer(SyntaxFactory.EqualsValueClause(initializerExpression)))));

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
