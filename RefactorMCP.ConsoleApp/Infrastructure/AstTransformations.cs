using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System.Linq;

internal static class AstTransformations
{
    internal static MethodDeclarationSyntax AddParameter(MethodDeclarationSyntax method, string name, string type)
    {
        var parameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier(name))
            .WithType(SyntaxFactory.ParseTypeName(type));
        return method.WithParameterList(method.ParameterList.AddParameters(parameter));
    }

    internal static MethodDeclarationSyntax ReplaceThisReferences(MethodDeclarationSyntax method, string parameterName)
    {
        return method.ReplaceNodes(
            method.DescendantNodes().OfType<ThisExpressionSyntax>(),
            (_, _) => SyntaxFactory.IdentifierName(parameterName));
    }

    internal static MethodDeclarationSyntax QualifyInstanceMembers(MethodDeclarationSyntax method, string parameterName, SemanticModel semanticModel, INamedTypeSymbol typeSymbol)
    {
        return method.ReplaceNodes(
            method.DescendantNodes().OfType<IdentifierNameSyntax>().Where(id =>
            {
                var sym = semanticModel.GetSymbolInfo(id).Symbol;
                return sym is IFieldSymbol or IPropertySymbol or IMethodSymbol &&
                       SymbolEqualityComparer.Default.Equals(sym.ContainingType, typeSymbol) &&
                       !sym.IsStatic && id.Parent is not MemberAccessExpressionSyntax;
            }),
            (old, _) => SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(parameterName),
                SyntaxFactory.IdentifierName(old.Identifier)));
    }

    internal static MethodDeclarationSyntax QualifyInstanceMembers(MethodDeclarationSyntax method, string parameterName, HashSet<string> members)
    {
        return method.ReplaceNodes(
            method.DescendantNodes().OfType<IdentifierNameSyntax>().Where(id =>
                members.Contains(id.Identifier.ValueText) && id.Parent is not MemberAccessExpressionSyntax),
            (old, _) => SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(parameterName),
                SyntaxFactory.IdentifierName(old.Identifier)));
    }

    internal static MethodDeclarationSyntax EnsureStaticModifier(MethodDeclarationSyntax method)
    {
        var modifiers = method.Modifiers;
        if (!modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
            modifiers = modifiers.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));
        return method.WithModifiers(modifiers);
    }

    internal static InvocationExpressionSyntax AddArgument(
        InvocationExpressionSyntax invocation,
        ExpressionSyntax argumentExpression,
        SyntaxGenerator generator)
    {
        var argument = (ArgumentSyntax)generator.Argument(argumentExpression);
        return invocation.WithArgumentList(invocation.ArgumentList.AddArguments(argument));
    }

    internal static InvocationExpressionSyntax RemoveArgument(
        InvocationExpressionSyntax invocation,
        int argumentIndex)
    {
        var newArgs = invocation.ArgumentList.Arguments.RemoveAt(argumentIndex);
        return invocation.WithArgumentList(invocation.ArgumentList.WithArguments(newArgs));
    }
}
