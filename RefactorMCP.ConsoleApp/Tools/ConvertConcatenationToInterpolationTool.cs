using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Threading;

[McpServerToolType]
public static class ConvertConcatenationToInterpolationTool
{
    [McpServerTool, Description("Convert a string concatenation with + into an interpolated string")]
    public static async Task<string> ConvertConcatenationToInterpolation(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line of the concatenation (1-based)")] int line,
        [Description("Column on that line inside the concatenation (1-based)")] int column,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var target = await CaretTarget.FindAsync(solutionPath, filePath, line, column, cancellationToken);
            var model = target.Model;
            var concatenation = Outermost(target.Token.Parent?.AncestorsAndSelf().OfType<BinaryExpressionSyntax>().FirstOrDefault(b => IsConcatenation(b, model)), model)
                ?? throw new McpException($"Error: The expression at {line}:{column} is not a string concatenation");

            var inner = concatenation.DescendantTrivia().Where(t => !concatenation.GetLeadingTrivia().Contains(t) && !concatenation.GetTrailingTrivia().Contains(t));
            if (inner.Any(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) || t.IsKind(SyntaxKind.MultiLineCommentTrivia)))
                throw new McpException("Error: The concatenation has comments between its operands, which an interpolated string cannot keep");

            var operands = Operands(concatenation, model).ToList();
            var verbatim = operands.Any(IsStringText) && operands.Where(IsStringText).All(IsVerbatim);

            var contents = new StringBuilder();
            foreach (var operand in operands)
                contents.Append(Contents(operand, verbatim, model));

            var interpolated = SyntaxFactory.ParseExpression((verbatim ? "$@\"" : "$\"") + contents + "\"");
            if (interpolated is not InterpolatedStringExpressionSyntax || interpolated.ContainsDiagnostics)
                throw new McpException("Error: The operands cannot be written as one interpolated string");

