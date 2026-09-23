using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Threading;

[McpServerToolType]
public static class JoinDeclarationAndAssignmentTool
{
    [McpServerTool, Description("Join a local declaration without an initializer and its first assignment into one declaration")]
    public static async Task<string> JoinDeclarationAndAssignment(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line of the local's declaration or of a use of it (1-based)")] int line,
        [Description("Column of the local's name on that line (1-based)")] int column,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var target = await LocalVariableTarget.FindAsync(solutionPath, filePath, line, column, cancellationToken);
            var declaration = target.DeclarationStatement
                ?? throw new McpException($"Error: '{target.Name}' is not declared by a local declaration statement");

            if (target.Declaration.Variables.Count > 1)
                throw new McpException("Error: The statement declares several locals; split it into one declaration per local first");
            if (target.Declarator.Initializer != null)
                throw new McpException($"Error: '{target.Name}' already has an initializer");
            if (target.SiblingStatements() is not { } statements)
                throw new McpException($"Error: The declaration of '{target.Name}' is not in a block");

            var declarationIndex = statements.IndexOf(declaration);
            var firstUse = statements.Skip(declarationIndex + 1).FirstOrDefault(target.Mentions)
                ?? throw new McpException($"Error: '{target.Name}' is never assigned");

            if (firstUse is not ExpressionStatementSyntax
                {
                    Expression: AssignmentExpressionSyntax
                    {
                        RawKind: (int)SyntaxKind.SimpleAssignmentExpression,
                        Left: IdentifierNameSyntax left,
                    } assignment,
                } || !target.IsReference(left) || target.Mentions(assignment.Right))
            {
                var useLine = firstUse.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                throw new McpException(
                    $"Error: The first use of '{target.Name}' after its declaration, at line {useLine}, is not an assignment to it in the same block");
            }

            // The declaration's comments describe the local, so they move with it, below
            // whatever separated the assignment from the statement before it.
            var declarationLeading = declaration.GetLeadingTrivia();
            var blankLines = declarationLeading.TakeWhile(IsWhitespace).Where(t => t.IsKind(SyntaxKind.EndOfLineTrivia)).ToList();
            var comments = declarationLeading.SkipWhile(IsWhitespace);
            var trailing = firstUse.GetTrailingTrivia().Any(IsComment)
                ? firstUse.GetTrailingTrivia()
                : declaration.GetTrailingTrivia();

            var joined = declaration
                .WithDeclaration(target.Declaration.WithVariables(SyntaxFactory.SingletonSeparatedList(
                    target.Declarator.WithInitializer(SyntaxFactory.EqualsValueClause(assignment.Right.WithoutTrivia())))))
                .WithLeadingTrivia(firstUse.GetLeadingTrivia().AddRange(comments))
                .WithTrailingTrivia(trailing)
                .WithAdditionalAnnotations(Formatter.Annotation);

            var updated = statements.Replace(firstUse, joined).RemoveAt(declarationIndex);

            // A blank line that set the declaration apart now sets apart what follows it.
            if (blankLines.Count > 0 && declarationIndex < updated.Count)
            {
                var next = updated[declarationIndex];
                if (!next.GetLeadingTrivia().Any(SyntaxKind.EndOfLineTrivia))
                    updated = updated.Replace(next, next.WithLeadingTrivia(next.GetLeadingTrivia().InsertRange(0, blankLines)));
            }

            await target.WriteStatementsAsync(updated);

            return $"Successfully joined the declaration of '{target.Name}' with its assignment in {filePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error joining declaration and assignment: {ex.Message}", ex);
        }
    }

    private static bool IsWhitespace(SyntaxTrivia trivia) =>
        trivia.IsKind(SyntaxKind.WhitespaceTrivia) || trivia.IsKind(SyntaxKind.EndOfLineTrivia);

    private static bool IsComment(SyntaxTrivia trivia) =>
        trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia);
}
