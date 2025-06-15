using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;

public class VariableRemovalRewriter : CSharpSyntaxRewriter
{
    private readonly string _variableName;
    private readonly TextSpan _span;

    public VariableRemovalRewriter(string variableName, TextSpan span)
    {
        _variableName = variableName;
        _span = span;
    }

    public override SyntaxNode? VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
    {
        var variable = node.Declaration.Variables.FirstOrDefault(v => v.Identifier.ValueText == _variableName && v.Span.Equals(_span));
        if (variable == null)
            return base.VisitLocalDeclarationStatement(node);

        if (node.Declaration.Variables.Count == 1)
            return null;

        var newDecl = node.Declaration.WithVariables(SyntaxFactory.SeparatedList(node.Declaration.Variables.Where(v => v != variable)));
        return node.WithDeclaration(newDecl);
    }
}