            await target.ApplyAsync(target.Root.ReplaceNode(concatenation, interpolated.WithTriviaFrom(concatenation)), cancellationToken);
            return $"Successfully converted the concatenation to an interpolated string in {filePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error converting concatenation to interpolation: {ex.Message}", ex);
        }
    }

    /// <summary>A + that the built-in string concatenation operator performs.</summary>
    private static bool IsConcatenation(ExpressionSyntax expression, SemanticModel model) =>
        expression is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression }
        && model.GetSymbolInfo(expression).Symbol is IMethodSymbol { ContainingType.SpecialType: SpecialType.System_String };

    /// <summary>The whole chain a concatenation belongs to, through parentheses.</summary>
    private static BinaryExpressionSyntax? Outermost(BinaryExpressionSyntax? concatenation, SemanticModel model)
    {
        if (concatenation is null)
            return null;

        SyntaxNode current = concatenation;
        for (var parent = current.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is BinaryExpressionSyntax binary && IsConcatenation(binary, model))
                current = binary;
            else if (parent is not ParenthesizedExpressionSyntax)
                break;
        }

        return (BinaryExpressionSyntax)current;
    }

    /// <summary>
    /// The operands of a concatenation in order, flattening nested concatenations.
    /// A numeric addition among the leading operands is refused: it runs before the
    /// concatenation, which the result could only show by parenthesising it.
    /// </summary>
    private static IEnumerable<ExpressionSyntax> Operands(ExpressionSyntax expression, SemanticModel model)
    {
        var unwrapped = expression;
        while (unwrapped is ParenthesizedExpressionSyntax parenthesized)
            unwrapped = parenthesized.Expression;

        if (unwrapped is BinaryExpressionSyntax binary && IsConcatenation(binary, model))
        {
            if (binary.Left is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression } left && !IsConcatenation(left, model))
                throw new McpException($"Error: '{left}' adds numbers before the concatenation; parenthesise it to make that explicit first");

            return Operands(binary.Left, model).Concat(Operands(binary.Right, model));
        }

        return new[] { expression };
    }

    private static bool IsStringText(ExpressionSyntax operand) =>
        operand is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } or InterpolatedStringExpressionSyntax;

    private static bool IsVerbatim(ExpressionSyntax operand) => operand switch
    {
        LiteralExpressionSyntax literal => literal.Token.IsKind(SyntaxKind.StringLiteralToken) && literal.Token.Text.StartsWith('@'),
        InterpolatedStringExpressionSyntax interpolated => interpolated.StringStartToken.IsKind(SyntaxKind.InterpolatedVerbatimStringStartToken),
        _ => false,
    };

    /// <summary>What an operand contributes between the quotes of the interpolated string.</summary>
    private static string Contents(ExpressionSyntax operand, bool verbatim, SemanticModel model)
    {
        switch (operand)
        {
            case LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } literal
                when literal.Token.IsKind(SyntaxKind.StringLiteralToken):
                // A literal in the same form as the result keeps its escapes as written.
                return IsVerbatim(literal) == verbatim
                    ? EscapeBraces(literal.Token.Text[(verbatim ? 2 : 1)..^1])
                    : Escape(literal.Token.ValueText, verbatim);
            case LiteralExpressionSyntax { RawKind: (int)SyntaxKind.CharacterLiteralExpression } character when !verbatim:
                return Escape(character.Token.ValueText, verbatim);
            case InterpolatedStringExpressionSyntax interpolated
                when interpolated.StringStartToken.Kind() is SyntaxKind.InterpolatedStringStartToken or SyntaxKind.InterpolatedVerbatimStringStartToken:
                return string.Concat(interpolated.Contents.Select(content => content switch
                {
                    InterpolatedStringTextSyntax text when IsVerbatim(interpolated) != verbatim => Escape(text.TextToken.ValueText, verbatim),
                    _ => content.ToString(),
                }));
            default:
                return Interpolation(operand, model);
        }
    }

    /// <summary>
    /// An operand as an interpolation. A value type's <c>ToString(format)</c> becomes a
    /// format specifier, parentheses go unless a conditional needs them to keep
    /// its colon from starting a format.
    /// </summary>
    private static string Interpolation(ExpressionSyntax operand, SemanticModel model)
    {
        var expression = operand;
        while (expression is ParenthesizedExpressionSyntax { Expression: not ConditionalExpressionSyntax } parenthesized)
            expression = parenthesized.Expression;

        if (expression is InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "ToString" } access,
                ArgumentList.Arguments: [{ Expression: LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } format }],
            } invocation
            && model.GetTypeInfo(access.Expression).Type is { IsValueType: true } type
            && type.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T
            && type.AllInterfaces.Any(i => i.ToDisplayString() == "System.IFormattable")
            && model.GetSymbolInfo(invocation).Symbol is IMethodSymbol { Parameters: [{ Type.SpecialType: SpecialType.System_String }] }
            && format.Token.ValueText.Length > 0
            && format.Token.ValueText.All(c => c is not ('{' or '}' or '"' or '\\') && !char.IsControl(c)))
        {
            return "{" + access.Expression.WithoutTrivia() + ":" + format.Token.ValueText + "}";
        }

        return "{" + expression.WithoutTrivia() + "}";
    }

    private static string EscapeBraces(string text) => text.Replace("{", "{{").Replace("}", "}}");

    /// <summary>Text as it is written inside a regular or verbatim interpolated string.</summary>
    private static string Escape(string value, bool verbatim)
    {
        if (verbatim)
            return EscapeBraces(value.Replace("\"", "\"\""));

        var escaped = new StringBuilder();
        foreach (var c in value)
        {
            escaped.Append(c switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                '\0' => "\\0",
                '{' => "{{",
                '}' => "}}",
                _ when char.IsControl(c) => $"\\u{(int)c:x4}",
                _ => c.ToString(),
            });
        }

        return escaped.ToString();
    }
}
