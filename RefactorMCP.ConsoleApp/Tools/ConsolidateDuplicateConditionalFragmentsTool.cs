using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Threading;

[McpServerToolType]
public static class ConsolidateDuplicateConditionalFragmentsTool
{
    [McpServerTool, Description("Move statements that start or end every branch of an if/else chain out of the conditional, before or after it")]
    public static async Task<string> ConsolidateDuplicateConditionalFragments(
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
            if (CaretDocument.Siblings(statement) is not { } siblings)
                throw new McpException("Error: The if statement is not in a block, so no statement can move before or after it");

            var (conditions, branches) = Chain(statement);
            var leading = CommonCount(branches, (b, i) => b[i]);
            var trailing = CommonCount(branches.Select(b => b.Skip(leading).ToList()).ToList(), (b, i) => b[b.Count - 1 - i]);
            if (leading == 0 && trailing == 0)
                throw new McpException("Error: The branches neither start nor end with the same statement, so there are no common fragments to move");

            foreach (var branch in branches)
                EnsureUsesNoBranchLocals(branch.Skip(branch.Count - trailing).ToList(), branch, caret.Model);
            if (leading > 0)
                EnsureConditionsIndependent(branches[0].Take(leading).ToList(), conditions, caret.Model);

            var rebuilt = Rebuild(statement, branch => branch.Skip(leading).Take(branch.Count - leading - trailing));
            var before = branches[0].Take(leading).Select(Moved).ToList();
            var after = branches[0].Skip(branches[0].Count - trailing).Select(Moved).ToList();

            if (before.Count > 0)
            {
                // The moved statements take the if's place, and the if is set apart from them.
                before[0] = before[0].WithLeadingTrivia(LeadingBlankLines(statement).AddRange(HierarchyMemberHelpers.WithoutLeadingBlankLines(before[0].GetLeadingTrivia())));
                rebuilt = CaretDocument.WithBlankLineBefore(CaretDocument.WithoutLeadingBlankLines(rebuilt));
            }

            if (after.Count > 0)
                after[0] = CaretDocument.WithBlankLineBefore(after[0]);

            var index = siblings.IndexOf(statement);
            var statements = siblings.Take(index).Concat(before).Append(rebuilt).Concat(after).Concat(siblings.Skip(index + 1));
            var parent = statement.Parent!;
            SyntaxNode replacement = parent switch
            {
                BlockSyntax block => block.WithStatements(SyntaxFactory.List(statements)),
                SwitchSectionSyntax section => section.WithStatements(SyntaxFactory.List(statements)),
                _ => throw new InvalidOperationException("The if statement's siblings are in neither a block nor a switch section"),
            };

            await caret.ApplyAsync(caret.Root.ReplaceNode(parent, replacement), cancellationToken);
            return $"Successfully moved {leading} statement(s) before and {trailing} after the conditional in {filePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error consolidating duplicate conditional fragments: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// The conditions of the chain and the statements of each branch, the
    /// final else last. Without a final else there is a path on which no
    /// branch runs, and a moved statement would run on it.
    /// </summary>
    private static (List<ExpressionSyntax> Conditions, List<IReadOnlyList<StatementSyntax>> Branches) Chain(IfStatementSyntax statement)
    {
        var conditions = new List<ExpressionSyntax>();
        var branches = new List<IReadOnlyList<StatementSyntax>>();
        for (var current = statement; ; )
        {
            conditions.Add(current.Condition);
            branches.Add(CaretDocument.Statements(current.Statement));
            switch (current.Else?.Statement)
            {
                case null:
                    throw new McpException("Error: The if statement has no final else, so a statement moved out of its branches would also run when none of them does");
                case IfStatementSyntax next:
                    current = next;
                    break;
                case var otherwise:
                    branches.Add(CaretDocument.Statements(otherwise));
                    return (conditions, branches);
            }
        }
    }

    /// <summary>How many statements, counted by <paramref name="at"/>, every branch has in common.</summary>
    private static int CommonCount(
        IReadOnlyList<IReadOnlyList<StatementSyntax>> branches,
        Func<IReadOnlyList<StatementSyntax>, int, StatementSyntax> at)
    {
        var count = 0;
        while (branches.All(b => b.Count > count)
            && branches.Skip(1).All(b => SyntaxFactory.AreEquivalent(at(b, count), at(branches[0], count))))
            count++;
        return count;
    }

    /// <summary>
    /// A statement moved after the conditional can no longer see the locals
    /// its branch declared.
    /// </summary>
    private static void EnsureUsesNoBranchLocals(IReadOnlyList<StatementSyntax> moved, IReadOnlyList<StatementSyntax> branch, SemanticModel model)
    {
        var branchSpan = Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(branch[0].SpanStart, branch[^1].Span.End);
        foreach (var statement in moved)
        {
            var local = statement.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
                .Select(identifier => model.GetSymbolInfo(identifier).Symbol)
                .OfType<ILocalSymbol>()
                .FirstOrDefault(symbol => symbol.DeclaringSyntaxReferences.Any(r =>
                    branchSpan.Contains(r.Span) && !statement.Span.Contains(r.Span)));
            if (local is not null)
                throw new McpException($"Error: '{statement.WithoutTrivia()}' uses the local '{local.Name}' its branch declares, so it cannot move after the conditional");
        }
    }

    /// <summary>
    /// A statement moved before the conditional runs before the conditions
    /// are evaluated, rather than after. That is only the same when the
    /// conditions have no side effects and read nothing the statement changes.
    /// </summary>
    private static void EnsureConditionsIndependent(IReadOnlyList<StatementSyntax> moved, IReadOnlyList<ExpressionSyntax> conditions, SemanticModel model)
    {
        var written = model.AnalyzeDataFlow(moved[0], moved[^1]).WrittenInside;
        var read = conditions
            .SelectMany(c => c.DescendantNodesAndSelf().OfType<SimpleNameSyntax>())
            .Select(name => model.GetSymbolInfo(name).Symbol)
            .Where(symbol => symbol is not null)
            .ToList();
        var readsState = read.Any(symbol => symbol is IFieldSymbol { IsConst: false } or IPropertySymbol or IMethodSymbol { MethodKind: MethodKind.Ordinary } or IEventSymbol);

        var depends = conditions.Any(ExpressionFacts.HasSideEffects)
            || read.Any(symbol => written.Contains(symbol, SymbolEqualityComparer.Default))
            || (readsState && moved.Any(s => ChangesState(s, model)));
        if (depends)
            throw new McpException("Error: The conditions depend on what the common first statements do, so those statements cannot move before them");
    }

    /// <summary>Whether a statement may change anything other than its own locals: a call, a creation, or a write to a non-local.</summary>
    private static bool ChangesState(StatementSyntax statement, SemanticModel model) =>
        statement.DescendantNodesAndSelf().Any(node => node switch
        {
            InvocationExpressionSyntax or BaseObjectCreationExpressionSyntax or AwaitExpressionSyntax => true,
            AssignmentExpressionSyntax assignment => model.GetSymbolInfo(assignment.Left).Symbol is not ILocalSymbol,
            PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PreIncrementExpression or (int)SyntaxKind.PreDecrementExpression } prefix =>
                model.GetSymbolInfo(prefix.Operand).Symbol is not ILocalSymbol,
            PostfixUnaryExpressionSyntax postfix => model.GetSymbolInfo(postfix.Operand).Symbol is not ILocalSymbol,
            _ => false,
        });

