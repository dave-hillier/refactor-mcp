using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Puts an expression where another stood, adding parentheses only when
/// operator precedence would otherwise bind it differently.
/// </summary>
internal static class ExpressionPlacement
{
    private const int Assignment = 1;
    private const int Conditional = 2;
    private const int Unary = 14;
    private const int Primary = 16;

    /// <summary>The replacement as it should be written in place of <paramref name="replaced"/>, with its trivia.</summary>
    public static ExpressionSyntax Fit(ExpressionSyntax replacement, ExpressionSyntax replaced)
    {
        replacement = replacement.WithoutTrivia();
        var fitted = NeedsParentheses(replacement, replaced)
            ? SyntaxFactory.ParenthesizedExpression(replacement)
            : replacement;
        return fitted.WithTriviaFrom(replaced);
    }

    /// <summary>A cast to <paramref name="type"/>, parenthesising the operand when it is not unary or tighter.</summary>
    public static ExpressionSyntax Cast(TypeSyntax type, ExpressionSyntax operand)
    {
        operand = operand.WithoutTrivia();
        return SyntaxFactory.CastExpression(
            type,
            Precedence(operand) >= Unary ? operand : SyntaxFactory.ParenthesizedExpression(operand));
    }

    private static bool NeedsParentheses(ExpressionSyntax replacement, ExpressionSyntax replaced)
    {
        var inner = Precedence(replacement);
        if (inner == Primary)
            return false;

        switch (replaced.Parent)
        {
            case BinaryExpressionSyntax binary:
                var outer = Precedence(binary);
                if (inner != outer)
                    return inner < outer;

                // Binary operators associate to the left, except ??, which associates to the right.
                return binary.IsKind(SyntaxKind.CoalesceExpression) ? binary.Left == replaced : binary.Right == replaced;
            case PrefixUnaryExpressionSyntax or CastExpressionSyntax or AwaitExpressionSyntax:
                return inner < Unary;
            case ConditionalExpressionSyntax conditional:
                return conditional.Condition == replaced ? inner <= Conditional : inner < Conditional;
            case AssignmentExpressionSyntax assignment when assignment.Right == replaced:
                return false;
            case InterpolationSyntax:
                // The colon of a conditional would start a format string.
                return inner <= Conditional;
            case ArgumentSyntax or EqualsValueClauseSyntax or ReturnStatementSyntax or ParenthesizedExpressionSyntax
                or ExpressionStatementSyntax or ArrowExpressionClauseSyntax or IfStatementSyntax or WhileStatementSyntax
                or YieldStatementSyntax or ThrowStatementSyntax or InitializerExpressionSyntax:
                return false;
            default:
                return true;
        }
    }

    private static int Precedence(ExpressionSyntax expression) => expression switch
    {
        BinaryExpressionSyntax binary => binary.Kind() switch
        {
            SyntaxKind.MultiplyExpression or SyntaxKind.DivideExpression or SyntaxKind.ModuloExpression => 13,
            SyntaxKind.AddExpression or SyntaxKind.SubtractExpression => 12,
            SyntaxKind.LeftShiftExpression or SyntaxKind.RightShiftExpression or SyntaxKind.UnsignedRightShiftExpression => 11,
            SyntaxKind.LessThanExpression or SyntaxKind.GreaterThanExpression or SyntaxKind.LessThanOrEqualExpression
                or SyntaxKind.GreaterThanOrEqualExpression or SyntaxKind.IsExpression or SyntaxKind.AsExpression => 10,
            SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression => 9,
            SyntaxKind.BitwiseAndExpression => 8,
            SyntaxKind.ExclusiveOrExpression => 7,
            SyntaxKind.BitwiseOrExpression => 6,
            SyntaxKind.LogicalAndExpression => 5,
            SyntaxKind.LogicalOrExpression => 4,
            SyntaxKind.CoalesceExpression => 3,
            _ => 0,
        },
        IsPatternExpressionSyntax => 10,
        PrefixUnaryExpressionSyntax or CastExpressionSyntax or AwaitExpressionSyntax => Unary,
        ConditionalAccessExpressionSyntax => Unary + 1,
        ConditionalExpressionSyntax => Conditional,
        AssignmentExpressionSyntax or LambdaExpressionSyntax => Assignment,
        IdentifierNameSyntax or GenericNameSyntax or QualifiedNameSyntax or AliasQualifiedNameSyntax or PredefinedTypeSyntax
            or LiteralExpressionSyntax or MemberAccessExpressionSyntax or InvocationExpressionSyntax
            or ElementAccessExpressionSyntax or ParenthesizedExpressionSyntax or ThisExpressionSyntax
            or BaseExpressionSyntax or BaseObjectCreationExpressionSyntax or InterpolatedStringExpressionSyntax
            or TypeOfExpressionSyntax or DefaultExpressionSyntax or CheckedExpressionSyntax or PostfixUnaryExpressionSyntax
            or TupleExpressionSyntax or ArrayCreationExpressionSyntax or ImplicitArrayCreationExpressionSyntax
            or SizeOfExpressionSyntax => Primary,
        _ => 0,
    };
}

/// <summary>Comparing the constant values calls pass.</summary>
internal static class ConstantValues
{
    /// <summary>
    /// Whether two constants are the same value, treating numbers of
    /// different types as equal when they are numerically equal, as they are
    /// once converted to a parameter's type.
    /// </summary>
    public static bool Same(object? left, object? right)
    {
        if (Equals(left, right))
            return true;

        if (!IsNumber(left) || !IsNumber(right))
            return false;

        try
        {
            return Convert.ToDecimal(left) == Convert.ToDecimal(right);
        }
        catch (OverflowException)
        {
            return Convert.ToDouble(left).Equals(Convert.ToDouble(right));
        }
    }

    private static bool IsNumber(object? value) =>
        value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal or char;
}
