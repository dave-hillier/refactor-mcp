using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
namespace RefactorMCP.ConsoleApp.SyntaxWalkers
{

    internal class MethodAnalysisWalker : CSharpSyntaxWalker
    {
        private readonly HashSet<string> _instanceMembers;
        private readonly HashSet<string> _methodNames;
        private readonly string _methodName;

        public bool UsesInstanceMembers { get; private set; }
        public bool CallsOtherMethods { get; private set; }
        public bool IsRecursive { get; private set; }

        public MethodAnalysisWalker(HashSet<string> instanceMembers, HashSet<string> methodNames, string methodName)
        {
            _instanceMembers = instanceMembers;
            _methodNames = methodNames;
            _methodName = methodName;
        }

        public override void VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (_instanceMembers.Contains(node.Identifier.ValueText))
            {
                var parent = node.Parent;
                if (parent is not MemberAccessExpressionSyntax ||
                    (parent is MemberAccessExpressionSyntax ma &&
                     (ma.Expression == node ||
                      ma.Expression is ThisExpressionSyntax)))
                {
                    // A bare name, a receiver (member.Something), or an explicit
                    // this. qualification all reach an instance member. A base.
                    // qualification is deliberately not counted: treating it as
                    // instance usage makes the move rewrite the receiver to the
                    // injected parameter before the base call can be redirected
                    // to the wrapper the move creates.
                    UsesInstanceMembers = true;
                }
            }

            if (_methodNames.Contains(node.Identifier.ValueText))
            {
                var parent = node.Parent;
                if (parent is not InvocationExpressionSyntax &&
                    (parent is not MemberAccessExpressionSyntax ||
                     (parent is MemberAccessExpressionSyntax ma && ma.Expression is ThisExpressionSyntax)))
                {
                    if (node.Identifier.ValueText == _methodName)
                        IsRecursive = true;
                    else
                        CallsOtherMethods = true;
                }
            }

            base.VisitIdentifierName(node);
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (node.Expression is IdentifierNameSyntax id && _methodNames.Contains(id.Identifier.ValueText))
            {
                if (id.Identifier.ValueText == _methodName)
                {
                    IsRecursive = true;
                }
                else
                {
                    CallsOtherMethods = true;
                }
            }
            base.VisitInvocationExpression(node);
        }
    }
}
