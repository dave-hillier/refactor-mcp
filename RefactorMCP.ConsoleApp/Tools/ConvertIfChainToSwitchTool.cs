using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Threading;

[McpServerToolType]
public static class ConvertIfChainToSwitchTool
{
    [McpServerTool, Description("Convert an if/else-if chain that compares one value with constants or patterns into a switch statement")]
    public static async Task<string> ConvertIfChainToSwitch(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line of the first if keyword (1-based)")] int line,
        [Description("Column of the first if keyword (1-based)")] int column,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var caret = await CaretDocument.FindAsync(solutionPath, filePath, line, column, cancellationToken);
            var statement = caret.IfStatement();
            var switchStatement = ToSwitchStatement(statement, caret);
            await caret.ApplyAsync(caret.Root.ReplaceNode(statement, switchStatement), cancellationToken);
            return $"Successfully converted the if chain to a switch statement in {filePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error converting if chain to switch: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// The switch statement an if chain becomes, laid out by the formatter
    /// when it is applied. Convert If to Switch Expression continues from it.
    /// </summary>
    internal static SwitchStatementSyntax ToSwitchStatement(IfStatementSyntax statement, CaretDocument caret)
    {
        var (branches, otherwise) = Chain(statement);
        if (branches.Count + (otherwise is null ? 0 : 1) < 2)
            throw new McpException("Error: The if statement is not part of a chain with at least two cases");

        ExpressionSyntax? subject = null;
        var sections = new List<SwitchSectionSyntax>();
        foreach (var (condition, body) in branches)
        {
            var (tested, labels) = Labels(condition, caret.Model);
            subject ??= tested;
            if (!SyntaxFactory.AreEquivalent(subject, tested))
                throw new McpException($"Error: The conditions do not all compare the same value: '{subject}' and '{tested}'");

            sections.Add(Section(labels, body, caret));
        }

        if (ExpressionFacts.HasSideEffects(subject!))
            throw new McpException($"Error: '{subject}' has side effects, and the chain evaluates it once per comparison where a switch evaluates it once");

        if (otherwise is not null)
            sections.Add(Section(new SwitchLabelSyntax[] { SyntaxFactory.DefaultSwitchLabel() }, otherwise, caret));

        return SyntaxFactory.SwitchStatement(subject!.WithoutTrivia(), SyntaxFactory.List(sections))
            .WithTriviaFrom(statement)
            .WithAdditionalAnnotations(Formatter.Annotation);
    }

    /// <summary>The condition and body of each if in the chain, and the final else's body.</summary>
    private static (List<(ExpressionSyntax Condition, StatementSyntax Body)> Branches, StatementSyntax? Otherwise) Chain(IfStatementSyntax statement)
    {
        var branches = new List<(ExpressionSyntax, StatementSyntax)>();
        for (var current = statement; ; )
        {
            branches.Add((current.Condition, current.Statement));
            switch (current.Else?.Statement)
            {
                case null:
                    return (branches, null);
                case IfStatementSyntax next:
                    current = next;
                    break;
                case var otherwise:
                    return (branches, otherwise);
            }
        }
    }

    /// <summary>
    /// The value a condition tests and the case labels that test it the same
    /// way: <c>x == 1</c> is <c>case 1:</c>, <c>x is T t</c> is <c>case T t:</c>,
    /// an <c>||</c> of those is several labels, and a pattern followed by
    /// <c>&amp;&amp;</c> is a label with a <c>when</c> clause.
    /// </summary>
    private static (ExpressionSyntax Subject, List<SwitchLabelSyntax> Labels) Labels(ExpressionSyntax condition, SemanticModel model)
    {
        switch (WithoutParentheses(condition))
        {
            case BinaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalOrExpression } or:
            {
                var (subject, labels) = Labels(or.Left, model);
                var (right, rightLabels) = Labels(or.Right, model);
                if (!SyntaxFactory.AreEquivalent(subject, right))
                    throw new McpException($"Error: The conditions do not all compare the same value: '{subject}' and '{right}'");
                labels.AddRange(rightLabels);
                if (labels.OfType<CasePatternSwitchLabelSyntax>().Any(l => l.Pattern.DescendantNodes().OfType<SingleVariableDesignationSyntax>().Any()))
                    throw Unsupported(or, "declares a pattern variable in one of several alternatives");
                return (subject, labels);
            }
            case BinaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalAndExpression } and:
            {
                var (subject, labels) = Labels(and.Left, model);
                if (labels is not [var label] || label is CasePatternSwitchLabelSyntax { WhenClause: not null })
                    throw Unsupported(and, "does not test a single pattern before &&");

                var pattern = label switch
                {
                    CaseSwitchLabelSyntax constant => SyntaxFactory.ConstantPattern(constant.Value),
                    CasePatternSwitchLabelSyntax patternLabel => patternLabel.Pattern,
                    _ => throw Unsupported(and, "does not test a single pattern before &&"),
                };
                return (subject, new List<SwitchLabelSyntax>
                {
                    SyntaxFactory.CasePatternSwitchLabel(pattern, SyntaxFactory.WhenClause(and.Right.WithoutTrivia()), SyntaxFactory.Token(SyntaxKind.ColonToken)),
                });
            }
            case BinaryExpressionSyntax { RawKind: (int)SyntaxKind.EqualsExpression } equals:
            {
                var (subject, constant) = IsConstant(equals.Right, model) ? (equals.Left, equals.Right)
                    : IsConstant(equals.Left, model) ? (equals.Right, equals.Left)
                    : throw Unsupported(equals, "does not compare with a constant");
                if (model.GetConstantValue(constant).Value is double.NaN or float.NaN)
                    throw Unsupported(equals, "compares with NaN, which == never matches but a case does");
                if (model.GetSymbolInfo(equals).Symbol is IMethodSymbol { MethodKind: MethodKind.UserDefinedOperator } op
                    && op.ContainingType.SpecialType != SpecialType.System_String)
                    throw Unsupported(equals, "uses a user-defined ==, which a case would not call");
                return (subject.WithoutTrivia(), new List<SwitchLabelSyntax> { SyntaxFactory.CaseSwitchLabel(constant.WithoutTrivia()) });
            }
            case BinaryExpressionSyntax { RawKind: (int)SyntaxKind.IsExpression, Right: TypeSyntax type } isType:
                return (isType.Left.WithoutTrivia(), new List<SwitchLabelSyntax> { PatternLabel(SyntaxFactory.TypePattern(type.WithoutTrivia())) });
            case IsPatternExpressionSyntax isPattern:
                return (isPattern.Expression.WithoutTrivia(), new List<SwitchLabelSyntax>
                {
                    isPattern.Pattern is ConstantPatternSyntax constantPattern
                        ? SyntaxFactory.CaseSwitchLabel(constantPattern.Expression.WithoutTrivia())
                        : PatternLabel(isPattern.Pattern.WithoutTrivia()),
                });
            default:
                throw Unsupported(condition, "is not a comparison with a constant or a pattern test");
        }
    }

    private static SwitchLabelSyntax PatternLabel(PatternSyntax pattern) =>
        SyntaxFactory.CasePatternSwitchLabel(pattern, SyntaxFactory.Token(SyntaxKind.ColonToken));

    private static McpException Unsupported(ExpressionSyntax condition, string reason) =>
        new($"Error: The condition '{condition}' {reason}, so it cannot become a case label");

    private static bool IsConstant(ExpressionSyntax expression, SemanticModel model) =>
        model.GetConstantValue(expression).HasValue;

    /// <summary>
    /// A section running the branch's statements, ending with <c>break</c>
    /// unless they always jump away already. A <c>break</c> in the branch
    /// would leave the switch rather than the loop it was meant for.
    /// </summary>
    private static SwitchSectionSyntax Section(IEnumerable<SwitchLabelSyntax> labels, StatementSyntax body, CaretDocument caret)
    {
        var breaks = body
            .DescendantNodesAndSelf(n => n is not (WhileStatementSyntax or DoStatementSyntax or ForStatementSyntax or CommonForEachStatementSyntax
                or SwitchStatementSyntax or AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
            .OfType<BreakStatementSyntax>();
        if (breaks.Any())
            throw new McpException("Error: A branch contains a break, which in a switch would leave the switch instead of the enclosing loop");

        var statements = CaretDocument.Statements(body).ToList();
        if (caret.EndPointIsReachable(statements))
            statements.Add(SyntaxFactory.BreakStatement());

        return SyntaxFactory.SwitchSection(SyntaxFactory.List(labels), SyntaxFactory.List(statements));
    }

    private static ExpressionSyntax WithoutParentheses(ExpressionSyntax expression) =>
        expression is ParenthesizedExpressionSyntax parenthesized ? WithoutParentheses(parenthesized.Expression) : expression;
}
