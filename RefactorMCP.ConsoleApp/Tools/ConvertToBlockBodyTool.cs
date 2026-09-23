using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Threading;

[McpServerToolType]
public static class ConvertToBlockBodyTool
{
    [McpServerTool, Description("Convert an expression-bodied method, property, indexer, accessor, constructor, operator or local function to a block body")]
    public static async Task<string> ConvertToBlockBody(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line of the member's name, or of the accessor's keyword (1-based)")] int line,
        [Description("Column on that line (1-based)")] int column,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var target = await PositionTarget.FindAsync(solutionPath, filePath, line, column, cancellationToken);
            var member = MemberBody.Find(target);
            var arrow = MemberBody.Arrow(member);
            if (arrow == null)
            {
                var hasBlock = MemberBody.Block(member) != null ||
                               member is BasePropertyDeclarationSyntax { AccessorList: { } list } &&
                               list.Accessors.Any(a => a.Body != null || a.ExpressionBody != null);
                throw hasBlock
                    ? new McpException($"Error: The {MemberBody.Describe(member)} already has a block body")
                    : new McpException($"Error: The {MemberBody.Describe(member)} has no body");
            }

            var statement = Statement(member, arrow, ReturnsValue(target.Model, member, cancellationToken));
            var semicolon = SemicolonOf(member);
            var block = SyntaxFactory.Block(statement.WithTrailingTrivia(StatementTrailingTrivia(semicolon)))
                .WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken)
                    .WithTrailingTrivia(semicolon.TrailingTrivia.Where(t => !MemberBody.IsComment(t) && !t.IsKind(SyntaxKind.WhitespaceTrivia))))
                .WithAdditionalAnnotations(Formatter.Annotation);

            var cleared = member.ReplaceToken(arrow.GetFirstToken().GetPreviousToken(), arrow.GetFirstToken().GetPreviousToken().WithTrailingTrivia());
            SyntaxNode converted = cleared switch
            {
                PropertyDeclarationSyntax property => property
                    .WithExpressionBody(null)
                    .WithSemicolonToken(default)
                    .WithAccessorList(Getter(block)),
                IndexerDeclarationSyntax indexer => indexer
                    .WithExpressionBody(null)
                    .WithSemicolonToken(default)
                    .WithAccessorList(Getter(block)),
                _ => MemberBody.WithBody(cleared, block, null, default),
            };

            var newRoot = target.Root.ReplaceNode(member, converted);
            var formatted = Formatter.Format(newRoot, Formatter.Annotation, target.Document.Project.Solution.Workspace);
            await target.WriteAsync(formatted);
            return $"Successfully converted {MemberBody.Describe(member)} to a block body in {filePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error converting to block body: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// A throw expression becomes a throw statement; otherwise the expression is returned
    /// when the member returns a value, and is a statement of its own when it does not.
    /// </summary>
    private static StatementSyntax Statement(SyntaxNode member, ArrowExpressionClauseSyntax arrow, bool returnsValue)
    {
        // The statement sits one level deeper than the member's header it followed.
        var expression = PositionTarget.Reindent(arrow.Expression, IndentationSize).WithoutTrivia();
        return expression switch
        {
            ThrowExpressionSyntax thrown => SyntaxFactory.ThrowStatement(thrown.Expression),
            _ when returnsValue => SyntaxFactory.ReturnStatement(expression),
            _ => SyntaxFactory.ExpressionStatement(expression),
        };
    }

    private const int IndentationSize = 4;

    private static bool ReturnsValue(SemanticModel model, SyntaxNode member, CancellationToken cancellationToken)
    {
        if (member is BasePropertyDeclarationSyntax)
            return true;

        var symbol = model.GetDeclaredSymbol(member, cancellationToken) as IMethodSymbol;
        if (symbol == null || symbol.ReturnsVoid)
            return false;

        return symbol.MethodKind switch
        {
            MethodKind.Constructor or MethodKind.StaticConstructor or MethodKind.Destructor => false,
            _ => !(symbol.IsAsync && IsNonGenericTask(symbol.ReturnType)),
        };
    }

    /// <summary>An async method returning a task with no result awaits rather than returns.</summary>
    private static bool IsNonGenericTask(ITypeSymbol type) =>
        type is INamedTypeSymbol { Arity: 0 } named &&
        named.GetMembers("GetAwaiter").OfType<IMethodSymbol>().Any(m => m.Parameters.Length == 0);

    private static SyntaxToken SemicolonOf(SyntaxNode member) => member switch
    {
        AccessorDeclarationSyntax accessor => accessor.SemicolonToken,
        LocalFunctionStatementSyntax function => function.SemicolonToken,
        BaseMethodDeclarationSyntax method => method.SemicolonToken,
        PropertyDeclarationSyntax property => property.SemicolonToken,
        IndexerDeclarationSyntax indexer => indexer.SemicolonToken,
        _ => default,
    };

    /// <summary>A comment after the member's semicolon stays at the end of the statement.</summary>
    private static SyntaxTriviaList StatementTrailingTrivia(SyntaxToken semicolon)
    {
        var comments = semicolon.TrailingTrivia.Where(MemberBody.IsComment).ToList();
        return comments.Count == 0
            ? SyntaxFactory.TriviaList(SyntaxFactory.ElasticCarriageReturnLineFeed)
            : SyntaxFactory.TriviaList(comments.SelectMany(c => new[] { SyntaxFactory.Space, c }))
                .Add(SyntaxFactory.ElasticCarriageReturnLineFeed);
    }

    private static AccessorListSyntax Getter(BlockSyntax block) =>
        SyntaxFactory.AccessorList(SyntaxFactory.SingletonList(
                SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration, block)))
            .WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken).WithTrailingTrivia(block.CloseBraceToken.TrailingTrivia))
            .WithAdditionalAnnotations(Formatter.Annotation);
}
