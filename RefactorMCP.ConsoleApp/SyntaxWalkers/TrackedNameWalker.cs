using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
namespace RefactorMCP.ConsoleApp.SyntaxWalkers
{

    internal abstract class TrackedNameWalker : CSharpSyntaxWalker
    {
        private readonly HashSet<string> _names;
        private readonly Action<string>? _onMatch;

        public HashSet<string> Matches { get; } = new();

        protected TrackedNameWalker(HashSet<string> names, Action<string>? onMatch = null)
        {
            _names = names;
            _onMatch = onMatch;
        }

        protected bool IsTarget(string name) => _names.Contains(name);

        protected static bool IsParameterOrType(SyntaxNode? node) =>
            node is ParameterSyntax or TypeSyntax;

        protected static bool IsThisMember(MemberAccessExpressionSyntax ma, IdentifierNameSyntax node) =>
            ma.Expression is ThisExpressionSyntax && ma.Name == node;

        protected static bool IsMemberExpression(IdentifierNameSyntax node, MemberAccessExpressionSyntax ma) =>
            ma.Expression == node;

        protected static string? GetInvocationName(InvocationExpressionSyntax node)
        {
            return node.Expression switch
            {
                IdentifierNameSyntax id => id.Identifier.ValueText,
                MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: IdentifierNameSyntax id } => id.Identifier.ValueText,
                _ => null
            };
        }

        protected virtual bool ShouldRecordIdentifier(IdentifierNameSyntax node)
        {
            var parent = node.Parent;
            if (!IsTarget(node.Identifier.ValueText) || IsParameterOrType(parent))
                return false;

            if (parent is MemberAccessExpressionSyntax memberAccess)
            {
                if (IsThisMember(memberAccess, node) || IsMemberExpression(node, memberAccess))
                    return true;
                return false;
            }

            if (parent is InvocationExpressionSyntax)
                return false;

            return true;
        }

        public override void VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (ShouldRecordIdentifier(node))
                RecordMatch(node.Identifier.ValueText);
            base.VisitIdentifierName(node);
        }

        protected virtual bool TryRecordInvocation(InvocationExpressionSyntax node) => false;

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (!TryRecordInvocation(node))
                base.VisitInvocationExpression(node);
        }

        protected void RecordMatch(string name)
        {
            Matches.Add(name);
            _onMatch?.Invoke(name);
        }
    }
}
