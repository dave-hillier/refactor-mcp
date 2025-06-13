using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;

public class MethodRemovalRewriter : CSharpSyntaxRewriter
{
    private readonly string _methodName;

    public MethodRemovalRewriter(string methodName)
    {
        _methodName = methodName;
    }

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (node.Identifier.ValueText == _methodName)
            return null;

        return base.VisitMethodDeclaration(node);
    }
}

