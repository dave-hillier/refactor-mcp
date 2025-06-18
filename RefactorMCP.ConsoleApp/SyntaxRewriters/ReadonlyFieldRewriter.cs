using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;

internal class ReadonlyFieldRewriter : CSharpSyntaxRewriter
{
    private readonly string _fieldName;
    private readonly ExpressionSyntax? _initializer;

    public ReadonlyFieldRewriter(string fieldName, ExpressionSyntax? initializer)
    {
        _fieldName = fieldName;
        _initializer = initializer;
    }

    public override SyntaxNode? VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        var variable = node.Declaration.Variables.FirstOrDefault(v => v.Identifier.ValueText == _fieldName);
        if (variable == null)
            return base.VisitFieldDeclaration(node);

        var newVariable = variable.WithInitializer(null);
        var newDecl = node.Declaration.ReplaceNode(variable, newVariable);
        var modifiers = node.Modifiers;
        if (!modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword)))
            modifiers = modifiers.Add(SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));

        return node.WithDeclaration(newDecl).WithModifiers(modifiers);
    }

    public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        var visited = (ConstructorDeclarationSyntax)base.VisitConstructorDeclaration(node)!;
        if (_initializer != null)
        {
            var assignment = SyntaxFactory.ExpressionStatement(
                SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    SyntaxFactory.IdentifierName(_fieldName),
                    _initializer));
            var body = visited.Body ?? SyntaxFactory.Block();
            visited = visited.WithBody(body.AddStatements(assignment));
        }
        return visited;
    }
}

