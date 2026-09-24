using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using System.Threading;

[McpServerToolType]
public static class ConvertSwitchExpressionToStatementTool
{
    [McpServerTool, Description("Convert a switch expression that is returned, assigned or used to initialise a local into a switch statement")]
    public static async Task<string> ConvertSwitchExpressionToStatement(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line of the switch keyword (1-based)")] int line,
        [Description("Column of the switch keyword (1-based)")] int column,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var caret = await CaretDocument.FindAsync(solutionPath, filePath, line, column, cancellationToken);
            var expression = caret.SwitchExpression();
            var (statement, declaration, produce) = Context(expression, caret.Model);

            var sections = expression.Arms.Select(arm => Section(arm, produce)).ToList();
            if (!expression.Arms.Any(arm => arm.Pattern is DiscardPatternSyntax && arm.WhenClause is null))
                sections.Add(Unmatched(expression.GoverningExpression));

            var switchStatement = SyntaxFactory.SwitchStatement(expression.GoverningExpression.WithoutTrivia(), SyntaxFactory.List(sections))
                .WithAdditionalAnnotations(Formatter.Annotation);

            SyntaxNode newRoot;
            if (declaration is null)
            {
                newRoot = caret.Root.ReplaceNode(statement, switchStatement.WithTriviaFrom(statement));
            }
            else
            {
                var indentation = statement.GetLeadingTrivia().Where(t => t.IsKind(SyntaxKind.WhitespaceTrivia)).TakeLast(1);
                newRoot = caret.Root.ReplaceNode(statement, new SyntaxNode[]
                {
                    declaration,
                    switchStatement.WithLeadingTrivia(indentation).WithTrailingTrivia(statement.GetTrailingTrivia()),
                });
            }

            await caret.ApplyAsync(newRoot, cancellationToken);
            return $"Successfully converted the switch expression to a switch statement in {filePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error converting switch expression to statement: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// The statement the switch expression is the whole value of, a
    /// declaration of the local it initialised when there is one, and how each
    /// section should produce an arm's value.
    /// </summary>
    private static (StatementSyntax Statement, LocalDeclarationStatementSyntax? Declaration, Func<ExpressionSyntax, StatementSyntax[]> Produce) Context(
        SwitchExpressionSyntax expression,
        SemanticModel model)
    {
        switch (expression.Parent)
        {
            case ReturnStatementSyntax returned:
                return (returned, null, value => new StatementSyntax[] { SyntaxFactory.ReturnStatement(value) });
            case AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.SimpleAssignmentExpression, Parent: ExpressionStatementSyntax assigned } assignment
                when assignment.Right == expression:
                return (assigned, null, value => Assign(assignment.Left.WithoutTrivia(), value));
            case EqualsValueClauseSyntax
            {
                Parent: VariableDeclaratorSyntax declarator,
                Parent.Parent: VariableDeclarationSyntax { Variables.Count: 1, Parent: LocalDeclarationStatementSyntax local } variables,
            } when local.Modifiers.Count == 0 && local.UsingKeyword == default:
            {
                var type = variables.Type;
                if (type.IsVar)
                {
                    var symbol = (ILocalSymbol)model.GetDeclaredSymbol(declarator)!;
                    if (symbol.Type.IsAnonymousType)
                        throw new McpException($"Error: '{symbol.Name}' has an anonymous type, which cannot be named in a declaration");
                    type = SyntaxFactory.ParseTypeName(symbol.Type.ToMinimalDisplayString(model, declarator.SpanStart)).WithTriviaFrom(type);
                }

                var declaration = local.WithDeclaration(variables
                        .WithType(type)
                        .WithVariables(SyntaxFactory.SingletonSeparatedList(declarator.WithInitializer(null).WithoutTrailingTrivia())))
                    .WithTrailingTrivia(local.GetTrailingTrivia().Where(t => t.IsKind(SyntaxKind.EndOfLineTrivia)));
                var name = SyntaxFactory.IdentifierName(declarator.Identifier.WithoutTrivia());
                return (local, declaration, value => Assign(name, value));
            }
            default:
                throw new McpException("Error: The switch expression is not returned, assigned or used to initialise a local, so it has no statement to become");
        }
    }

