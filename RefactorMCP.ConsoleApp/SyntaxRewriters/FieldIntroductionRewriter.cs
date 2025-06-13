using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;

internal class FieldIntroductionRewriter : CSharpSyntaxRewriter
{
    private readonly ExpressionSyntax _targetExpression;
    private readonly IdentifierNameSyntax _fieldReference;
    private readonly FieldDeclarationSyntax _fieldDeclaration;
    private readonly ClassDeclarationSyntax? _containingClass;

    public FieldIntroductionRewriter(
        ExpressionSyntax targetExpression,
        IdentifierNameSyntax fieldReference,
        FieldDeclarationSyntax fieldDeclaration,
        ClassDeclarationSyntax? containingClass)
    {
        _targetExpression = targetExpression;
        _fieldReference = fieldReference;
        _fieldDeclaration = fieldDeclaration;
        _containingClass = containingClass;
    }

    public override SyntaxNode Visit(SyntaxNode node)
    {
        if (node is ExpressionSyntax expr && SyntaxFactory.AreEquivalent(expr, _targetExpression))
            return _fieldReference;

        return base.Visit(node);
    }

    public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var rewritten = (ClassDeclarationSyntax)base.VisitClassDeclaration(node);

        if (_containingClass != null && node == _containingClass)
        {
            rewritten = rewritten.WithMembers(rewritten.Members.Insert(0, _fieldDeclaration));
        }

        return rewritten;
    }
}

