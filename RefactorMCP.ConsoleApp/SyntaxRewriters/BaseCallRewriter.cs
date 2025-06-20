using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactorMCP.ConsoleApp.SyntaxRewriters
{
    internal class BaseCallRewriter : CSharpSyntaxRewriter
    {
        private readonly string _methodName;
        private readonly string _parameterName;
        private readonly string _wrapperName;

        public BaseCallRewriter(string methodName, string parameterName, string wrapperName)
        {
            _methodName = methodName;
            _parameterName = parameterName;
            _wrapperName = wrapperName;
        }

        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (InvocationHelpers.IsBaseInvocationOf(node, _methodName))
            {
                var memberAccess = SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(_parameterName),
                    SyntaxFactory.IdentifierName(_wrapperName));
                return node.WithExpression(memberAccess);
            }

            return base.VisitInvocationExpression(node)!;
        }
    }
}