    private static StatementSyntax[] Assign(ExpressionSyntax target, ExpressionSyntax value) => new StatementSyntax[]
    {
        SyntaxFactory.ExpressionStatement(SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, target, value)),
        SyntaxFactory.BreakStatement(),
    };

    /// <summary>
    /// A section for one arm. A discard becomes <c>default</c>, the
    /// alternatives of an <c>or</c> pattern become separate labels, and a
    /// throw expression becomes a throw statement.
    /// </summary>
    private static SwitchSectionSyntax Section(SwitchExpressionArmSyntax arm, Func<ExpressionSyntax, StatementSyntax[]> produce)
    {
        var value = arm.Expression.WithoutTrivia();
        var statements = value is ThrowExpressionSyntax thrown
            ? new StatementSyntax[] { SyntaxFactory.ThrowStatement(thrown.Expression) }
            : produce(value);

        var comments = arm.GetLeadingTrivia().Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) || t.IsKind(SyntaxKind.MultiLineCommentTrivia));
        var labels = Labels(arm).ToList();
        labels[0] = labels[0].WithLeadingTrivia(comments.SelectMany(c => new[] { c, SyntaxFactory.ElasticCarriageReturnLineFeed }));
        return SyntaxFactory.SwitchSection(SyntaxFactory.List(labels), SyntaxFactory.List(statements));
    }

    private static IEnumerable<SwitchLabelSyntax> Labels(SwitchExpressionArmSyntax arm)
    {
        var pattern = arm.Pattern.WithoutTrivia();
        var when = arm.WhenClause?.WithoutTrivia();
        if (pattern is DiscardPatternSyntax)
        {
            return new[]
            {
                when is null
                    ? (SwitchLabelSyntax)SyntaxFactory.DefaultSwitchLabel()
                    : SyntaxFactory.CasePatternSwitchLabel(SyntaxFactory.VarPattern(SyntaxFactory.DiscardDesignation()), when, SyntaxFactory.Token(SyntaxKind.ColonToken)),
            };
        }

        var alternatives = when is null && !pattern.DescendantNodes().OfType<SingleVariableDesignationSyntax>().Any()
            ? Alternatives(pattern)
            : new[] { pattern };
        return alternatives.Select(alternative => alternative is ConstantPatternSyntax constant && when is null
            ? (SwitchLabelSyntax)SyntaxFactory.CaseSwitchLabel(constant.Expression)
            : SyntaxFactory.CasePatternSwitchLabel(alternative, when, SyntaxFactory.Token(SyntaxKind.ColonToken)));
    }

    private static IEnumerable<PatternSyntax> Alternatives(PatternSyntax pattern) => pattern switch
    {
        BinaryPatternSyntax { RawKind: (int)SyntaxKind.OrPattern } or => Alternatives(or.Left).Concat(Alternatives(or.Right)),
        ParenthesizedPatternSyntax parenthesized => Alternatives(parenthesized.Pattern),
        _ => new[] { pattern.WithoutTrivia() },
    };

    /// <summary>
    /// A switch expression with no discard arm throws for a value no arm
    /// matches, so the statement's default section throws the same exception.
    /// </summary>
    private static SwitchSectionSyntax Unmatched(ExpressionSyntax governing)
    {
        var exception = SyntaxFactory.ParseTypeName("System.Runtime.CompilerServices.SwitchExpressionException")
            .WithAdditionalAnnotations(Simplifier.Annotation, Simplifier.AddImportsAnnotation);
        var arguments = ExpressionFacts.IsSimple(governing)
            ? SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(governing.WithoutTrivia())))
            : SyntaxFactory.ArgumentList();
        return SyntaxFactory.SwitchSection(
            SyntaxFactory.SingletonList<SwitchLabelSyntax>(SyntaxFactory.DefaultSwitchLabel()),
            SyntaxFactory.SingletonList<StatementSyntax>(SyntaxFactory.ThrowStatement(SyntaxFactory.ObjectCreationExpression(exception, arguments, null))));
    }
}
