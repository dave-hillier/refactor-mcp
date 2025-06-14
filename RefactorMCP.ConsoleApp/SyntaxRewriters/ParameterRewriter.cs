using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;

internal class ParameterRewriter : CSharpSyntaxRewriter
{
    private readonly Dictionary<string, ExpressionSyntax> _map;
    public ParameterRewriter(Dictionary<string, ExpressionSyntax> map)
    {
        _map = map;
    }
    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        if (_map.TryGetValue(node.Identifier.ValueText, out var expr))
            return expr;
        return base.VisitIdentifierName(node);
    }
}