    /// <summary>The chain with each branch reduced to the statements <paramref name="keep"/> leaves it.</summary>
    private static IfStatementSyntax Rebuild(IfStatementSyntax statement, Func<IReadOnlyList<StatementSyntax>, IEnumerable<StatementSyntax>> keep)
    {
        var then = Reduced(statement.Statement, keep);
        return statement.Else?.Statement switch
        {
            null => statement.WithStatement(then),
            IfStatementSyntax next => statement.WithStatement(then).WithElse(statement.Else.WithStatement(Rebuild(next, keep))),
            var otherwise => statement.WithStatement(then).WithElse(statement.Else.WithStatement(Reduced(otherwise, keep))),
        };
    }

    private static StatementSyntax Reduced(StatementSyntax branch, Func<IReadOnlyList<StatementSyntax>, IEnumerable<StatementSyntax>> keep)
    {
        var kept = keep(CaretDocument.Statements(branch)).ToList();
        return branch is BlockSyntax block
            ? block.WithStatements(SyntaxFactory.List(kept))
            : CaretDocument.Block(kept).WithTriviaFrom(branch);
    }

    private static StatementSyntax Moved(StatementSyntax statement) =>
        CaretDocument.WithoutLeadingBlankLines(statement).WithAdditionalAnnotations(Formatter.Annotation);

    private static SyntaxTriviaList LeadingBlankLines(StatementSyntax statement)
    {
        var leading = statement.GetLeadingTrivia();
        var lastEndOfLine = -1;
        for (var i = 0; i < leading.Count && (leading[i].IsKind(SyntaxKind.WhitespaceTrivia) || leading[i].IsKind(SyntaxKind.EndOfLineTrivia)); i++)
        {
            if (leading[i].IsKind(SyntaxKind.EndOfLineTrivia))
                lastEndOfLine = i;
        }

        return SyntaxFactory.TriviaList(leading.Take(lastEndOfLine + 1));
    }
}
