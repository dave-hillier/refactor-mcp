using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Threading;

/// <summary>
/// What converting between a lambda and a method group must keep: the receiver the
/// delegate is bound to, the method it calls, the delegate type it converts to, and the
/// overload of any call it is passed to.
/// </summary>
internal static class DelegateConversion
{
    private static readonly SymbolDisplayFormat Format = SymbolDisplayFormat.FullyQualifiedFormat
        .WithMemberOptions(SymbolDisplayMemberOptions.IncludeContainingType | SymbolDisplayMemberOptions.IncludeParameters |
                           SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeRef)
        .WithGenericsOptions(SymbolDisplayGenericsOptions.IncludeTypeParameters)
        .WithParameterOptions(SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeParamsRefOut)
        .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>
    /// A method group reads its receiver once, when the delegate is created; a lambda reads
    /// it on every call. They agree only when the receiver cannot change in between: no
    /// receiver, <c>this</c> or <c>base</c>, a type, a readonly field of such a receiver, or
    /// a local or value parameter that the member never assigns after declaring it.
    /// </summary>
    public static bool IsStableReceiver(ExpressionSyntax? receiver, SemanticModel model, CancellationToken cancellationToken)
    {
        switch (receiver)
        {
            case null:
            case ThisExpressionSyntax:
            case BaseExpressionSyntax:
            case PredefinedTypeSyntax:
                return true;
            case ParenthesizedExpressionSyntax parenthesized:
                return IsStableReceiver(parenthesized.Expression, model, cancellationToken);
        }

        var symbol = model.GetSymbolInfo(receiver, cancellationToken).Symbol;
        switch (symbol)
        {
            case INamespaceOrTypeSymbol:
                return true;
            case IFieldSymbol field when field.IsReadOnly || field.IsConst:
                return receiver is not MemberAccessExpressionSyntax access ||
                       IsStableReceiver(access.Expression, model, cancellationToken);
            case ILocalSymbol { IsRef: false } local:
                return !IsAssignedAfterDeclaration(receiver, local, model, cancellationToken);
            case IParameterSymbol { RefKind: RefKind.None } parameter:
                return !IsAssignedAfterDeclaration(receiver, parameter, model, cancellationToken);
            default:
                return false;
        }
    }

    private static bool IsAssignedAfterDeclaration(SyntaxNode use, ISymbol variable, SemanticModel model, CancellationToken cancellationToken)
    {
        var scope = use.Ancestors().LastOrDefault(a => a is MemberDeclarationSyntax and not BaseTypeDeclarationSyntax and not BaseNamespaceDeclarationSyntax)
            ?? use.SyntaxTree.GetRoot(cancellationToken);
        return scope.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(n => n.Identifier.ValueText == variable.Name)
            .Any(n => LocalVariableTarget.IsWrite(n) &&
                      SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(n, cancellationToken).Symbol, variable));
    }

    public static string Display(ISymbol? symbol) => symbol?.ToDisplayString(Format) ?? "";

    /// <summary>The call or object creation whose argument list holds the node, if any.</summary>
    public static ExpressionSyntax? ContainingCall(SyntaxNode node) =>
        node.Parent is ArgumentSyntax { Parent: ArgumentListSyntax { Parent: ExpressionSyntax call } }
            ? call
            : null;

    /// <summary>
    /// What a delegate expression resolves to: the method it calls, the delegate type it
    /// converts to and the overload of the call it is passed to.
    /// </summary>
    public sealed record Binding(string Method, string DelegateType, string EnclosingCall);

    public static Binding Bind(SemanticModel model, ExpressionSyntax expression, ExpressionSyntax methodExpression, CancellationToken cancellationToken)
    {
        var call = ContainingCall(expression);
        return new Binding(
            Display(model.GetSymbolInfo(methodExpression, cancellationToken).Symbol),
            Display(model.GetTypeInfo(expression, cancellationToken).ConvertedType),
            call == null ? "" : Display(model.GetSymbolInfo(call, cancellationToken).Symbol));
    }

    private static readonly SyntaxAnnotation Replaced = new(nameof(DelegateConversion));

    /// <summary>
    /// Replaces the delegate expression, then checks that the document compiles no worse
    /// and that the replacement binds as the original did; otherwise refuses.
    /// </summary>
    public static async Task<SyntaxNode> ReplaceCheckedAsync(
        PositionTarget target,
        ExpressionSyntax original,
        ExpressionSyntax replacement,
        Binding expected,
        Func<ExpressionSyntax, ExpressionSyntax> methodExpressionOf,
        string refusal,
        CancellationToken cancellationToken)
    {
        var newRoot = target.Root.ReplaceNode(original, replacement.WithAdditionalAnnotations(Replaced));
        var changed = target.Document.WithSyntaxRoot(newRoot);
        var model = (await changed.GetSemanticModelAsync(cancellationToken))!;
        var replaced = (ExpressionSyntax)(await changed.GetSyntaxRootAsync(cancellationToken))!
            .GetAnnotatedNodes(Replaced).Single();

        var errorsBefore = target.Model.GetDiagnostics(cancellationToken: cancellationToken).Count(d => d.Severity == DiagnosticSeverity.Error);
        var errorsAfter = model.GetDiagnostics(cancellationToken: cancellationToken).Count(d => d.Severity == DiagnosticSeverity.Error);
        var actual = Bind(model, replaced, methodExpressionOf(replaced), cancellationToken);
        if (errorsAfter > errorsBefore || actual != expected)
            throw new McpException(refusal);

        return newRoot;
    }
}
