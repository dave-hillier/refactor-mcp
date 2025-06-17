using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Base rewriter that replaces a target expression with a provided
/// reference and inserts a declaration into the specified container
/// when that container is visited.
/// </summary>
internal abstract class ExpressionIntroductionRewriter<TContainer> : CSharpSyntaxRewriter where TContainer : SyntaxNode
{
    private readonly ExpressionSyntax _targetExpression;
    private readonly ExpressionSyntax _replacement;
    private readonly SyntaxNode _declaration;
    private readonly TContainer? _targetContainer;

    protected ExpressionIntroductionRewriter(
        ExpressionSyntax targetExpression,
        ExpressionSyntax replacement,
        SyntaxNode declaration,
        TContainer? targetContainer)
    {
        _targetExpression = targetExpression;
        _replacement = replacement;
        _declaration = declaration;
        _targetContainer = targetContainer;
    }

    protected SyntaxNode Declaration => _declaration;
    protected ExpressionSyntax Replacement => _replacement;
    protected ExpressionSyntax TargetExpression => _targetExpression;

    public override SyntaxNode Visit(SyntaxNode? node)
    {
        if (node is ExpressionSyntax expr && SyntaxFactory.AreEquivalent(expr, _targetExpression))
            return _replacement;

        return base.Visit(node)!;
    }

    protected abstract TContainer InsertDeclaration(TContainer container, SyntaxNode declaration);

    protected TContainer MaybeInsertDeclaration(TContainer original, TContainer visited, bool? condition = null)
    {
        bool shouldInsert = condition ?? (_targetContainer != null && ReferenceEquals(original, _targetContainer));
        if (shouldInsert)
            return InsertDeclaration(visited, _declaration);

        return visited;
    }
}
