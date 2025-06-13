using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

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

internal class InstanceMemberUsageChecker : CSharpSyntaxRewriter
{
    private readonly HashSet<string> _knownInstanceMembers;
    public bool HasInstanceMemberUsage { get; private set; }

    public InstanceMemberUsageChecker(HashSet<string> knownInstanceMembers)
    {
        _knownInstanceMembers = knownInstanceMembers;
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        var parent = node.Parent;
        if (parent is ParameterSyntax || parent is TypeSyntax)
            return base.VisitIdentifierName(node);

        if (_knownInstanceMembers.Contains(node.Identifier.ValueText))
        {
            if (parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Expression == node)
            {
                HasInstanceMemberUsage = true;
            }
            else if (parent is not MemberAccessExpressionSyntax && parent is not InvocationExpressionSyntax)
            {
                HasInstanceMemberUsage = true;
            }
        }

        return base.VisitIdentifierName(node);
    }
}

internal class InstanceMemberRewriter : CSharpSyntaxRewriter
{
    private readonly string _parameterName;
    private readonly HashSet<string> _knownInstanceMembers;

    public InstanceMemberRewriter(string parameterName, HashSet<string> knownInstanceMembers)
    {
        _parameterName = parameterName;
        _knownInstanceMembers = knownInstanceMembers;
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        var parent = node.Parent;
        if (parent is ParameterSyntax || parent is TypeSyntax)
            return base.VisitIdentifierName(node);

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

internal class MethodCallChecker : CSharpSyntaxRewriter
{
    private readonly HashSet<string> _classMethodNames;
    public bool HasMethodCalls { get; private set; }

    public MethodCallChecker(HashSet<string> classMethodNames)
    {
        _classMethodNames = classMethodNames;
    }

    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (node.Expression is IdentifierNameSyntax identifier)
        {
            if (_classMethodNames.Contains(identifier.Identifier.ValueText))
            {
                HasMethodCalls = true;
            }
        }

        return base.VisitInvocationExpression(node);
    }
}

internal class MethodCallRewriter : CSharpSyntaxRewriter
{
    private readonly HashSet<string> _classMethodNames;
    private readonly string _parameterName;

    public MethodCallRewriter(HashSet<string> classMethodNames, string parameterName)
    {
        _classMethodNames = classMethodNames;
        _parameterName = parameterName;
    }

    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (node.Expression is IdentifierNameSyntax identifier)
        {
            if (_classMethodNames.Contains(identifier.Identifier.ValueText))
            {
                var memberAccess = SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(_parameterName),
                    identifier);

                return node.WithExpression(memberAccess);
            }
        }

        return base.VisitInvocationExpression(node);
    }
}

internal class StaticFieldChecker : CSharpSyntaxRewriter
{
    private readonly HashSet<string> _staticFieldNames;
    public bool HasStaticFieldReferences { get; private set; }

    public StaticFieldChecker(HashSet<string> staticFieldNames)
    {
        _staticFieldNames = staticFieldNames;
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        var parent = node.Parent;
        if (parent is ParameterSyntax || parent is TypeSyntax)
            return base.VisitIdentifierName(node);

        if (_staticFieldNames.Contains(node.Identifier.ValueText))
        {
            if (parent is not MemberAccessExpressionSyntax ||
                (parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Name == node))
            {
                HasStaticFieldReferences = true;
            }
        }

        return base.VisitIdentifierName(node);
    }
}

internal class StaticFieldRewriter : CSharpSyntaxRewriter
{
    private readonly HashSet<string> _staticFieldNames;
    private readonly string _sourceClassName;

    public StaticFieldRewriter(HashSet<string> staticFieldNames, string sourceClassName)
    {
        _staticFieldNames = staticFieldNames;
        _sourceClassName = sourceClassName;
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        var parent = node.Parent;
        if (parent is ParameterSyntax || parent is TypeSyntax)
            return base.VisitIdentifierName(node);

        if (_staticFieldNames.Contains(node.Identifier.ValueText))
        {
            if (parent is not MemberAccessExpressionSyntax ||
                (parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Name == node))
            {
                return SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(_sourceClassName),
                    node);
            }
        }

        return base.VisitIdentifierName(node);
    }
}

internal class FieldIntroductionRewriter : CSharpSyntaxRewriter
{
    private readonly ExpressionSyntax _targetExpression;
    private readonly IdentifierNameSyntax _fieldReference;
    private readonly FieldDeclarationSyntax _fieldDeclaration;
    private readonly ClassDeclarationSyntax? _containingClass;

    public FieldIntroductionRewriter(
        ExpressionSyntax targetExpression,
        IdentifierNameSyntax fieldReference,
        FieldDeclarationSyntax fieldDeclaration,
        ClassDeclarationSyntax? containingClass)
    {
        _targetExpression = targetExpression;
        _fieldReference = fieldReference;
        _fieldDeclaration = fieldDeclaration;
        _containingClass = containingClass;
    }

    public override SyntaxNode Visit(SyntaxNode node)
    {
        if (node is ExpressionSyntax expr && SyntaxFactory.AreEquivalent(expr, _targetExpression))
            return _fieldReference;

        return base.Visit(node);
    }

    public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var rewritten = (ClassDeclarationSyntax)base.VisitClassDeclaration(node);

        if (_containingClass != null && node == _containingClass)
        {
            rewritten = rewritten.WithMembers(rewritten.Members.Insert(0, _fieldDeclaration));
        }

        return rewritten;
    }
}

