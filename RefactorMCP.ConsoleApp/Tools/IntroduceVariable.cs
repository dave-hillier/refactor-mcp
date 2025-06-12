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
    public static async Task<string> IntroduceVariable(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Range in format 'startLine:startColumn-endLine:endColumn'")] string selectionRange,
        [Description("Name for the new variable")] string variableName)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath);
            var document = RefactoringHelpers.GetDocumentByPath(solution, filePath);
            if (document != null)
                return await IntroduceVariableWithSolution(document, selectionRange, variableName);

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

        var nodesToReplace = syntaxRoot.DescendantNodes()
            .OfType<ExpressionSyntax>()
            .Where(e => SyntaxFactory.AreEquivalent(e, selectedExpression))
            .ToList();

        var containingStatement = selectedExpression.Ancestors().OfType<StatementSyntax>().FirstOrDefault();
        SyntaxNode newRoot;
        if (containingStatement != null && containingStatement.Parent is BlockSyntax containingBlock)
        {
            var nodesToTrack = nodesToReplace.Cast<SyntaxNode>().Append(containingBlock).Append(containingStatement);
            var trackedRoot = syntaxRoot.TrackNodes(nodesToTrack);
            var replacedRoot = trackedRoot.ReplaceNodes(nodesToReplace.Select(n => trackedRoot.GetCurrentNode(n)!), (_1, _2) => variableReference);
            var currentBlock = replacedRoot.GetCurrentNode(containingBlock)!;
            var currentStatement = replacedRoot.GetCurrentNode(containingStatement)!;
            var statementIndex = currentBlock.Statements.IndexOf(currentStatement);
            var newStatements = currentBlock.Statements.Insert(statementIndex, variableDeclaration);
            var newBlock = currentBlock.WithStatements(newStatements);
            newRoot = replacedRoot.ReplaceNode(currentBlock, newBlock);
        }
        else
        {
            newRoot = syntaxRoot.ReplaceNodes(nodesToReplace, (_1, _2) => variableReference);
        }

        var formattedRoot = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        var newDocument = document.WithSyntaxRoot(formattedRoot);
        var newText = await newDocument.GetTextAsync();
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString());

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
        var nodesToReplace = syntaxRoot.DescendantNodes()
            .OfType<ExpressionSyntax>()
            .Where(e => SyntaxFactory.AreEquivalent(e, selectedExpression))
            .ToList();
        var containingStatement = selectedExpression.Ancestors().OfType<StatementSyntax>().FirstOrDefault();
        SyntaxNode newRoot;
        if (containingStatement != null && containingStatement.Parent is BlockSyntax containingBlock)
        {
            var nodesToTrack = nodesToReplace.Cast<SyntaxNode>().Append(containingBlock).Append(containingStatement);
            var trackedRoot = syntaxRoot.TrackNodes(nodesToTrack);
            var replacedRoot = trackedRoot.ReplaceNodes(nodesToReplace.Select(n => trackedRoot.GetCurrentNode(n)!), (_1, _2) => variableReference);
            var currentBlock = replacedRoot.GetCurrentNode(containingBlock)!;
            var currentStatement = replacedRoot.GetCurrentNode(containingStatement)!;
            var statementIndex = currentBlock.Statements.IndexOf(currentStatement);
            var newStatements = currentBlock.Statements.Insert(statementIndex, variableDeclaration);
            var newBlock = currentBlock.WithStatements(newStatements);
            newRoot = replacedRoot.ReplaceNode(currentBlock, newBlock);
        }
        else
        {
            newRoot = syntaxRoot.ReplaceNodes(nodesToReplace, (_1, _2) => variableReference);
        }

        var formattedRoot = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
        return formattedRoot.ToFullString();
    }

}
