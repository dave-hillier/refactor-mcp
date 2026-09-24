using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Threading;

[McpServerToolType]
public static class ReplaceNestedConditionalWithGuardClausesTool
{
    [McpServerTool, Description("Flatten nested if statements into guard clauses: an if ending a method or loop body becomes an early return or continue, and an else after a branch that always jumps away follows the if instead, repeatedly")]
    public static async Task<string> ReplaceNestedConditionalWithGuardClauses(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line of the outermost if keyword (1-based)")] int line,
        [Description("Column of the outermost if keyword (1-based)")] int column,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var caret = await CaretDocument.FindAsync(solutionPath, filePath, line, column, cancellationToken);
            var statement = caret.IfStatement();
            if (statement.Parent is not BlockSyntax block)
                throw new McpException("Error: The if statement is not in a block, so no guard clause can follow it");

            var flattener = new Flattener(caret, InvertIfTool.ImplicitExit(block, caret.Model));
            var index = block.Statements.IndexOf(statement);
            var replacement = flattener.Flatten(statement, endsBlock: index == block.Statements.Count - 1);
            if (flattener.Steps == 0)
            {
                throw new McpException(
                    "Error: The if statement has no else after a branch that always jumps away, and does not end a method or loop body, so it has no guard clause to become");
            }

            var statements = block.Statements.Take(index).Concat(replacement).Concat(block.Statements.Skip(index + 1));
            await caret.ApplyAsync(caret.Root.ReplaceNode(block, block.WithStatements(SyntaxFactory.List(statements))), cancellationToken);
            return $"Successfully replaced the nested conditional with {flattener.Steps} guard clause(s) in {filePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error replacing nested conditional with guard clauses: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Turns an if into a guard clause followed by the statements it guarded,
    /// then does the same to the last of those when it is an if in turn. Every
    /// decision is made on the original statements, whose semantic model is
    /// known; the result is assembled from them.
    /// </summary>
    private sealed class Flattener
    {
        private readonly CaretDocument _caret;
        private readonly StatementSyntax? _exit;

        public Flattener(CaretDocument caret, StatementSyntax? exit)
        {
            _caret = caret;
            _exit = exit;
        }

        public int Steps { get; private set; }

        /// <summary>
        /// The statements that replace <paramref name="statement"/>.
        /// <paramref name="endsBlock"/> says whether nothing follows them in a
        /// method or loop body, so running off the end is the implicit exit.
        /// </summary>
        public IReadOnlyList<StatementSyntax> Flatten(IfStatementSyntax statement, bool endsBlock)
        {
            var then = CaretDocument.Statements(statement.Statement);
            if (statement.Else is { } elseClause)
            {
                var otherwise = CaretDocument.Statements(elseClause.Statement);

                // The else only runs when the branch did not jump away, so it can follow the if.
                if (!_caret.EndPointIsReachable(then))
                    return Guard(statement.WithElse(null), otherwise, endsBlock);

                // Otherwise the else must be the branch that jumps away, and becomes the guard.
                if (!_caret.EndPointIsReachable(otherwise))
                    return Guard(Inverted(statement, otherwise), then, endsBlock);

                return new[] { statement };
            }

            if (endsBlock && _exit is not null)
                return Guard(Inverted(statement, new[] { _exit }), then, endsBlock);

            return new[] { statement };
        }

        private IfStatementSyntax Inverted(IfStatementSyntax statement, IEnumerable<StatementSyntax> branch)
        {
            var condition = BooleanNegation.Negate(statement.Condition, b => BooleanNegation.CanFlip(b, _caret.Model));
            return statement
                .WithCondition(condition)
                .WithStatement(CaretDocument.Block(branch.Select((s, i) => i == 0 ? CaretDocument.WithoutLeadingBlankLines(s) : s))
                    .WithTriviaFrom(statement.Statement))
                .WithElse(null);
        }

        /// <summary>
        /// The guard clause, then the statements it guarded, set apart by a
        /// blank line. The last of those is flattened in turn when it is an if.
        /// </summary>
        private IReadOnlyList<StatementSyntax> Guard(IfStatementSyntax guard, IReadOnlyList<StatementSyntax> following, bool endsBlock)
        {
            Steps++;
            var result = new List<StatementSyntax> { guard };
            for (var i = 0; i < following.Count; i++)
            {
                var flattened = i == following.Count - 1 && following[i] is IfStatementSyntax nested
                    ? Flatten(nested, endsBlock)
                    : new[] { following[i] };

                foreach (var statement in flattened)
                {
                    var moved = statement.WithAdditionalAnnotations(Formatter.Annotation);
                    result.Add(result.Count == 1 ? CaretDocument.WithBlankLineBefore(moved) : moved);
                }
            }

            return result;
        }
    }
}
