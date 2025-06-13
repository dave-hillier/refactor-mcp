using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

internal class MethodCallChecker : CSharpSyntaxWalker
{
    private readonly HashSet<string> _classMethodNames;
    public bool HasMethodCalls { get; private set; }

    public MethodCallChecker(HashSet<string> classMethodNames)
    {
        _classMethodNames = classMethodNames;
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (node.Expression is IdentifierNameSyntax identifier)
        {
            if (_classMethodNames.Contains(identifier.Identifier.ValueText))
            {
                HasMethodCalls = true;
            }
        }
        base.VisitInvocationExpression(node);
    }
}

