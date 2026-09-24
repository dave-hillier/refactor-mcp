using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Threading;

[McpServerToolType]
public static class ConvertToExpressionBodyTool
{
    [McpServerTool, Description("Convert a method, property, accessor, constructor, operator or local function whose body is a single return, expression or throw statement to an expression body")]
    public static async Task<string> ConvertToExpressionBody(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line of the member's name, or of the accessor's keyword (1-based)")] int line,
        [Description("Column on that line (1-based)")] int column,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var target = await PositionTarget.FindAsync(solutionPath, filePath, line, column, cancellationToken);
            var member = MemberBody.Find(target);
            var converted = member switch
            {
                PropertyDeclarationSyntax or IndexerDeclarationSyntax => ConvertProperty((BasePropertyDeclarationSyntax)member),
                _ => ConvertBody(member),
            };

            await target.WriteAsync(target.Root.ReplaceNode(member, converted));
            return $"Successfully converted {MemberBody.Describe(member)} to an expression body in {filePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error converting to expression body: {ex.Message}", ex);
        }
    }

    private static SyntaxNode ConvertBody(SyntaxNode member)
    {
        var block = MemberBody.Block(member);
        if (block == null)
        {
            throw MemberBody.Arrow(member) != null
                ? new McpException($"Error: The {MemberBody.Describe(member)} is already expression-bodied")
                : new McpException($"Error: The {MemberBody.Describe(member)} has no body");
        }

        var body = SingleExpression(member, block);
        var cleared = ClearTrailingTrivia(member, block.OpenBraceToken.GetPreviousToken());
        var converted = MemberBody.WithBody(cleared, null, Arrow(body.Expression), Semicolon(body, block.CloseBraceToken));
        return WithCommentsAbove(converted, body.Leading);
    }

    /// <summary>A property or indexer whose only accessor is a get accessor becomes its expression.</summary>
    private static SyntaxNode ConvertProperty(BasePropertyDeclarationSyntax property)
    {
        var accessors = property.AccessorList;
        if (accessors == null)
            throw new McpException($"Error: The {MemberBody.Describe(property)} is already expression-bodied");

        if (accessors.Accessors.Count != 1 || !accessors.Accessors[0].IsKind(SyntaxKind.GetAccessorDeclaration) ||
            accessors.Accessors[0].Modifiers.Count > 0 || accessors.Accessors[0].AttributeLists.Count > 0)
        {
            throw new McpException(
                $"Error: The {MemberBody.Describe(property)} has more than a plain get accessor; convert its accessors one at a time");
        }

        var getter = accessors.Accessors[0];
        var body = getter switch
        {
            { Body: { } block } => SingleExpression(property, block),
            { ExpressionBody: { } arrow } => new SingleExpressionBody(arrow.Expression.WithoutTrivia(), [], []),
            _ => throw new McpException($"Error: The {MemberBody.Describe(property)} has no body"),
        };

        var cleared = ClearTrailingTrivia(property, accessors.OpenBraceToken.GetPreviousToken());
        SyntaxNode converted = cleared switch
        {
            PropertyDeclarationSyntax p => p.WithAccessorList(null).WithExpressionBody(Arrow(body.Expression))
                .WithSemicolonToken(Semicolon(body, accessors.CloseBraceToken)),
            IndexerDeclarationSyntax i => i.WithAccessorList(null).WithExpressionBody(Arrow(body.Expression))
                .WithSemicolonToken(Semicolon(body, accessors.CloseBraceToken)),
            _ => throw new InvalidOperationException(),
        };
        return WithCommentsAbove(converted, body.Leading);
    }

    private sealed record SingleExpressionBody(
        ExpressionSyntax Expression,
        IReadOnlyList<SyntaxTrivia> Leading,
        IReadOnlyList<SyntaxTrivia> Trailing);

    /// <summary>
    /// The expression of a block holding one return, expression or throw statement,
    /// indented to follow the member's header, with the comments around the statement.
    /// </summary>
    private static SingleExpressionBody SingleExpression(SyntaxNode member, BlockSyntax block)
    {
        if (block.DescendantTrivia().Any(t => t.IsDirective))
            throw new McpException($"Error: The body of the {MemberBody.Describe(member)} contains a preprocessor directive");
        if (block.Statements.Count != 1)
            throw new McpException($"Error: The body of the {MemberBody.Describe(member)} is not a single return, expression or throw statement");

        var statement = block.Statements[0];
        ExpressionSyntax expression = statement switch
        {
            ReturnStatementSyntax { Expression: { } returned } => returned,
            ExpressionStatementSyntax expressionStatement => expressionStatement.Expression,
            ThrowStatementSyntax { Expression: { } thrown } => SyntaxFactory.ThrowExpression(
                SyntaxFactory.Token(SyntaxKind.ThrowKeyword).WithTrailingTrivia(SyntaxFactory.Space), thrown),
            _ => throw new McpException(
                $"Error: The body of the {MemberBody.Describe(member)} is not a single return, expression or throw statement"),
        };

        // The statement sat one level deeper than the member's header it now follows.
        var delta = PositionTarget.IndentationOf(member) - PositionTarget.IndentationOf(statement);
        if (expression is ThrowExpressionSyntax throwExpression)
            expression = throwExpression.WithExpression(PositionTarget.Reindent(((ThrowStatementSyntax)statement).Expression!, delta).WithoutTrivia());
        else
            expression = PositionTarget.Reindent(expression, delta);

        var leading = block.OpenBraceToken.TrailingTrivia
            .Concat(statement.GetLeadingTrivia())
            .Concat(block.CloseBraceToken.LeadingTrivia)
            .Where(MemberBody.IsComment)
            .ToList();
        var trailing = statement.GetTrailingTrivia().Where(MemberBody.IsComment).ToList();
        return new SingleExpressionBody(expression.WithoutTrivia(), leading, trailing);
    }

    private static ArrowExpressionClauseSyntax Arrow(ExpressionSyntax expression) =>
        SyntaxFactory.ArrowExpressionClause(
            SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.Space), SyntaxKind.EqualsGreaterThanToken, SyntaxFactory.TriviaList(SyntaxFactory.Space)),
            expression);

    /// <summary>The semicolon ends the line the closing brace ended, after any comment that followed the statement.</summary>
    private static SyntaxToken Semicolon(SingleExpressionBody body, SyntaxToken closeBrace)
    {
        var trailing = new List<SyntaxTrivia>();
        foreach (var comment in body.Trailing)
        {
            trailing.Add(SyntaxFactory.Space);
            trailing.Add(comment);
        }

        trailing.AddRange(closeBrace.TrailingTrivia.SkipWhile(t => t.IsKind(SyntaxKind.WhitespaceTrivia)));
        return SyntaxFactory.Token(SyntaxFactory.TriviaList(), SyntaxKind.SemicolonToken, SyntaxFactory.TriviaList(trailing));
    }

    private static SyntaxNode ClearTrailingTrivia(SyntaxNode member, SyntaxToken token) =>
        member.ReplaceToken(token, token.WithTrailingTrivia());

    /// <summary>Comments from inside the body go on lines of their own above the member.</summary>
    private static SyntaxNode WithCommentsAbove(SyntaxNode member, IReadOnlyList<SyntaxTrivia> comments)
    {
        if (comments.Count == 0)
            return member;

        var leading = member.GetLeadingTrivia();
        var indentation = leading.Count > 0 && leading.Last().IsKind(SyntaxKind.WhitespaceTrivia)
            ? leading.Last()
            : SyntaxFactory.Whitespace("");
        var endOfLine = MemberBody.EndOfLine(member);
        var added = comments.SelectMany(c => new[] { c, endOfLine, indentation });
        return member.WithLeadingTrivia(leading.AddRange(added));
    }
}
