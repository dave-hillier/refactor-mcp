using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace RefactorMCP.ConsoleApp.SyntaxWalkers
{
    internal class ImplicitInstanceMemberWalker : CSharpSyntaxWalker
    {
        private readonly HashSet<string> _parameters = new();
        private readonly HashSet<string> _locals = new();
        public HashSet<string> Members { get; } = new();

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            foreach (var p in node.ParameterList.Parameters)
                _parameters.Add(p.Identifier.ValueText);
            base.VisitMethodDeclaration(node);
        }

        public override void VisitVariableDeclarator(VariableDeclaratorSyntax node)
        {
            _locals.Add(node.Identifier.ValueText);
            base.VisitVariableDeclarator(node);
        }

        public override void VisitIdentifierName(IdentifierNameSyntax node)
        {
            var name = node.Identifier.ValueText;
            var parent = node.Parent;

            if (_parameters.Contains(name) || _locals.Contains(name) || parent is TypeSyntax || SyntaxFacts.IsInNamespaceOrTypeContext(node))
            {
                base.VisitIdentifierName(node);
                return;
            }

            if (parent is AssignmentExpressionSyntax assign &&
                assign.Left == node &&
                assign.Parent is InitializerExpressionSyntax init &&
                (init.IsKind(SyntaxKind.ObjectInitializerExpression) || init.IsKind(SyntaxKind.WithInitializerExpression)))
            {
                base.VisitIdentifierName(node);
                return;
            }

            if (parent is NameColonSyntax { Parent: SubpatternSyntax { Parent: PropertyPatternClauseSyntax } })
            {
                base.VisitIdentifierName(node);
                return;
            }

            if (parent is MemberAccessExpressionSyntax ma && ma.Expression == node)
            {
                base.VisitIdentifierName(node);
                return;
            }

            if (parent is QualifiedNameSyntax qn && qn.Left == node)
            {
                base.VisitIdentifierName(node);
                return;
            }

            if (parent is InvocationExpressionSyntax)
            {
                base.VisitIdentifierName(node);
                return;
            }

            Members.Add(name);
            base.VisitIdentifierName(node);
        }
    }
}
