using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;

internal class VariableIntroductionRewriter : CSharpSyntaxRewriter
{
    private readonly ExpressionSyntax _targetExpression;
    private readonly IdentifierNameSyntax _variableReference;
    private readonly LocalDeclarationStatementSyntax _variableDeclaration;
    private readonly StatementSyntax? _containingStatement;
    private readonly BlockSyntax? _containingBlock;

    public VariableIntroductionRewriter(
        ExpressionSyntax targetExpression,
        IdentifierNameSyntax variableReference,
        LocalDeclarationStatementSyntax variableDeclaration,
        StatementSyntax? containingStatement,
        BlockSyntax? containingBlock)
    {
        _targetExpression = targetExpression;
        _variableReference = variableReference;
        _variableDeclaration = variableDeclaration;
        _containingStatement = containingStatement;
        _containingBlock = containingBlock;
    }

    public override SyntaxNode Visit(SyntaxNode? node)
    {
        if (node is ExpressionSyntax expr && SyntaxFactory.AreEquivalent(expr, _targetExpression))
            return _variableReference;

        return base.Visit(node)!;
    }

    public override SyntaxNode VisitBlock(BlockSyntax node)
    {
        int insertIndex = -1;
        if (_containingBlock != null && node == _containingBlock && _containingStatement != null)
            insertIndex = node.Statements.IndexOf(_containingStatement);

        var rewritten = (BlockSyntax)base.VisitBlock(node)!;

        if (_containingBlock != null && node == _containingBlock && _containingStatement != null && insertIndex >= 0)
        {
            rewritten = rewritten.WithStatements(rewritten.Statements.Insert(insertIndex, _variableDeclaration));
        }

        return rewritten;
    }
}

