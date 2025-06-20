using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class InvocationHelpers
{
    internal static string? GetInvokedMethodName(InvocationExpressionSyntax node)
        => node.Expression switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            MemberAccessExpressionSyntax ma when ma.Name is IdentifierNameSyntax id => id.Identifier.ValueText,
            _ => null
        };

    internal static bool IsInvocationOf(InvocationExpressionSyntax node, string methodName)
        => GetInvokedMethodName(node) == methodName;

    internal static bool IsBaseInvocationOf(InvocationExpressionSyntax node, string methodName)
        => node.Expression is MemberAccessExpressionSyntax { Expression: BaseExpressionSyntax, Name: IdentifierNameSyntax id } && id.Identifier.ValueText == methodName;
}
