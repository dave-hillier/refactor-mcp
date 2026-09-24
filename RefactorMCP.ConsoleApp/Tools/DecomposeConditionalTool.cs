using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

[McpServerToolType]
public static class DecomposeConditionalTool
{
    [McpServerTool, Description("Extract the condition of an if statement and each of its branches into methods of their own")]
    public static async Task<string> DecomposeConditional(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line of the if keyword (1-based)")] int line,
        [Description("Column of the if keyword (1-based)")] int column,
        [Description("Name of the method that evaluates the condition")] string conditionName,
        [Description("Name of the method for the statements run when the condition holds")] string thenName,
        [Description("Name of the method for the else branch; leave out to keep the else branch as it is")] string? elseName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var document = RefactoringHelpers.GetDocumentByPath(solution, filePath)
                ?? throw new McpException($"Error: File {filePath} not found in solution");
            var original = document;

            var mark = new SyntaxAnnotation();
            var ifStatement = await IfStatementAtAsync(document, line, column, cancellationToken);
            if (elseName != null && ifStatement.Else == null)
                throw new McpException("Error: The if statement has no else branch to extract");
            if (elseName != null && ifStatement.Else!.Statement is IfStatementSyntax)
                throw new McpException("Error: The else branch is another if statement; decompose it on its own");

            var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
            document = document.WithSyntaxRoot(root.ReplaceNode(ifStatement, ifStatement.WithAdditionalAnnotations(mark)));

            // Each new method is placed straight after the containing method, so the
            // branches go first and the methods end up in the order they are read.
            if (elseName != null)
                document = await ExtractAsync(document, mark, s => BranchSpan(s.Else!.Statement), elseName);
            document = await ExtractAsync(document, mark, s => BranchSpan(s.Statement), thenName);
            document = await ExtractAsync(document, mark, s => s.Condition.Span, conditionName);

            await RefactoringHelpers.WriteAndUpdateCachesAsync(original, (await document.GetSyntaxRootAsync(cancellationToken))!);
            return $"Successfully decomposed the conditional at {line}:{column} in {filePath}";
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error decomposing conditional: {ex.Message}", ex);
        }
    }

    private static async Task<IfStatementSyntax> IfStatementAtAsync(Document document, int line, int column, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken);
        if (line < 1 || line > text.Lines.Count || column < 1)
            throw new McpException($"Error: {line}:{column} is outside {document.FilePath}");

        var position = text.Lines[line - 1].Start + column - 1;
        var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
        return root.FindToken(position).Parent as IfStatementSyntax
            ?? throw new McpException($"Error: There is no if statement at {line}:{column}");
    }

    /// <summary>Extracts part of the marked if statement, as Extract Method does.</summary>
    private static async Task<Document> ExtractAsync(
        Document document,
        SyntaxAnnotation mark,
        Func<IfStatementSyntax, TextSpan> part,
        string name)
    {
        var root = (await document.GetSyntaxRootAsync())!;
        var ifStatement = (IfStatementSyntax)root.GetAnnotatedNodes(mark).Single();
        var extracted = await ExtractMethodTool.ExtractAsync(document, part(ifStatement), name);
        return document.WithSyntaxRoot(extracted);
    }

    /// <summary>The statements a branch runs: those inside its braces, or the statement itself.</summary>
    private static TextSpan BranchSpan(StatementSyntax branch)
    {
        if (branch is not BlockSyntax block)
            return branch.Span;

        if (block.Statements.Count == 0)
            throw new McpException("Error: A branch of the if statement is empty, so there is nothing to extract");

        return TextSpan.FromBounds(block.Statements.First().SpanStart, block.Statements.Last().Span.End);
    }
}
