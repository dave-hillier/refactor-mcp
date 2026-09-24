using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System.Threading;

[McpServerToolType]
public static class UsePatternMatchingTool
{
    [McpServerTool, Description("Replace a type test followed by casts, or an as conversion followed by a null check, with a declaration pattern in the if statement")]
    public static async Task<string> UsePatternMatching(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line of the if keyword (1-based)")] int line,
        [Description("Column of the if keyword (1-based)")] int column,
        [Description("Name for the pattern variable (optional; defaults to a local initialised with the cast, or the type's name)")] string? name = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var caret = await CaretDocument.FindAsync(solutionPath, filePath, line, column, cancellationToken);
            var statement = caret.IfStatement();

            var newRoot = TypeTest(statement.Condition) is { } test
                ? ReplaceCasts(caret, statement, test.Value, test.Type, test.Negated, name)
                : ReplaceAsConversion(caret, statement, name)
                  ?? throw new McpException("Error: The condition is not a type test of a local, nor a null check of a local assigned with as");

            await caret.ApplyAsync(newRoot, cancellationToken);
            return $"Successfully used pattern matching in {filePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error using pattern matching: {ex.Message}", ex);
        }
    }

    /// <summary><c>x is T</c>, or its negation <c>!(x is T)</c> or <c>x is not T</c>.</summary>
    private static (ExpressionSyntax Value, TypeSyntax Type, bool Negated)? TypeTest(ExpressionSyntax condition) => WithoutParentheses(condition) switch
    {
        BinaryExpressionSyntax { RawKind: (int)SyntaxKind.IsExpression, Right: TypeSyntax type } isType => (isType.Left, type, false),
        IsPatternExpressionSyntax { Pattern: TypePatternSyntax pattern } isPattern => (isPattern.Expression, pattern.Type, false),
        IsPatternExpressionSyntax { Pattern: UnaryPatternSyntax { RawKind: (int)SyntaxKind.NotPattern, Pattern: TypePatternSyntax pattern } } isPattern =>
            (isPattern.Expression, pattern.Type, true),
        PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } not when TypeTest(not.Operand) is { Negated: false } inner =>
            (inner.Value, inner.Type, true),
        _ => null,
    };

    /// <summary>
    /// <c>if (x is T) { ... (T)x ... }</c> becomes <c>if (x is T t) { ... t ... }</c>.
    /// When the test is negated and its branch always leaves, the casts
    /// replaced are in the statements after the if, where <c>x</c> is a <c>T</c>.
    /// </summary>
    private static SyntaxNode ReplaceCasts(CaretDocument caret, IfStatementSyntax statement, ExpressionSyntax value, TypeSyntax type, bool negated, string? name)
    {
        var tested = caret.Model.GetSymbolInfo(value).Symbol;
        if (value is not IdentifierNameSyntax || tested is not (ILocalSymbol or IParameterSymbol))
            throw new McpException($"Error: '{value}' is not a local or parameter, so it could change between the test and a cast");

        var testedType = caret.Model.GetTypeInfo(type).Type;
        IReadOnlyList<StatementSyntax> scope;
        if (!negated)
        {
            scope = new[] { statement.Statement };
        }
        else if (!caret.EndPointIsReachable(CaretDocument.Statements(statement.Statement)) && CaretDocument.Siblings(statement) is { } siblings)
        {
            scope = siblings.Skip(siblings.IndexOf(statement) + 1).ToList();
        }
        else
        {
            throw new McpException($"Error: The if statement tests that '{value}' is not a '{type}' but its branch does not always leave, so there is no code where it is one");
        }

        var casts = scope.SelectMany(s => s.DescendantNodesAndSelf())
            .OfType<CastExpressionSyntax>()
            .Where(cast => WithoutParentheses(cast.Expression) is IdentifierNameSyntax operand
                && SymbolEqualityComparer.Default.Equals(caret.Model.GetSymbolInfo(operand).Symbol, tested)
                && SymbolEqualityComparer.Default.Equals(caret.Model.GetTypeInfo(cast.Type).Type, testedType))
            .ToList();
        if (casts.Count == 0)
            throw new McpException($"Error: There is no cast of '{value}' to '{type}' to replace");

        var writes = scope.SelectMany(s => s.DescendantNodesAndSelf())
            .OfType<IdentifierNameSyntax>()
            .Where(n => LocalVariableTarget.IsWrite(n) && SymbolEqualityComparer.Default.Equals(caret.Model.GetSymbolInfo(n).Symbol, tested));
        if (writes.Any())
            throw new McpException($"Error: '{value}' is assigned after the test, so a cast may no longer see the value that was tested");

        // A local initialised with the cast alone already names the value; it
        // becomes the pattern variable.
        var declaration = name is null ? CastDeclaration(caret, scope, casts) : null;
        name ??= declaration?.Declaration.Variables[0].Identifier.ValueText ?? FreeName(caret, statement, testedType);
        EnsureFree(caret, statement, name, declaration);

        var pattern = (PatternSyntax)SyntaxFactory.DeclarationPattern(type.WithoutTrivia(), SyntaxFactory.SingleVariableDesignation(SyntaxFactory.Identifier(name)));
        if (negated)
            pattern = SyntaxFactory.UnaryPattern(SyntaxFactory.Token(SyntaxKind.NotKeyword).WithTrailingTrivia(SyntaxFactory.Space), pattern);
        var condition = SyntaxFactory.IsPatternExpression(value.WithoutTrivia(), pattern).NormalizeWhitespace().WithTriviaFrom(statement.Condition);

        var replaced = casts.Select(cast => cast.Parent is ParenthesizedExpressionSyntax parenthesized ? (SyntaxNode)parenthesized : cast);
        var editor = new SyntaxEditor(caret.Root, caret.Document.Project.Solution.Services);
        foreach (var node in replaced)
            editor.ReplaceNode(node, (current, _) => SyntaxFactory.IdentifierName(name).WithTriviaFrom(current));
        if (declaration is not null)
            editor.RemoveNode(declaration, SyntaxRemoveOptions.KeepNoTrivia);
        editor.ReplaceNode(statement.Condition, condition);
        return editor.GetChangedRoot();
    }

    /// <summary>
    /// <c>var t = x as T; if (t != null)</c> becomes <c>if (x is T t)</c>, and
    /// <c>if (t == null)</c>, with a branch that always leaves, becomes
    /// <c>if (x is not T t)</c>.
    /// </summary>
    private static SyntaxNode? ReplaceAsConversion(CaretDocument caret, IfStatementSyntax statement, string? name)
    {
        if (NullCheck(statement.Condition) is not var (checkedName, isNull)
            || CaretDocument.Siblings(statement) is not { } siblings
            || siblings.IndexOf(statement) is var index && index == 0
            || siblings[index - 1] is not LocalDeclarationStatementSyntax
            {
                Declaration.Variables: [{ Initializer.Value: BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AsExpression } conversion } declarator],
            } declaration
            || declarator.Identifier.ValueText != checkedName.Identifier.ValueText)
            return null;

        var local = (ILocalSymbol)caret.Model.GetDeclaredSymbol(declarator)!;
        if (!SymbolEqualityComparer.Default.Equals(caret.Model.GetSymbolInfo(checkedName).Symbol, local))
            return null;

        if (isNull && caret.EndPointIsReachable(CaretDocument.Statements(statement.Statement)))
            throw new McpException($"Error: The if statement tests that '{local.Name}' is null but its branch does not always leave, so the code after it may see it unassigned");

        // The pattern variable is only assigned where the pattern matched.
        var assigned = isNull
            ? statement.Statement.DescendantNodesAndSelf()
            : siblings.Skip(index + 1).SelectMany(s => s.DescendantNodesAndSelf())
                .Where(n => !statement.Statement.Span.Contains(n.Span) && !statement.Condition.Span.Contains(n.Span));
        if (assigned.OfType<IdentifierNameSyntax>().Any(n => SymbolEqualityComparer.Default.Equals(caret.Model.GetSymbolInfo(n).Symbol, local)))
            throw new McpException($"Error: '{local.Name}' is used outside the branch where it is known not to be null, where the pattern variable would not be assigned");

        name ??= local.Name;
        if (name != local.Name)
            EnsureFree(caret, statement, name, declaration);

        var type = (TypeSyntax)conversion.Right;
        var pattern = (PatternSyntax)SyntaxFactory.DeclarationPattern(type.WithoutTrivia(), SyntaxFactory.SingleVariableDesignation(SyntaxFactory.Identifier(name)));
        if (isNull)
            pattern = SyntaxFactory.UnaryPattern(SyntaxFactory.Token(SyntaxKind.NotKeyword).WithTrailingTrivia(SyntaxFactory.Space), pattern);
        var condition = SyntaxFactory.IsPatternExpression(conversion.Left.WithoutTrivia(), pattern).NormalizeWhitespace().WithTriviaFrom(statement.Condition);

        var editor = new SyntaxEditor(caret.Root, caret.Document.Project.Solution.Services);
        if (name != local.Name)
        {
            foreach (var use in statement.Parent!.DescendantNodes().OfType<IdentifierNameSyntax>()
                         .Where(n => SymbolEqualityComparer.Default.Equals(caret.Model.GetSymbolInfo(n).Symbol, local)))
                editor.ReplaceNode(use, (current, _) => SyntaxFactory.IdentifierName(name).WithTriviaFrom(current));
        }

        // The condition holds no use of the local once it tests the converted value.
        editor.ReplaceNode(statement, (current, _) => ((IfStatementSyntax)current)
            .WithCondition(condition)
            .WithLeadingTrivia(declaration.GetLeadingTrivia()));
        editor.RemoveNode(declaration, SyntaxRemoveOptions.KeepNoTrivia);
        return editor.GetChangedRoot();
    }

    /// <summary><c>t != null</c>, <c>t is not null</c>, <c>t == null</c> or <c>t is null</c>, and whether it tests for null.</summary>
    private static (IdentifierNameSyntax Name, bool IsNull)? NullCheck(ExpressionSyntax condition) => WithoutParentheses(condition) switch
    {
        BinaryExpressionSyntax { RawKind: (int)SyntaxKind.NotEqualsExpression or (int)SyntaxKind.EqualsExpression } binary
            when NullComparedWith(binary) is { } name => (name, binary.IsKind(SyntaxKind.EqualsExpression)),
        IsPatternExpressionSyntax { Expression: IdentifierNameSyntax name, Pattern: ConstantPatternSyntax { Expression: LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression } } } =>
            (name, true),
        IsPatternExpressionSyntax
        {
            Expression: IdentifierNameSyntax name,
            Pattern: UnaryPatternSyntax { RawKind: (int)SyntaxKind.NotPattern, Pattern: ConstantPatternSyntax { Expression: LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression } } },
        } => (name, false),
        _ => null,
    };

    private static IdentifierNameSyntax? NullComparedWith(BinaryExpressionSyntax binary) => (binary.Left, binary.Right) switch
    {
        (IdentifierNameSyntax name, LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression }) => name,
        (LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression }, IdentifierNameSyntax name) => name,
        _ => null,
    };

    /// <summary>
    /// A statement in the scope declaring one local initialised with nothing
    /// but one of the casts, which is never assigned afterwards.
    /// </summary>
    private static LocalDeclarationStatementSyntax? CastDeclaration(CaretDocument caret, IReadOnlyList<StatementSyntax> scope, List<CastExpressionSyntax> casts)
    {
        var statements = scope.Count == 1 ? CaretDocument.Statements(scope[0]) : scope;
        foreach (var statement in statements.OfType<LocalDeclarationStatementSyntax>())
        {
            if (statement is not { Declaration.Variables: [{ Initializer.Value: CastExpressionSyntax cast } declarator] } || !casts.Contains(cast))
                continue;

            var local = caret.Model.GetDeclaredSymbol(declarator);
            var written = statement.Parent!.DescendantNodes().OfType<IdentifierNameSyntax>()
                .Any(n => LocalVariableTarget.IsWrite(n) && SymbolEqualityComparer.Default.Equals(caret.Model.GetSymbolInfo(n).Symbol, local));
            if (!written)
                return statement;
        }

        return null;
    }

    /// <summary>The type's name in camel case, numbered if that name is taken.</summary>
    private static string FreeName(CaretDocument caret, IfStatementSyntax statement, ITypeSymbol? type)
    {
        var baseName = type is null || type.Name.Length == 0 ? "value" : char.ToLowerInvariant(type.Name[0]) + type.Name[1..];
        if (SyntaxFacts.GetKeywordKind(baseName) != SyntaxKind.None)
            baseName = "@" + baseName;

        for (var suffix = 1; ; suffix++)
        {
            var candidate = suffix == 1 ? baseName : baseName + suffix;
            if (!IsTaken(caret, statement, candidate))
                return candidate;
        }
    }

    private static void EnsureFree(CaretDocument caret, IfStatementSyntax statement, string name, LocalDeclarationStatementSyntax? replacedDeclaration)
    {
        if (IsTaken(caret, statement, name, replacedDeclaration))
            throw new McpException($"Error: The name '{name}' is already declared where the pattern variable would be");
    }

    /// <summary>
    /// Whether the name is visible at the if, or declared by a local or pattern
    /// anywhere in the enclosing member, where it would clash with the pattern
    /// variable.
    /// </summary>
    private static bool IsTaken(CaretDocument caret, IfStatementSyntax statement, string name, SyntaxNode? ignoring = null)
    {
        if (caret.Model.LookupSymbols(statement.SpanStart, name: name).Any(s => s is ILocalSymbol or IParameterSymbol or IRangeVariableSymbol
                && !(ignoring?.Span.Contains(s.Locations[0].SourceSpan) ?? false)))
            return true;

        var member = statement.Ancestors().FirstOrDefault(a => a is MemberDeclarationSyntax) ?? caret.Root;
        return member.DescendantNodes()
            .Where(n => ignoring is null || !ignoring.Span.Contains(n.Span))
            .Any(n => n switch
            {
                VariableDeclaratorSyntax v => v.Identifier.ValueText == name,
                SingleVariableDesignationSyntax d => d.Identifier.ValueText == name,
                ForEachStatementSyntax f => f.Identifier.ValueText == name,
                _ => false,
            });
    }

    private static ExpressionSyntax WithoutParentheses(ExpressionSyntax expression) =>
        expression is ParenthesizedExpressionSyntax parenthesized ? WithoutParentheses(parenthesized.Expression) : expression;
}