internal class VariableIntroductionRewriter : CSharpSyntaxRewriter
{
    private readonly ExpressionSyntax _targetExpression;
    private readonly IdentifierNameSyntax _variableReference;
    private readonly LocalDeclarationStatementSyntax _variableDeclaration;
    private readonly StatementSyntax? _containingStatement;
    private readonly BlockSyntax? _containingBlock;

    public VariableIntroductionRewriter(
        ExpressionSyntax targetExpression,
        IdentifierNameSyntax variableReference,
        LocalDeclarationStatementSyntax variableDeclaration,
        StatementSyntax? containingStatement,
        BlockSyntax? containingBlock)
    {
        _targetExpression = targetExpression;
        _variableReference = variableReference;
        _variableDeclaration = variableDeclaration;
        _containingStatement = containingStatement;
        _containingBlock = containingBlock;
    }

    public override SyntaxNode Visit(SyntaxNode node)
    {
        if (node is ExpressionSyntax expr && SyntaxFactory.AreEquivalent(expr, _targetExpression))
            return _variableReference;

        return base.Visit(node);
    }

    public override SyntaxNode VisitBlock(BlockSyntax node)
    {
        int insertIndex = -1;
        if (_containingBlock != null && node == _containingBlock && _containingStatement != null)
            insertIndex = node.Statements.IndexOf(_containingStatement);

        var rewritten = (BlockSyntax)base.VisitBlock(node);

        if (_containingBlock != null && node == _containingBlock && _containingStatement != null && insertIndex >= 0)
        {
            rewritten = rewritten.WithStatements(rewritten.Statements.Insert(insertIndex, _variableDeclaration));
        }

        return rewritten;
    }
}

internal class ExtensionMethodRewriter : CSharpSyntaxRewriter
{
    private readonly string _parameterName;
    private readonly string _parameterType;
    private readonly SemanticModel? _semanticModel;
    private readonly INamedTypeSymbol? _typeSymbol;
    private readonly HashSet<string>? _knownMembers;

    public ExtensionMethodRewriter(string parameterName, string parameterType, SemanticModel semanticModel, INamedTypeSymbol typeSymbol)
    {
        _parameterName = parameterName;
        _parameterType = parameterType;
        _semanticModel = semanticModel;
        _typeSymbol = typeSymbol;
    }

    public ExtensionMethodRewriter(string parameterName, string parameterType, HashSet<string> knownMembers)
    {
        _parameterName = parameterName;
        _parameterType = parameterType;
        _knownMembers = knownMembers;
    }

    public MethodDeclarationSyntax Rewrite(MethodDeclarationSyntax method)
    {
        return (MethodDeclarationSyntax)Visit(method)!;
    }

    public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var thisParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier(_parameterName))
            .WithType(SyntaxFactory.ParseTypeName(_parameterType))
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.ThisKeyword));

        var updated = node.WithParameterList(node.ParameterList.AddParameters(thisParam));
        updated = AstTransformations.EnsureStaticModifier(updated);
        return base.VisitMethodDeclaration(updated);
    }

    public override SyntaxNode VisitThisExpression(ThisExpressionSyntax node)
    {
        return SyntaxFactory.IdentifierName(_parameterName).WithTriviaFrom(node);
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        bool qualify = false;

        if (_semanticModel != null)
        {
            var sym = _semanticModel.GetSymbolInfo(node).Symbol;
            if (sym is IFieldSymbol or IPropertySymbol or IMethodSymbol &&
                SymbolEqualityComparer.Default.Equals(sym.ContainingType, _typeSymbol) &&
                !sym.IsStatic && node.Parent is not MemberAccessExpressionSyntax)
            {
                qualify = true;
            }
        }
        else if (_knownMembers != null &&
                 _knownMembers.Contains(node.Identifier.ValueText) &&
                 node.Parent is not MemberAccessExpressionSyntax)
        {
            qualify = true;
        }

        if (qualify)
        {
            return SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(_parameterName),
                    node.WithoutTrivia())
                .WithTriviaFrom(node);
        }

        return base.VisitIdentifierName(node);
    }
}

internal class ParameterIntroductionRewriter : CSharpSyntaxRewriter
{
    private readonly ExpressionSyntax _targetExpression;
    private readonly string _methodName;
    private readonly ParameterSyntax _parameter;
    private readonly IdentifierNameSyntax _parameterReference;

    public ParameterIntroductionRewriter(
        ExpressionSyntax targetExpression,
        string methodName,
        ParameterSyntax parameter,
        IdentifierNameSyntax parameterReference)
    {
        _targetExpression = targetExpression;
        _methodName = methodName;
        _parameter = parameter;
        _parameterReference = parameterReference;
    }

    public override SyntaxNode Visit(SyntaxNode node)
    {
        if (node is ExpressionSyntax expr && SyntaxFactory.AreEquivalent(expr, _targetExpression))
            return _parameterReference;

        return base.Visit(node);
    }

    public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        var visited = (InvocationExpressionSyntax)base.VisitInvocationExpression(node);
        var isTarget =
            (visited.Expression is IdentifierNameSyntax id && id.Identifier.ValueText == _methodName) ||
            (visited.Expression is MemberAccessExpressionSyntax ma && ma.Name.Identifier.ValueText == _methodName);

        if (isTarget)
        {
            var newArgs = visited.ArgumentList.AddArguments(SyntaxFactory.Argument(_targetExpression.WithoutTrivia()));
            visited = visited.WithArgumentList(newArgs);
        }

        return visited;
    }

    public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var visited = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node);
        if (node.Identifier.ValueText == _methodName)
            visited = visited.AddParameterListParameters(_parameter);

        return visited;

    }
}
