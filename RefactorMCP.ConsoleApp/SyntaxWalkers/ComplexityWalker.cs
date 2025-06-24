using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;

namespace RefactorMCP.ConsoleApp.SyntaxWalkers
{
    internal class ComplexityWalker : CSharpSyntaxWalker
    {
        public int Complexity { get; private set; } = 1;
        private int _depth;
        public int MaxDepth { get; private set; }

        private void Enter()
        {
            _depth++;
            if (_depth > MaxDepth)
                MaxDepth = _depth;
        }

        private void Exit() => _depth--;

        public override void VisitIfStatement(IfStatementSyntax node)
        {
            Complexity++;
            Enter();
            base.VisitIfStatement(node);
            Exit();
        }

        public override void VisitForStatement(ForStatementSyntax node)
        {
            Complexity++;
            Enter();
            base.VisitForStatement(node);
            Exit();
        }

        public override void VisitForEachStatement(ForEachStatementSyntax node)
        {
            Complexity++;
            Enter();
            base.VisitForEachStatement(node);
            Exit();
        }

        public override void VisitWhileStatement(WhileStatementSyntax node)
        {
            Complexity++;
            Enter();
            base.VisitWhileStatement(node);
            Exit();
        }

        public override void VisitDoStatement(DoStatementSyntax node)
        {
            Complexity++;
            Enter();
            base.VisitDoStatement(node);
            Exit();
        }

        public override void VisitSwitchStatement(SwitchStatementSyntax node)
        {
            var count = node.Sections.Count; // each case adds complexity
            Complexity += Math.Max(1, count);
            Enter();
            base.VisitSwitchStatement(node);
            Exit();
        }

        public override void VisitCatchClause(CatchClauseSyntax node)
        {
            Complexity++;
            Enter();
            base.VisitCatchClause(node);
            Exit();
        }

        public override void VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            if (node.IsKind(SyntaxKind.LogicalAndExpression) || node.IsKind(SyntaxKind.LogicalOrExpression))
                Complexity++;
            base.VisitBinaryExpression(node);
        }

        public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
        {
            Complexity++;
            Enter();
            base.VisitConditionalExpression(node);
            Exit();
        }
    }
}
