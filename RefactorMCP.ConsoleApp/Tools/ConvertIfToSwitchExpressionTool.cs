using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Threading;

[McpServerToolType]
public static class ConvertIfToSwitchExpressionTool
{
    [McpServerTool, Description("Convert an if/else-if chain that compares one value, and whose branches each return a value or assign one variable, into a switch expression")]
    public static async Task<string> ConvertIfToSwitchExpression(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line of the first if keyword (1-based)")] int line,
        [Description("Column of the first if keyword (1-based)")] int column,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var caret = await CaretDocument.FindAsync(solutionPath, filePath, line, column, cancellationToken);
            var statement = caret.IfStatement();

            // Both steps are made in memory, so a chain that becomes a switch
            // statement but not an expression is left as it was.
            var marker = new SyntaxAnnotation();
            var switchStatement = ConvertIfChainToSwitchTool.ToSwitchStatement(statement, caret).WithAdditionalAnnotations(marker);
            var withSwitch = await Formatter.FormatAsync(
                caret.Document.WithSyntaxRoot(caret.Root.ReplaceNode(statement, switchStatement)),
                Formatter.Annotation,
                cancellationToken: cancellationToken);

            // The expression is written in the layout of the formatted statement.
            var root = (await withSwitch.GetSyntaxRootAsync(cancellationToken))!;
            var laidOut = (SwitchStatementSyntax)root.GetAnnotatedNodes(marker).Single();
            await caret.ApplyAsync(ConvertSwitchStatementToExpressionTool.ToSwitchExpression(root, laidOut), cancellationToken);
            return $"Successfully converted the if chain to a switch expression in {filePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error converting if chain to switch expression: {ex.Message}", ex);
        }
    }
}
