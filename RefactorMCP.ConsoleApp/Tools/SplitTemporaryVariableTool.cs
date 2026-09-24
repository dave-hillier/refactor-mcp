using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Threading;

[McpServerToolType]
public static class SplitTemporaryVariableTool
{
    [McpServerTool, Description("Give a local that is reassigned for an unrelated purpose a new local from that assignment on")]
    public static async Task<string> SplitTemporaryVariable(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line of the assignment to split at (1-based)")] int line,
        [Description("Column of the assigned local's name on that line (1-based)")] int column,
        [Description("Name for the new local")] string name,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var target = await LocalVariableTarget.FindAsync(solutionPath, filePath, line, column, cancellationToken);
            var declaration = target.DeclarationStatement
                ?? throw new McpException($"Error: '{target.Name}' is not declared by a local declaration statement");

            if (target.AtCaret?.Parent is not AssignmentExpressionSyntax assignment ||
                assignment.Left != target.AtCaret ||
                assignment.Parent is not ExpressionStatementSyntax assignmentStatement)
            {
                throw new McpException($"Error: The caret is not on an assignment to '{target.Name}'");
            }

            if (!assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) || target.Mentions(assignment.Right))
                throw new McpException($"Error: The assignment reads '{target.Name}', so its value is related to the one before it");
            if (assignmentStatement.Parent != declaration.Parent)
            {
                throw new McpException(
                    $"Error: The assignment is in a block nested inside the one declaring '{target.Name}', so code after that block could read either value");
            }

            var references = target.References().ToList();
            var captured = references.FirstOrDefault(r => r.Ancestors()
                .Any(a => a is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax && !a.Span.Contains(declaration.Span)));
            if (captured != null)
                throw new McpException($"Error: '{target.Name}' is captured by a lambda or local function, which could read it after the assignment");

            EnsureNameIsFree(target, assignmentStatement, name);

            var newDeclaration = SyntaxFactory.LocalDeclarationStatement(
                    SyntaxFactory.VariableDeclaration(
                        DeclaredType(target, assignment.Right),
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier(name))
                                .WithInitializer(SyntaxFactory.EqualsValueClause(assignment.Right.WithoutTrivia())))))
                .WithTriviaFrom(assignmentStatement)
                .WithAdditionalAnnotations(Formatter.Annotation);

            var editor = await target.EditorAsync();
            editor.ReplaceNode(assignmentStatement, newDeclaration);
            foreach (var reference in references.Where(r => r.SpanStart > assignmentStatement.Span.End))
                editor.ReplaceNode(reference, SyntaxFactory.IdentifierName(name).WithTriviaFrom(reference));

            await target.WriteAsync(editor);

            return $"Successfully split '{target.Name}' into '{name}' from line {line} in {filePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error splitting temporary variable: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// The new local is declared the way the old one was. A var local stays var only
    /// when the new value has the local's type by itself.
    /// </summary>
    private static TypeSyntax DeclaredType(LocalVariableTarget target, ExpressionSyntax value)
    {
        var written = target.Declaration.Type;
        if (!written.IsVar)
            return written.WithoutTrivia();

        var valueType = target.Model.GetTypeInfo(value).Type;
        return valueType != null && SymbolEqualityComparer.Default.Equals(valueType, target.Local.Type)
            ? SyntaxFactory.IdentifierName("var")
            : target.TypeSyntax();
    }

    /// <summary>
    /// The new name must not be anything the assignment could already see, nor a local
    /// or parameter declared anywhere in the member, which would clash with it.
    /// </summary>
    private static void EnsureNameIsFree(LocalVariableTarget target, StatementSyntax assignment, string name)
    {
        var visible = target.Model.LookupSymbols(assignment.SpanStart, name: name);
        var declared = target.EnclosingMember().DescendantNodesAndSelf()
            .Any(n => n switch
            {
                VariableDeclaratorSyntax v => v.Identifier.ValueText == name,
                ParameterSyntax p => p.Identifier.ValueText == name,
                SingleVariableDesignationSyntax d => d.Identifier.ValueText == name,
                ForEachStatementSyntax f => f.Identifier.ValueText == name,
                _ => false,
            });

        if (!visible.IsEmpty || declared)
            throw new McpException($"Error: '{name}' is already declared in the scope of '{target.Name}'");
    }
}
