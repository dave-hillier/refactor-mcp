using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;

internal class InlineInvocationRewriter : CSharpSyntaxRewriter
{
    private readonly MethodDeclarationSyntax _method;
    private readonly IMethodSymbol? _methodSymbol;
    private readonly SemanticModel? _semanticModel;

    public InlineInvocationRewriter(MethodDeclarationSyntax method)
    {
        _method = method;
    }

    public InlineInvocationRewriter(MethodDeclarationSyntax method, SemanticModel semanticModel, IMethodSymbol methodSymbol)
    {
        _method = method;
        _semanticModel = semanticModel;
        _methodSymbol = methodSymbol;
    }

    private bool IsTargetInvocation(InvocationExpressionSyntax node)
    {
        if (_semanticModel != null && _methodSymbol != null)
        {
            var symbol = _semanticModel.GetSymbolInfo(node).Symbol as IMethodSymbol;
            if (symbol != null && SymbolEqualityComparer.Default.Equals(symbol, _methodSymbol))
                return true;
        }

        if (node.Expression is IdentifierNameSyntax id)
            return id.Identifier.ValueText == _method.Identifier.ValueText;

        return false;
    }

    public override SyntaxNode VisitBlock(BlockSyntax node)
    {
        var newStatements = new List<StatementSyntax>();
        foreach (var stmt in node.Statements)
        {
            if (stmt is ExpressionStatementSyntax expr &&
                expr.Expression is InvocationExpressionSyntax invocation &&
                IsTargetInvocation(invocation) &&
                _method.ReturnType is PredefinedTypeSyntax pts &&
                pts.Keyword.IsKind(SyntaxKind.VoidKeyword))
            {
                var argMap = _method.ParameterList.Parameters
                    .Zip(invocation.ArgumentList.Arguments, (p, a) => new { p, a })
                    .ToDictionary(x => x.p.Identifier.ValueText, x => x.a.Expression);

                var rewriter = new ParameterRewriter(argMap);
                var stmts = _method.Body!.Statements.Select(s => (StatementSyntax)rewriter.Visit(s)!);
                newStatements.AddRange(stmts);
            }
            else
            {
                newStatements.Add((StatementSyntax)Visit(stmt)!);
            }
        }

        return node.WithStatements(SyntaxFactory.List(newStatements));
    }
}

