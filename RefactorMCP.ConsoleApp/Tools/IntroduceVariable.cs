using ModelContextProtocol.Server;
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
            return $"Error introducing variable: {ex.Message}";
        }
    }

    private static async Task<string> IntroduceVariableWithSolution(Document document, string selectionRange, string variableName)
    {
        var sourceText = await document.GetTextAsync();
        var syntaxRoot = await document.GetSyntaxRootAsync();

        if (!TryParseRange(selectionRange, out var startLine, out var startColumn, out var endLine, out var endColumn))
            return "Error: Invalid selection range format";

        var startPosition = sourceText.Lines[startLine - 1].Start + startColumn - 1;
        var endPosition = sourceText.Lines[endLine - 1].Start + endColumn - 1;
        var span = TextSpan.FromBounds(startPosition, endPosition);

        var selectedExpression = syntaxRoot!.DescendantNodes()
            .OfType<ExpressionSyntax>()
            .FirstOrDefault(e => span.Contains(e.Span) || e.Span.Contains(span));

        if (selectedExpression == null)
            return "Error: Selected code is not a valid expression";

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
            return $"Error: File {filePath} not found";

        var sourceText = await File.ReadAllTextAsync(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = await syntaxTree.GetRootAsync();
        var textLines = SourceText.From(sourceText).Lines;

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

        // In single file mode, use 'var' for type since we don't have semantic analysis
        var typeName = "var";

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

        // Format and write back to file
        var workspace = new AdhocWorkspace();
        var formattedRoot = Formatter.Format(newRoot, workspace);
        await File.WriteAllTextAsync(filePath, formattedRoot.ToFullString());

        return $"Successfully introduced variable '{variableName}' from {selectionRange} in {filePath} (single file mode)";
    }

}
