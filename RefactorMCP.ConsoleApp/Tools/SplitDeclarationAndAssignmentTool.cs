using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Threading;

[McpServerToolType]
public static class SplitDeclarationAndAssignmentTool
{
    [McpServerTool, Description("Split a local declaration with an initializer into a declaration with an explicit type and an assignment")]
    public static async Task<string> SplitDeclarationAndAssignment(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line of the local's declaration or of a use of it (1-based)")] int line,
        [Description("Column of the local's name on that line (1-based)")] int column,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var target = await LocalVariableTarget.FindAsync(solutionPath, filePath, line, column, cancellationToken);
            var statement = target.DeclarationStatement
                ?? throw new McpException($"Error: '{target.Name}' is not declared by a local declaration statement");

            if (statement.IsConst)
                throw new McpException($"Error: '{target.Name}' is a constant, which cannot be assigned");
            if (statement.UsingKeyword != default)
                throw new McpException($"Error: '{target.Name}' is a using declaration, whose disposal is tied to its initializer");
            if (target.Declaration.Variables.Count > 1)
                throw new McpException("Error: The statement declares several locals; their initializers would run out of order");
            if (target.Declarator.Initializer is not { } initializer)
                throw new McpException($"Error: '{target.Name}' has no initializer to split off");
            if (target.Local.IsRef)
                throw new McpException($"Error: '{target.Name}' is a ref local, which is assigned by reference");
            if (target.SiblingStatements() is not { } statements)
                throw new McpException($"Error: The declaration of '{target.Name}' is not in a block");

            var type = target.Declaration.Type;
            if (type.IsVar)
            {
                if (LocalVariableTarget.IsAnonymous(target.Local.Type))
                    throw new McpException($"Error: '{target.Name}' has an anonymous type, which cannot be named in a declaration");
                type = target.TypeSyntax().WithTriviaFrom(type);
            }

            // An array initializer is only allowed in a declaration, so the assignment
            // creates the array explicitly.
            var value = initializer.Value is InitializerExpressionSyntax arrayInitializer && type is ArrayTypeSyntax arrayType
                ? SyntaxFactory.ArrayCreationExpression(arrayType.WithoutTrivia(), arrayInitializer)
                : initializer.Value;

            var endOfLine = statement.GetTrailingTrivia().Where(t => t.IsKind(SyntaxKind.EndOfLineTrivia));
            var declaration = statement
                .WithDeclaration(target.Declaration
                    .WithType(type)
                    .WithVariables(SyntaxFactory.SingletonSeparatedList(target.Declarator.WithInitializer(null))))
                .WithTrailingTrivia(endOfLine)
                .WithAdditionalAnnotations(Formatter.Annotation);

            // A comment after the declaration describes the value, so it stays with it.
            var assignment = SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        SyntaxFactory.IdentifierName(target.Declarator.Identifier.WithoutTrivia()),
                        value.WithoutTrivia()))
                .WithTrailingTrivia(statement.GetTrailingTrivia())
                .WithAdditionalAnnotations(Formatter.Annotation);

            var index = statements.IndexOf(statement);
            await target.WriteStatementsAsync(statements
                .RemoveAt(index)
                .InsertRange(index, new StatementSyntax[] { declaration, assignment }));

            return $"Successfully split the declaration of '{target.Name}' in {filePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error splitting declaration and assignment: {ex.Message}", ex);
        }
    }
}
