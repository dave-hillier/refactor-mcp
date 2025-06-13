using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;

public class FieldRemovalRewriter : CSharpSyntaxRewriter
{
    private readonly string _fieldName;

    public FieldRemovalRewriter(string fieldName)
    {
        _fieldName = fieldName;
    }

    public override SyntaxNode? VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        var variable = node.Declaration.Variables.FirstOrDefault(v => v.Identifier.ValueText == _fieldName);
        if (variable == null)
            return base.VisitFieldDeclaration(node);

        if (node.Declaration.Variables.Count == 1)
            return null;

        var newDecl = node.Declaration.WithVariables(SyntaxFactory.SeparatedList(node.Declaration.Variables.Where(v => v != variable)));
        return node.WithDeclaration(newDecl);
    }
}

