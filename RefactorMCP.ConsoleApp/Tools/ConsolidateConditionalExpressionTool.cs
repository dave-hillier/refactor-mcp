using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Threading;

[McpServerToolType]
public static class ConsolidateConditionalExpressionTool
{
    [McpServerTool, Description("Combine the conditions of ifs that lead to the same code into one if: nested ifs join with &&, consecutive ifs and else if branches with the same body join with ||, and the combined condition can be extracted into a method")]
    public static async Task<string> ConsolidateConditionalExpression(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line of the first if keyword (1-based)")] int line,
        [Description("Column of the first if keyword (1-based)")] int column,
        [Description("Name of a private method to extract the combined condition into (optional)")] string? methodName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var caret = await CaretDocument.FindAsync(solutionPath, filePath, line, column, cancellationToken);
            var statement = caret.IfStatement();
            var merge = Nested(statement) ?? ElseIfBranches(statement) ?? ConsecutiveIfs(statement, caret)
                ?? throw new McpException(
                    "Error: The if statement has nothing to consolidate: no nested if, no else if and no following if with the same body");

            if (merge.Operator == SyntaxKind.LogicalOrExpression && merge.Conditions.Any(MergeSiblingIfsTool.DeclaresVariable))
                throw new McpException("Error: A condition declares a variable, which joining the conditions with || would leave unassigned");

            var condition = MergeSiblingIfsTool.Combine(merge.Conditions, merge.Operator);
            MethodDeclarationSyntax? extracted = null;
            if (methodName is not null)
                (condition, extracted) = Extract(condition, merge.Conditions, methodName, statement, caret);

            var newRoot = Apply(caret.Root, statement, merge, condition.WithTriviaFrom(statement.Condition), extracted);
            await caret.ApplyAsync(newRoot, cancellationToken);
            return $"Successfully consolidated {merge.Conditions.Count} conditions in {filePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error consolidating conditional expression: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// The conditions to join and how, the if they become once its condition
    /// is replaced, and the ifs that follow it and are absorbed into it.
    /// </summary>
    private sealed record Merge(
        IReadOnlyList<ExpressionSyntax> Conditions,
        SyntaxKind Operator,
        IfStatementSyntax Result,
        IReadOnlyList<IfStatementSyntax> Absorbed);

    /// <summary><c>if (a) { if (b) { S } }</c>, as deep as it goes, with no else at any level.</summary>
    private static Merge? Nested(IfStatementSyntax statement)
    {
        var conditions = new List<ExpressionSyntax> { statement.Condition };
        var current = statement;
        while (current.Else is null && InnerIf(current) is { Else: null } inner)
        {
            conditions.Add(inner.Condition);
            current = inner;
        }

        if (current == statement)
            return null;

        var body = current.Statement.WithTriviaFrom(statement.Statement).WithAdditionalAnnotations(Formatter.Annotation);
        return new Merge(conditions, SyntaxKind.LogicalAndExpression, statement.WithStatement(body), Array.Empty<IfStatementSyntax>());
    }

    private static IfStatementSyntax? InnerIf(IfStatementSyntax outer) => outer.Statement switch
    {
        IfStatementSyntax inner => inner,
        BlockSyntax { Statements: [IfStatementSyntax inner] } => inner,
        _ => null,
    };

    /// <summary><c>if (a) S else if (b) S else E</c>: the leading branches with the same body.</summary>
    private static Merge? ElseIfBranches(IfStatementSyntax statement)
    {
        var conditions = new List<ExpressionSyntax> { statement.Condition };
        var last = statement;
        while (last.Else?.Statement is IfStatementSyntax next && SyntaxFactory.AreEquivalent(next.Statement, statement.Statement))
        {
            conditions.Add(next.Condition);
            last = next;
        }

        return last == statement
            ? null
            : new Merge(conditions, SyntaxKind.LogicalOrExpression, statement.WithElse(last.Else), Array.Empty<IfStatementSyntax>());
    }

    /// <summary>
    /// <c>if (a) S</c> followed by <c>if (b) S</c>, with no else, where
    /// <c>S</c> always jumps away: the second if only runs when <c>a</c> is
    /// false, so the two are <c>if (a || b) S</c>.
    /// </summary>
    private static Merge? ConsecutiveIfs(IfStatementSyntax statement, CaretDocument caret)
    {
        if (statement.Else is not null || CaretDocument.Siblings(statement) is not { } siblings)
            return null;

        var following = siblings.Skip(siblings.IndexOf(statement) + 1)
            .TakeWhile(s => s is IfStatementSyntax { Else: null } next && SyntaxFactory.AreEquivalent(next.Statement, statement.Statement))
            .Cast<IfStatementSyntax>()
            .ToList();
        if (following.Count == 0)
            return null;

        if (caret.EndPointIsReachable(CaretDocument.Statements(statement.Statement)))
        {
            throw new McpException(
                "Error: The ifs share a body that can fall through, so both run it when both conditions hold, where one if would run it once");
        }

        var conditions = following.Select(f => f.Condition).Prepend(statement.Condition).ToList();
        return new Merge(conditions, SyntaxKind.LogicalOrExpression, statement.WithLeadingTrivia(MergeSiblingIfsTool.LeadingComments(statement, following)), following);
    }

    /// <summary>
    /// A private method returning the combined condition, placed after the
    /// member that holds the if, and the call that replaces the condition. It
    /// takes the locals and parameters the conditions read, in the order they
    /// are first read, and is static when that member is.
    /// </summary>
    private static (ExpressionSyntax Call, MethodDeclarationSyntax Method) Extract(
        ExpressionSyntax condition,
        IReadOnlyList<ExpressionSyntax> conditions,
        string name,
        IfStatementSyntax statement,
        CaretDocument caret)
    {
        if (!CaretTarget.IsValidName(name))
            throw new McpException($"Error: '{name}' is not a valid method name");

        var member = statement.Ancestors().OfType<MemberDeclarationSyntax>().First(m => m.Parent is TypeDeclarationSyntax);
        var type = (TypeDeclarationSyntax)member.Parent!;
        var typeSymbol = caret.Model.GetDeclaredSymbol(type)!;
        if (typeSymbol.GetMembers(name).Any())
            throw new McpException($"Error: {typeSymbol.Name} already has a member named '{name}'");

        foreach (var original in conditions)
        {
            var flow = caret.Model.AnalyzeDataFlow(original);
            if (MergeSiblingIfsTool.DeclaresVariable(original) || flow is { Succeeded: true, WrittenInside.IsEmpty: false })
                throw new McpException("Error: A condition declares or assigns a variable, which a method extracted from it could not share with the if");
        }

        var parameters = conditions
            .SelectMany(c => c.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
            .Select(identifier => caret.Model.GetSymbolInfo(identifier).Symbol)
            .Where(symbol => symbol is ILocalSymbol or IParameterSymbol
                && !symbol.DeclaringSyntaxReferences.Any(r => conditions.Any(c => c.Span.Contains(r.Span))))
            .Distinct(SymbolEqualityComparer.Default)
            .ToList();

        var isStatic = caret.Model.GetDeclaredSymbol(member) is { IsStatic: true };
        var modifiers = SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));
        if (isStatic)
            modifiers = modifiers.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));

        var method = SyntaxFactory.MethodDeclaration(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.BoolKeyword)), name)
            .WithModifiers(modifiers)
            .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters.Select(p =>
                SyntaxFactory.Parameter(SyntaxFactory.Identifier(p!.Name))
                    .WithType(SyntaxFactory.ParseTypeName(TypeOf(p).ToMinimalDisplayString(caret.Model, statement.SpanStart)))))))
            .WithBody(SyntaxFactory.Block(SyntaxFactory.ReturnStatement(condition)))
            .WithAdditionalAnnotations(Formatter.Annotation);

        var call = SyntaxFactory.InvocationExpression(
            SyntaxFactory.IdentifierName(name),
            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(parameters.Select(p => SyntaxFactory.Argument(SyntaxFactory.IdentifierName(p!.Name))))));
        return (call.WithAdditionalAnnotations(Formatter.Annotation), method);
    }

    private static ITypeSymbol TypeOf(ISymbol symbol) => symbol switch
    {
        ILocalSymbol local => local.Type,
        IParameterSymbol parameter => parameter.Type,
        _ => throw new InvalidOperationException($"'{symbol.Name}' is not a local or parameter"),
    };

    /// <summary>
    /// Replaces the if with the merged one, removes the ifs it absorbed, and
    /// adds the extracted method after the member that holds the if.
    /// </summary>
    private static SyntaxNode Apply(
        SyntaxNode root,
        IfStatementSyntax statement,
        Merge merge,
        ExpressionSyntax condition,
        MethodDeclarationSyntax? extracted)
    {
        var member = statement.Ancestors().OfType<MemberDeclarationSyntax>().First(m => m.Parent is TypeDeclarationSyntax);
        var tracked = root.TrackNodes(merge.Absorbed.Append<SyntaxNode>(statement).Append(member));

        tracked = tracked.ReplaceNode(tracked.GetCurrentNode(statement)!, merge.Result.WithCondition(condition));
        foreach (var absorbed in merge.Absorbed)
            tracked = tracked.RemoveNode(tracked.GetCurrentNode(absorbed)!, SyntaxRemoveOptions.KeepNoTrivia)!;

        if (extracted is null)
            return tracked;

        var currentMember = tracked.GetCurrentNode(member)!;
        var type = (TypeDeclarationSyntax)currentMember.Parent!;
        var endOfLine = currentMember.GetTrailingTrivia().LastOrDefault(t => t.IsKind(SyntaxKind.EndOfLineTrivia));
        var method = extracted
            .WithLeadingTrivia(endOfLine)
            .WithTrailingTrivia(endOfLine);
        return tracked.ReplaceNode(type, type.WithMembers(type.Members.Insert(type.Members.IndexOf(currentMember) + 1, method)));
    }
}
