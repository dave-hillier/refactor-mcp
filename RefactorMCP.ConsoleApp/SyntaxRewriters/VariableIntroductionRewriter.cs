using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal class VariableIntroductionRewriter : ExpressionIntroductionRewriter<BlockSyntax>
{
    private readonly StatementSyntax? _containingStatement;
    private readonly int _insertIndex;

    public VariableIntroductionRewriter(
        ExpressionSyntax targetExpression,
        IdentifierNameSyntax variableReference,
        LocalDeclarationStatementSyntax variableDeclaration,
        StatementSyntax? containingStatement,
        BlockSyntax? containingBlock)
        : base(targetExpression, variableReference, variableDeclaration, containingBlock)
    {
        _containingStatement = containingStatement;
        _insertIndex = containingBlock != null && containingStatement != null
            ? containingBlock.Statements.IndexOf(containingStatement)
            : -1;
    }

    protected override BlockSyntax InsertDeclaration(BlockSyntax node, SyntaxNode declaration)
    {
        if (_insertIndex >= 0)
            return node.WithStatements(node.Statements.Insert(_insertIndex, (StatementSyntax)declaration));
        return node;
    }

    public override SyntaxNode Visit(SyntaxNode? node)
    {
        if (node is ExpressionSyntax expr && SyntaxFactory.AreEquivalent(expr, TargetExpression))
            return Replacement;

        return base.Visit(node)!;
    }

    public override SyntaxNode VisitBlock(BlockSyntax node)
    {
        var rewritten = (BlockSyntax)base.VisitBlock(node)!;
        return MaybeInsertDeclaration(node, rewritten);
    }
}

