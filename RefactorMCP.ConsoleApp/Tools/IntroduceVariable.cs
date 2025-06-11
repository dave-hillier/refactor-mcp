using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

public static partial class RefactoringTools
{
    public static async Task<string> IntroduceVariable(
        [Description("Path to the C# file")] string filePath,
        [Description("Range in format 'startLine:startColumn-endLine:endColumn'")] string selectionRange,
        [Description("Name for the new variable")] string variableName,
        [Description("Path to the solution file (.sln) - optional for single file mode")] string? solutionPath = null)
    {
        try
        {
            if (solutionPath != null)
            {
                // Solution mode - full semantic analysis
                var solution = await GetOrLoadSolution(solutionPath);
                var document = GetDocumentByPath(solution, filePath);
                if (document != null)
                    return await IntroduceVariableWithSolution(document, selectionRange, variableName);

                // Fallback to single file mode when file isn't part of the solution
                return await IntroduceVariableSingleFile(filePath, selectionRange, variableName);
            }

            // Single file mode - direct syntax tree manipulation
            return await IntroduceVariableSingleFile(filePath, selectionRange, variableName);
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

        if (!TryParseRange(selectionRange, out var startLine, out var startColumn, out var endLine, out var endColumn))
            return ThrowMcpException("Error: Invalid selection range format");

        var startPosition = sourceText.Lines[startLine - 1].Start + startColumn - 1;
        var endPosition = sourceText.Lines[endLine - 1].Start + endColumn - 1;
        var span = TextSpan.FromBounds(startPosition, endPosition);

        var selectedExpression = syntaxRoot!.DescendantNodes()
            .OfType<ExpressionSyntax>()
            .FirstOrDefault(e => span.Contains(e.Span) || e.Span.Contains(span));

        if (selectedExpression == null)
            return ThrowMcpException("Error: Selected code is not a valid expression");

        // Get the semantic model to determine the type
        var semanticModel = await document.GetSemanticModelAsync();
        var typeInfo = semanticModel!.GetTypeInfo(selectedExpression);
        var typeName = typeInfo.Type?.ToDisplayString() ?? "var";

        // Create the variable declaration
        var variableDeclaration = SyntaxFactory.LocalDeclarationStatement(
            SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName(typeName))
            .WithVariables(SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.VariableDeclarator(variableName)
                .WithInitializer(SyntaxFactory.EqualsValueClause(selectedExpression)))));

        // Replace the selected expression with the variable reference
        var variableReference = SyntaxFactory.IdentifierName(variableName);
        var newRoot = syntaxRoot.ReplaceNode(selectedExpression, variableReference);

        // Find the containing statement to insert the variable declaration before it
        var containingStatement = selectedExpression.Ancestors().OfType<StatementSyntax>().FirstOrDefault();
        if (containingStatement != null)
        {
            var containingBlock = containingStatement.Parent as BlockSyntax;
            if (containingBlock != null)
            {
                var statementIndex = containingBlock.Statements.IndexOf(containingStatement);
                var newStatements = containingBlock.Statements.Insert(statementIndex, variableDeclaration);
                var newBlock = containingBlock.WithStatements(newStatements);
                newRoot = newRoot.ReplaceNode(containingBlock, newBlock);
            }
        }

        var formattedRoot = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        var newDocument = document.WithSyntaxRoot(formattedRoot);
        var newText = await newDocument.GetTextAsync();
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString());

        return $"Successfully introduced variable '{variableName}' from {selectionRange} in {document.FilePath} (solution mode)";
    }

    private static async Task<string> IntroduceVariableSingleFile(string filePath, string selectionRange, string variableName)
    {
        if (!File.Exists(filePath))
            return ThrowMcpException($"Error: File {filePath} not found (current dir: {Directory.GetCurrentDirectory()})");

        var sourceText = await File.ReadAllTextAsync(filePath);
        var newText = IntroduceVariableInSource(sourceText, selectionRange, variableName);
        await File.WriteAllTextAsync(filePath, newText);

        return $"Successfully introduced variable '{variableName}' from {selectionRange} in {filePath} (single file mode)";
    }

    public static string IntroduceVariableInSource(string sourceText, string selectionRange, string variableName)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = syntaxTree.GetRoot();
        var textLines = SourceText.From(sourceText).Lines;

        if (!TryParseRange(selectionRange, out var startLine, out var startColumn, out var endLine, out var endColumn))
            return ThrowMcpException("Error: Invalid selection range format");

        var startPosition = textLines[startLine - 1].Start + startColumn - 1;
        var endPosition = textLines[endLine - 1].Start + endColumn - 1;
        var span = TextSpan.FromBounds(startPosition, endPosition);

        var selectedExpression = syntaxRoot.DescendantNodes()
            .OfType<ExpressionSyntax>()
            .FirstOrDefault(e => span.Contains(e.Span) || e.Span.Contains(span));

        if (selectedExpression == null)
            return ThrowMcpException("Error: Selected code is not a valid expression");

        var typeName = "var";

        var variableDeclaration = SyntaxFactory.LocalDeclarationStatement(
            SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName(typeName))
                .WithVariables(SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.VariableDeclarator(variableName)
                        .WithInitializer(SyntaxFactory.EqualsValueClause(selectedExpression)))));

        var variableReference = SyntaxFactory.IdentifierName(variableName);
        var containingStatement = selectedExpression.Ancestors().OfType<StatementSyntax>().FirstOrDefault();
        if (containingStatement != null && containingStatement.Parent is BlockSyntax containingBlock)
        {
            var trackedRoot = syntaxRoot.TrackNodes(selectedExpression, containingBlock, containingStatement);
            var replacedRoot = trackedRoot.ReplaceNode(trackedRoot.GetCurrentNode(selectedExpression)!, variableReference);
            var currentBlock = replacedRoot.GetCurrentNode(containingBlock)!;
            var currentStatement = replacedRoot.GetCurrentNode(containingStatement)!;
            var statementIndex = currentBlock.Statements.IndexOf(currentStatement);
            var newStatements = currentBlock.Statements.Insert(statementIndex, variableDeclaration);
            var newBlock = currentBlock.WithStatements(newStatements);
            replacedRoot = replacedRoot.ReplaceNode(currentBlock, newBlock);
            var formattedRoot = Formatter.Format(replacedRoot, SharedWorkspace);
            return formattedRoot.ToFullString();
        }

        var replaced = syntaxRoot.ReplaceNode(selectedExpression, variableReference);
        var formatted = Formatter.Format(replaced, SharedWorkspace);
        return formatted.ToFullString();
    }

}
