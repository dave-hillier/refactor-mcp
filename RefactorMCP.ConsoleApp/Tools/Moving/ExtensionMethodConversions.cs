using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Simplification;
using ModelContextProtocol;

namespace RefactorMCP.ConsoleApp.Tools.Moving;

/// <summary>
/// Turns a static method of a static class into an extension method on its
/// first parameter, and back, rewriting the calls to match.
/// </summary>
internal static class ExtensionMethodConversions
{
    public static async Task<string> ToExtensionAsync(Solution solution, IMethodSymbol method, CancellationToken cancellationToken)
    {
        var type = method.ContainingType;
        if (method.IsExtensionMethod)
            throw new McpException($"Error: {method.Name} is already an extension method");
        if (!type.IsStatic)
            throw new McpException($"Error: {type.Name} is not a static class, so it cannot declare extension methods");
        if (type.ContainingType is not null || type.IsGenericType)
            throw new McpException($"Error: {type.Name} is nested or generic, so it cannot declare extension methods");
        if (method.Parameters.Length == 0)
            throw new McpException($"Error: {method.Name} has no parameters, so there is nothing to extend");
        if (method.Parameters[0] is { RefKind: RefKind.Out } or { IsParams: true } || method.Parameters[0].Type is IPointerTypeSymbol)
            throw new McpException($"Error: The first parameter of {method.Name} cannot become the extended value");

        var edits = new Dictionary<DocumentId, Dictionary<SyntaxNode, Func<SyntaxNode, SyntaxNode>>>();
        foreach (var (document, invocation, model) in await InvocationsAsync(solution, method, cancellationToken))
        {
            if (model.GetOperation(invocation, cancellationToken) is not Microsoft.CodeAnalysis.Operations.IInvocationOperation operation
                || !CanBeReceiver(operation, model, invocation, type))
            {
                continue;
            }

            EditsFor(edits, document.Id)[invocation] = rewritten =>
            {
                var call = (InvocationExpressionSyntax)rewritten;
                var arguments = call.ArgumentList.Arguments;
                return SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            Receiver(arguments[0].Expression),
                            MethodName(call.Expression).WithoutTrivia()),
                        call.ArgumentList.WithArguments(arguments.RemoveAt(0)))
                    .WithTriviaFrom(call);
            };
        }

        var declaration = await DeclarationAsync(method, cancellationToken);
        var first = declaration.ParameterList.Parameters[0];
        var withThis = first.WithModifiers(first.Modifiers.Insert(0, SyntaxFactory.Token(SyntaxKind.ThisKeyword).WithTrailingTrivia(SyntaxFactory.Space)));
        var declaringDocument = solution.GetDocument(declaration.SyntaxTree)!;
        EditsFor(edits, declaringDocument.Id)[first] = _ => withThis;

        await ApplyEditsAsync(solution, edits, cancellationToken);
        return $"Successfully converted {method.Name} to an extension method";
    }

    public static async Task<string> ToStaticAsync(Solution solution, IMethodSymbol method, CancellationToken cancellationToken)
    {
        if (!method.IsExtensionMethod)
            throw new McpException($"Error: {method.Name} is not an extension method");

        var edits = new Dictionary<DocumentId, Dictionary<SyntaxNode, Func<SyntaxNode, SyntaxNode>>>();
        foreach (var (document, invocation, model) in await InvocationsAsync(solution, method, cancellationToken))
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax access
                || model.GetSymbolInfo(access, cancellationToken).Symbol is not IMethodSymbol { ReducedFrom: not null })
            {
                continue;
            }

            EditsFor(edits, document.Id)[invocation] = rewritten =>
            {
                var call = (InvocationExpressionSyntax)rewritten;
                var member = (MemberAccessExpressionSyntax)call.Expression;
                var receiver = member.Expression is ParenthesizedExpressionSyntax parenthesized
                    ? parenthesized.Expression
                    : member.Expression;
                // Only a plain name may be reduced to a bare call inside the
                // class; the simplifier would also drop explicit type arguments.
                ExpressionSyntax target = SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    MovingSupport.QualifiedType(method.ContainingType),
                    member.Name);
                if (member.Name is IdentifierNameSyntax)
                    target = target.WithAdditionalAnnotations(Simplifier.Annotation);
                return SyntaxFactory.InvocationExpression(
                        target,
                        call.ArgumentList.WithArguments(WithFirst(call.ArgumentList.Arguments, SyntaxFactory.Argument(receiver.WithoutTrivia()))))
                    .WithTriviaFrom(call);
            };
        }

        foreach (var reference in await ReferencesAsync(solution, method, cancellationToken))
        {
            var root = await reference.Document.GetSyntaxRootAsync(cancellationToken);
            var node = root!.FindNode(reference.Location.SourceSpan, getInnermostNodeForTie: true);
            if (node.Parent is MemberBindingExpressionSyntax)
                throw new McpException($"Error: {method.Name} is called with null-conditional access (?.) in {Path.GetFileName(reference.Document.FilePath)}; rewrite that call first");
            if (node.Parent is MemberAccessExpressionSyntax { Parent: not InvocationExpressionSyntax } access
                && access.Name == node
                && (await reference.Document.GetSemanticModelAsync(cancellationToken))!.GetSymbolInfo(access, cancellationToken).Symbol is IMethodSymbol { ReducedFrom: not null })
            {
                throw new McpException($"Error: {method.Name} is used as a method group on a value in {Path.GetFileName(reference.Document.FilePath)}; it cannot be converted");
            }
        }

        var declaration = await DeclarationAsync(method, cancellationToken);
        var first = declaration.ParameterList.Parameters[0];
        var thisKeyword = first.Modifiers.First(m => m.IsKind(SyntaxKind.ThisKeyword));
        var withoutThis = first.WithModifiers(first.Modifiers.Remove(thisKeyword));
        if (withoutThis.Modifiers.Count == 0)
            withoutThis = withoutThis.WithLeadingTrivia(first.GetLeadingTrivia());
        var declaringDocument = solution.GetDocument(declaration.SyntaxTree)!;
        EditsFor(edits, declaringDocument.Id)[first] = _ => withoutThis;

        await ApplyEditsAsync(solution, edits, cancellationToken);
        return $"Successfully converted {method.Name} to a static method";
    }

    /// <summary>
    /// A static call can take the extension form when its first argument
    /// converts to the parameter by identity, reference or boxing, is not a
    /// null or default literal, and the call's file already sees the class's
    /// namespace.
    /// </summary>
    private static bool CanBeReceiver(
        Microsoft.CodeAnalysis.Operations.IInvocationOperation operation,
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol type)
    {
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count == 0 || arguments[0].NameColon is not null || arguments[0].RefKindKeyword.RawKind != 0)
            return false;

        var first = arguments[0].Expression;
        if (first is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression or (int)SyntaxKind.DefaultLiteralExpression })
            return false;

        var argumentType = model.GetTypeInfo(first).Type;
        var parameterType = operation.TargetMethod.Parameters[0].Type;
        if (argumentType is null)
            return false;

        var conversion = model.Compilation.ClassifyConversion(argumentType, parameterType);
        if (!(conversion.IsIdentity || conversion.IsReference || conversion.IsBoxing))
            return false;

        return NamespaceIsVisible(model, invocation.SpanStart, type.ContainingNamespace);
    }

    private static bool NamespaceIsVisible(SemanticModel model, int position, INamespaceSymbol ns)
    {
        if (ns.IsGlobalNamespace)
            return true;

        for (var symbol = model.GetEnclosingSymbol(position); symbol is not null; symbol = symbol.ContainingSymbol)
        {
            if (symbol is INamespaceSymbol enclosing && SymbolEqualityComparer.Default.Equals(enclosing, ns))
                return true;
        }

        var name = ns.ToDisplayString();
        var root = model.SyntaxTree.GetRoot();
        var token = root.FindToken(position);
        var usings = token.Parent!.AncestorsAndSelf()
            .SelectMany(node => node switch
            {
                CompilationUnitSyntax unit => unit.Usings,
                BaseNamespaceDeclarationSyntax block => block.Usings,
                _ => Enumerable.Empty<UsingDirectiveSyntax>(),
            });
        return usings.Any(u => u.Alias is null && u.StaticKeyword.RawKind == 0 && u.Name?.ToString() == name);
    }

    /// <summary>The arguments with <paramref name="first"/> in front, separated as written.</summary>
    private static SeparatedSyntaxList<ArgumentSyntax> WithFirst(SeparatedSyntaxList<ArgumentSyntax> arguments, ArgumentSyntax first)
    {
        var separators = arguments.GetSeparators().ToList();
        if (arguments.Count > 0)
            separators.Insert(0, SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space));
        return SyntaxFactory.SeparatedList(new[] { first }.Concat(arguments), separators);
    }

    /// <summary>A receiver for member access: parenthesized unless it already binds tighter.</summary>
    private static ExpressionSyntax Receiver(ExpressionSyntax expression)
    {
        var bare = expression.WithoutTrivia();
        return bare is IdentifierNameSyntax or GenericNameSyntax or MemberAccessExpressionSyntax or InvocationExpressionSyntax
            or ElementAccessExpressionSyntax or ThisExpressionSyntax or BaseExpressionSyntax or ParenthesizedExpressionSyntax
            or LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression }
            or ObjectCreationExpressionSyntax
            ? bare
            : SyntaxFactory.ParenthesizedExpression(bare);
    }

    private static SimpleNameSyntax MethodName(ExpressionSyntax expression) => expression switch
    {
        MemberAccessExpressionSyntax access => access.Name,
        SimpleNameSyntax name => name,
        _ => throw new McpException($"Error: Unexpected call form '{expression}'"),
    };

    private static async Task<MethodDeclarationSyntax> DeclarationAsync(IMethodSymbol method, CancellationToken cancellationToken) =>
        (MethodDeclarationSyntax)await method.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken);

    private static async Task<IEnumerable<ReferenceLocation>> ReferencesAsync(Solution solution, IMethodSymbol method, CancellationToken cancellationToken)
    {
        var references = await SymbolFinder.FindReferencesAsync(method, solution, cancellationToken);
        return references.SelectMany(r => r.Locations).Where(l => l.Location.IsInSource && !l.IsImplicit);
    }

    /// <summary>Every call of <paramref name="method"/>, with the document and model it is in.</summary>
    private static async Task<List<(Document Document, InvocationExpressionSyntax Invocation, SemanticModel Model)>> InvocationsAsync(
        Solution solution,
        IMethodSymbol method,
        CancellationToken cancellationToken)
    {
        var calls = new List<(Document, InvocationExpressionSyntax, SemanticModel)>();
        foreach (var reference in await ReferencesAsync(solution, method, cancellationToken))
        {
            var root = await reference.Document.GetSyntaxRootAsync(cancellationToken);
            var model = await reference.Document.GetSemanticModelAsync(cancellationToken);
            var name = root!.FindNode(reference.Location.SourceSpan, getInnermostNodeForTie: true);
            var callee = name.Parent is MemberAccessExpressionSyntax access && access.Name == name ? access : name;
            if (callee.Parent is InvocationExpressionSyntax invocation && invocation.Expression == callee)
                calls.Add((reference.Document, invocation, model!));
        }

        return calls;
    }

    private static Dictionary<SyntaxNode, Func<SyntaxNode, SyntaxNode>> EditsFor(Dictionary<DocumentId, Dictionary<SyntaxNode, Func<SyntaxNode, SyntaxNode>>> edits, DocumentId id) =>
        edits.TryGetValue(id, out var existing) ? existing : edits[id] = new Dictionary<SyntaxNode, Func<SyntaxNode, SyntaxNode>>();

    /// <summary>
    /// Replaces nodes document by document. Each edit is applied to the node
    /// with its nested edits already made, so chained calls convert together.
    /// </summary>
    private static async Task ApplyEditsAsync(
        Solution solution,
        Dictionary<DocumentId, Dictionary<SyntaxNode, Func<SyntaxNode, SyntaxNode>>> edits,
        CancellationToken cancellationToken)
    {
        var updated = solution;
        foreach (var (id, replacements) in edits)
        {
            var root = await solution.GetDocument(id)!.GetSyntaxRootAsync(cancellationToken);
            root = root!.ReplaceNodes(replacements.Keys, (original, rewritten) => replacements[original](rewritten));
            updated = updated.WithDocumentSyntaxRoot(id, root);
        }

        updated = await MovingSupport.TidyChangedDocumentsAsync(solution, updated, cancellationToken);
        await MovingSupport.ApplyAsync(solution, updated, cancellationToken);
    }
}
