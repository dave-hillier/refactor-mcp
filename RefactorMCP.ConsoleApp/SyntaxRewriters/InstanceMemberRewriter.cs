using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;

internal class InstanceMemberRewriter : CSharpSyntaxRewriter
{
    private readonly string _parameterName;
    private readonly HashSet<string> _knownInstanceMembers;

    public InstanceMemberRewriter(string parameterName, HashSet<string> knownInstanceMembers)
    {
        _parameterName = parameterName;
        _knownInstanceMembers = knownInstanceMembers;
    }

    public override SyntaxNode VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        if (node.Expression is ThisExpressionSyntax or BaseExpressionSyntax &&
            node.Name is IdentifierNameSyntax id &&
            _knownInstanceMembers.Contains(id.Identifier.ValueText))
        {
            var updated = node.WithExpression(SyntaxFactory.IdentifierName(_parameterName));
            return base.VisitMemberAccessExpression(updated)!;
        }

        return base.VisitMemberAccessExpression(node)!;
    }

    public override SyntaxNode? VisitConditionalAccessExpression(ConditionalAccessExpressionSyntax node)
    {
        // Handle cases like this.member?.Property or member?.Property
        // We need to rewrite the expression before the ?. but leave the binding expression alone
        var rewrittenExpression = (ExpressionSyntax?)Visit(node.Expression);
        if (rewrittenExpression != node.Expression)
        {
            return node.WithExpression(rewrittenExpression);
        }
        return base.VisitConditionalAccessExpression(node);
    }

    public override SyntaxNode? VisitNameColon(NameColonSyntax node)
    {
        // Override to handle named arguments manually and avoid casting issues
        // Don't call base.VisitNameColon as it has assumptions about syntax structure
        // Just visit the name part without transformation (parameter names should not be rewritten)
        return node;
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        var parent = node.Parent;
        if (parent is ParameterSyntax or TypeSyntax)
            return base.VisitIdentifierName(node);

        // Don't rewrite the name part of named arguments (e.g., "dbContextFactory" in "dbContextFactory: _value")
        if (parent is NameColonSyntax nameColon && nameColon.Name == node)
        {
            return base.VisitIdentifierName(node);
        }

        if (parent is AssignmentExpressionSyntax assign &&
            assign.Left == node &&
            assign.Parent is InitializerExpressionSyntax init &&
            (init.IsKind(SyntaxKind.ObjectInitializerExpression) || init.IsKind(SyntaxKind.WithInitializerExpression)))
        {
            // Property names in object initializers should not be qualified
            return base.VisitIdentifierName(node);
        }

        if (parent is NameColonSyntax nameColon2 &&
            nameColon2.Parent is SubpatternSyntax &&
            nameColon2.Parent.Parent is PropertyPatternClauseSyntax)
        {
            return base.VisitIdentifierName(node);
        }

        // Don't rewrite identifiers inside conditional access expressions (?.member)
        // The member binding expression expects a SimpleNameSyntax, not a MemberAccessExpressionSyntax
        if (parent is MemberBindingExpressionSyntax)
        {
            return base.VisitIdentifierName(node);
        }

        if (_knownInstanceMembers.Contains(node.Identifier.ValueText))
        {
            if (parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Expression == node)
            {
                return SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(_parameterName),
                    node);
            }
            else if (parent is not MemberAccessExpressionSyntax)
            {
                return SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(_parameterName),
                    node);
            }
        }

        return base.VisitIdentifierName(node);
    }
}

