using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal class ConstructorInjectionRewriter : CSharpSyntaxRewriter
{
    private readonly string _methodName;
    private readonly string _parameterName;
    private readonly int _parameterIndex;
    private readonly string _fieldName;
    private readonly TypeSyntax _parameterType;
    private readonly bool _useProperty;
    private bool _inTargetMethod;

    public ConstructorInjectionRewriter(string methodName, string parameterName, int parameterIndex, TypeSyntax parameterType, string fieldName, bool useProperty)
    {
        _methodName = methodName;
        _parameterName = parameterName;
        _parameterIndex = parameterIndex;
        _parameterType = parameterType;
        _fieldName = fieldName;
        _useProperty = useProperty;
    }

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var visited = node;
        if (node.Identifier.ValueText == _methodName)
        {
            _inTargetMethod = true;
            visited = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;
            _inTargetMethod = false;
            if (_parameterIndex < visited.ParameterList.Parameters.Count)
            {
                var newParams = visited.ParameterList.Parameters.RemoveAt(_parameterIndex);
                visited = visited.WithParameterList(visited.ParameterList.WithParameters(newParams));
            }
            return visited;
        }
        return base.VisitMethodDeclaration(node);
    }

    public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
    {
        if (_inTargetMethod && node.Identifier.ValueText == _parameterName)
        {
            return SyntaxFactory.IdentifierName(_fieldName).WithTriviaFrom(node);
        }
        return base.VisitIdentifierName(node);
    }

    public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        var visited = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
        if (InvocationHelpers.IsInvocationOf(visited, _methodName) &&
            _parameterIndex < visited.ArgumentList.Arguments.Count)
        {
            visited = AstTransformations.RemoveArgument(visited, _parameterIndex);
        }
        return visited;
    }

    public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        var visited = (ConstructorDeclarationSyntax)base.VisitConstructorDeclaration(node)!;
        if (!visited.ParameterList.Parameters.Any(p => p.Identifier.ValueText == _parameterName))
        {
            var param = SyntaxFactory.Parameter(SyntaxFactory.Identifier(_parameterName))
                .WithType(_parameterType);
            visited = visited.AddParameterListParameters(param);
            var assignment = SyntaxFactory.ExpressionStatement(
                SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    SyntaxFactory.IdentifierName(_fieldName),
                    SyntaxFactory.IdentifierName(_parameterName)));
            visited = visited.WithBody((visited.Body ?? SyntaxFactory.Block()).AddStatements(assignment));
        }
        return visited;
    }

    public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var visited = (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!;
        if (!visited.Members.OfType<FieldDeclarationSyntax>().Any(f => f.Declaration.Variables.Any(v => v.Identifier.ValueText == _fieldName)) &&
            !visited.Members.OfType<PropertyDeclarationSyntax>().Any(p => p.Identifier.ValueText == _fieldName))
        {
            MemberDeclarationSyntax member;
            if (_useProperty)
            {
                member = SyntaxFactory.PropertyDeclaration(_parameterType, _fieldName)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                    .WithAccessorList(
                        SyntaxFactory.AccessorList(
                            SyntaxFactory.List(new[]
                            {
                                SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                                SyntaxFactory.AccessorDeclaration(SyntaxKind.InitAccessorDeclaration)
                                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                            })));
            }
            else
            {
                member = SyntaxFactory.FieldDeclaration(
                        SyntaxFactory.VariableDeclaration(_parameterType)
                            .WithVariables(SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.VariableDeclarator(_fieldName))))
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword), SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));
            }
            visited = visited.WithMembers(visited.Members.Insert(0, member));
            if (!visited.Members.OfType<ConstructorDeclarationSyntax>().Any())
            {
                var param = SyntaxFactory.Parameter(SyntaxFactory.Identifier(_parameterName))
                    .WithType(_parameterType);
                var assignment = SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        SyntaxFactory.IdentifierName(_fieldName),
                        SyntaxFactory.IdentifierName(_parameterName)));
                var ctor = SyntaxFactory.ConstructorDeclaration(visited.Identifier)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                    .AddParameterListParameters(param)
                    .WithBody(SyntaxFactory.Block(assignment));
                visited = visited.AddMembers(ctor);
            }
        }
        return visited;
    }
}

