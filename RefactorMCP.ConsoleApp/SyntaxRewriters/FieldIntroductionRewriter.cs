using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal class FieldIntroductionRewriter : ExpressionIntroductionRewriter<ClassDeclarationSyntax>
{
    private readonly FieldDeclarationSyntax _fieldDeclaration;

    public FieldIntroductionRewriter(
        ExpressionSyntax targetExpression,
        IdentifierNameSyntax fieldReference,
        FieldDeclarationSyntax fieldDeclaration,
        ClassDeclarationSyntax? containingClass)
        : base(targetExpression, fieldReference, fieldDeclaration, containingClass)
    {
        _fieldDeclaration = fieldDeclaration;
    }

    protected override ClassDeclarationSyntax InsertDeclaration(ClassDeclarationSyntax node, SyntaxNode declaration)
    {
        return node.WithMembers(node.Members.Insert(0, (MemberDeclarationSyntax)declaration));
    }

    public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var rewritten = (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!;
        return MaybeInsertDeclaration(node, rewritten);
    }
}

