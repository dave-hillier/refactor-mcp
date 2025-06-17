using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;

internal class ExtractMethodRewriter : CSharpSyntaxRewriter
{
    private readonly MethodDeclarationSyntax _containingMethod;
    private readonly ClassDeclarationSyntax? _containingClass;
    private readonly List<StatementSyntax> _statements;
    private readonly string _methodName;
    private readonly MethodDeclarationSyntax _newMethod;
    private readonly MethodDeclarationSyntax _updatedMethod;

    public ExtractMethodRewriter(
        MethodDeclarationSyntax containingMethod,
        ClassDeclarationSyntax? containingClass,
        List<StatementSyntax> statements,
        string methodName)
    {
        _containingMethod = containingMethod;
        _containingClass = containingClass;
        _statements = statements;
        _methodName = methodName;

        _newMethod = SyntaxFactory.MethodDeclaration(
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                methodName)
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword)))
            .WithBody(SyntaxFactory.Block(statements));

        var methodCall = SyntaxFactory.ExpressionStatement(
            SyntaxFactory.InvocationExpression(
                SyntaxFactory.IdentifierName(methodName)));

        var body = containingMethod.Body!;
        var updated = body.ReplaceNode(statements.First(), methodCall)!;
        foreach (var stmt in statements.Skip(1))
            updated = updated.RemoveNode(stmt, SyntaxRemoveOptions.KeepNoTrivia)!;

        _updatedMethod = containingMethod.WithBody(updated);
    }

    public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (node == _containingMethod)
            return _updatedMethod;
        return base.VisitMethodDeclaration(node)!;
    }

    public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var visited = (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!;
        if (_containingClass != null && node == _containingClass)
        {
            visited = visited.AddMembers(_newMethod);
        }
        return visited;
    }
}
