using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// What the tools that move or copy an expression need to know about evaluating it:
/// whether it has side effects, whether it is cheap and stable enough to repeat, and
/// whether it always runs when its statement does.
/// </summary>
internal static class ExpressionFacts
{
    /// <summary>
    /// Anything evaluating the expression might do besides produce its value: a call, an
    /// object creation, an assignment, an increment or an await.
    /// </summary>
    public static bool HasSideEffects(ExpressionSyntax expression)
    {
        return expression.DescendantNodesAndSelf().Any(n => n is InvocationExpressionSyntax
            or BaseObjectCreationExpressionSyntax
            or AssignmentExpressionSyntax
            or AwaitExpressionSyntax
            or PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PreIncrementExpression or (int)SyntaxKind.PreDecrementExpression }
            or PostfixUnaryExpressionSyntax);
    }

    /// <summary>A literal, a name, <c>this</c>, or member access on those.</summary>
    public static bool IsSimple(ExpressionSyntax expression) => expression switch
    {
        LiteralExpressionSyntax or IdentifierNameSyntax or ThisExpressionSyntax or PredefinedTypeSyntax => true,
        MemberAccessExpressionSyntax access => IsSimple(access.Expression),
        ParenthesizedExpressionSyntax parenthesized => IsSimple(parenthesized.Expression),
        _ => false,
    };

    /// <summary>
    /// Whether the node runs only on some paths through its statement, or later: on the
    /// right of a short-circuiting operator, in a branch of a conditional, after a null
    /// conditional access, in a switch expression arm, or inside a lambda or local function.
    /// </summary>
    public static bool IsConditionallyEvaluated(SyntaxNode node, StatementSyntax statement)
    {
        for (var child = node; child.Parent != null && child != statement; child = child.Parent)
        {
            var conditional = child.Parent switch
            {
                BinaryExpressionSyntax binary => binary.Right == child && binary.Kind() is
                    SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression or SyntaxKind.CoalesceExpression,
                AssignmentExpressionSyntax assignment => assignment.Right == child && assignment.IsKind(SyntaxKind.CoalesceAssignmentExpression),
                ConditionalExpressionSyntax condition => condition.Condition != child,
                ConditionalAccessExpressionSyntax access => access.WhenNotNull == child,
                SwitchExpressionArmSyntax => true,
                AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax => true,
                _ => false,
            };
            if (conditional)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Whether the node is in a loop condition or increment, which runs on every
    /// iteration rather than once before the loop.
    /// </summary>
    public static bool IsInLoopCondition(SyntaxNode node, StatementSyntax statement) => statement switch
    {
        WhileStatementSyntax whileLoop => whileLoop.Condition.Span.Contains(node.Span),
        DoStatementSyntax doLoop => doLoop.Condition.Span.Contains(node.Span),
        ForStatementSyntax forLoop => (forLoop.Condition?.Span.Contains(node.Span) ?? false) ||
                                      forLoop.Incrementors.Any(i => i.Span.Contains(node.Span)),
        _ => false,
    };
}
