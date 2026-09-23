using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Writes the logical negation of a boolean expression in its most direct
/// form: comparisons flip, <c>!x</c> loses its <c>!</c>, <c>&amp;&amp;</c> and
/// <c>||</c> follow De Morgan's laws, and patterns gain or lose <c>not</c>.
/// </summary>
internal static class BooleanNegation
{
    /// <summary>
    /// The negation of <paramref name="expression"/>, with its trivia, ready
    /// to be fitted where it stood. <paramref name="canFlip"/> says whether a
    /// comparison may be negated by flipping its operator.
    /// </summary>
    public static ExpressionSyntax Negate(ExpressionSyntax expression, Func<BinaryExpressionSyntax, bool> canFlip) =>
        NegateCore(expression, canFlip).WithTriviaFrom(expression);

    /// <summary>
    /// Whether flipping the operator of a comparison negates it. Equality
    /// always does. An ordering does only for the built-in operators on
    /// integral, character, decimal and enum values: a NaN or a null makes
    /// both <c>a &lt; b</c> and <c>a &gt;= b</c> false, and a user-defined
    /// ordering need not be total.
    /// </summary>
    public static bool CanFlip(BinaryExpressionSyntax comparison, SemanticModel model)
    {
        if (comparison.Kind() is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression)
            return true;

        if (model.GetSymbolInfo(comparison).Symbol is not IMethodSymbol { MethodKind: MethodKind.BuiltinOperator })
            return false;

        return IsTotallyOrdered(model.GetTypeInfo(comparison.Left).ConvertedType)
            && IsTotallyOrdered(model.GetTypeInfo(comparison.Right).ConvertedType);
    }

    /// <summary><c>!</c> applied to the expression, parenthesising it when it is not unary or tighter.</summary>
    public static ExpressionSyntax Not(ExpressionSyntax expression)
    {
        var not = SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, expression.WithoutTrivia());
        return not.ReplaceNode(not.Operand, ExpressionPlacement.Fit(not.Operand, not.Operand)).WithTriviaFrom(expression);
    }

    private static ExpressionSyntax NegateCore(ExpressionSyntax expression, Func<BinaryExpressionSyntax, bool> canFlip)
    {
        switch (expression)
        {
            case ParenthesizedExpressionSyntax parenthesized:
                return NegateCore(parenthesized.Expression, canFlip);
            case PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } not:
                return WithoutParentheses(not.Operand);
            case LiteralExpressionSyntax { RawKind: (int)SyntaxKind.TrueLiteralExpression }:
                return SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression);
            case LiteralExpressionSyntax { RawKind: (int)SyntaxKind.FalseLiteralExpression }:
                return SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression);
            case BinaryExpressionSyntax binary when binary.Kind() is SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression:
                return DeMorgan(binary, canFlip);
            case BinaryExpressionSyntax binary when Flipped(binary.Kind()) is { } flipped && canFlip(binary):
                return SyntaxFactory.BinaryExpression(
                    flipped.Expression,
                    binary.Left,
                    SyntaxFactory.Token(flipped.Operator).WithTriviaFrom(binary.OperatorToken),
                    binary.Right);
            case BinaryExpressionSyntax { RawKind: (int)SyntaxKind.IsExpression } isType when isType.Right is TypeSyntax type:
                return SyntaxFactory.IsPatternExpression(
                    isType.Left,
                    isType.OperatorToken,
                    NotPattern(SyntaxFactory.TypePattern(type)));
            case IsPatternExpressionSyntax isPattern:
                return isPattern.WithPattern(isPattern.Pattern is UnaryPatternSyntax { RawKind: (int)SyntaxKind.NotPattern } notPattern
                    ? notPattern.Pattern.WithTriviaFrom(isPattern.Pattern)
                    : NotPattern(isPattern.Pattern));
            default:
                return Not(expression);
        }
    }

    private static ExpressionSyntax DeMorgan(BinaryExpressionSyntax binary, Func<BinaryExpressionSyntax, bool> canFlip)
    {
        var and = binary.IsKind(SyntaxKind.LogicalOrExpression);
        var negated = SyntaxFactory.BinaryExpression(
            and ? SyntaxKind.LogicalAndExpression : SyntaxKind.LogicalOrExpression,
            NegateCore(binary.Left, canFlip).WithTriviaFrom(binary.Left),
            SyntaxFactory.Token(and ? SyntaxKind.AmpersandAmpersandToken : SyntaxKind.BarBarToken).WithTriviaFrom(binary.OperatorToken),
            NegateCore(binary.Right, canFlip).WithTriviaFrom(binary.Right));

        return negated.ReplaceNodes(
            new[] { negated.Left, negated.Right },
            (original, _) => ExpressionPlacement.Fit(original, original));
    }

    private static PatternSyntax NotPattern(PatternSyntax pattern)
    {
        // not binds tighter than and and or.
        var operand = pattern is BinaryPatternSyntax
            ? SyntaxFactory.ParenthesizedPattern(pattern.WithoutTrivia())
            : pattern.WithoutTrivia();
        return SyntaxFactory.UnaryPattern(SyntaxFactory.Token(SyntaxKind.NotKeyword).WithTrailingTrivia(SyntaxFactory.Space), operand)
            .WithTriviaFrom(pattern);
    }

    private static ExpressionSyntax WithoutParentheses(ExpressionSyntax expression) =>
        expression is ParenthesizedExpressionSyntax parenthesized ? WithoutParentheses(parenthesized.Expression) : expression;

    private static (SyntaxKind Expression, SyntaxKind Operator)? Flipped(SyntaxKind kind) => kind switch
    {
        SyntaxKind.EqualsExpression => (SyntaxKind.NotEqualsExpression, SyntaxKind.ExclamationEqualsToken),
        SyntaxKind.NotEqualsExpression => (SyntaxKind.EqualsExpression, SyntaxKind.EqualsEqualsToken),
        SyntaxKind.LessThanExpression => (SyntaxKind.GreaterThanOrEqualExpression, SyntaxKind.GreaterThanEqualsToken),
        SyntaxKind.LessThanOrEqualExpression => (SyntaxKind.GreaterThanExpression, SyntaxKind.GreaterThanToken),
        SyntaxKind.GreaterThanExpression => (SyntaxKind.LessThanOrEqualExpression, SyntaxKind.LessThanEqualsToken),
        SyntaxKind.GreaterThanOrEqualExpression => (SyntaxKind.LessThanExpression, SyntaxKind.LessThanToken),
        _ => null,
    };

    private static bool IsTotallyOrdered(ITypeSymbol? type) =>
        type is not null && (type.TypeKind == TypeKind.Enum || type.SpecialType is SpecialType.System_SByte
            or SpecialType.System_Byte or SpecialType.System_Int16 or SpecialType.System_UInt16
            or SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64
            or SpecialType.System_UInt64 or SpecialType.System_Char or SpecialType.System_Decimal
            or SpecialType.System_IntPtr or SpecialType.System_UIntPtr);
}
