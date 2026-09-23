using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

namespace RefactorMCP.ConsoleApp.Tools.Moving;

/// <summary>
/// One use of a member found by the symbol finder: the name as written, what
/// it is accessed through, and the call it makes, if any.
/// </summary>
internal sealed record MemberReference(
    Document Document,
    SemanticModel Model,
    SimpleNameSyntax Name,
    ExpressionSyntax? Receiver,
    InvocationExpressionSyntax? Invocation,
    bool IsConditional)
{
    /// <summary>The expression naming the member: <c>x.M</c> or <c>M</c>.</summary>
    public ExpressionSyntax Callee => Receiver is null ? Name : (ExpressionSyntax)Name.Parent!;

    /// <summary>Whether the reference is accessed without a receiver, or through <c>this</c> or <c>base</c>.</summary>
    public bool IsOnThis => Receiver is null or ThisExpressionSyntax or BaseExpressionSyntax;
}

internal static class MemberReferences
{
    public static async Task<List<MemberReference>> FindAsync(Solution solution, ISymbol symbol, CancellationToken cancellationToken)
    {
        var found = new List<MemberReference>();
        var references = await SymbolFinder.FindReferencesAsync(symbol, solution, cancellationToken);
        foreach (var location in references.SelectMany(r => r.Locations))
        {
            if (location.IsImplicit || !location.Location.IsInSource)
                continue;

            var root = await location.Document.GetSyntaxRootAsync(cancellationToken);
            var model = await location.Document.GetSemanticModelAsync(cancellationToken);
            if (root!.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true) is not SimpleNameSyntax name)
                continue;

            ExpressionSyntax? receiver = null;
            var conditional = false;
            ExpressionSyntax callee = name;
            switch (name.Parent)
            {
                case MemberAccessExpressionSyntax access when access.Name == name:
                    receiver = access.Expression;
                    callee = access;
                    break;
                case MemberBindingExpressionSyntax binding:
                    conditional = true;
                    callee = binding;
                    break;
            }

            var invocation = callee.Parent is InvocationExpressionSyntax call && call.Expression == callee ? call : null;
            found.Add(new MemberReference(location.Document, model!, name, receiver, invocation, conditional));
        }

        return found;
    }

    /// <summary>
    /// Whether <paramref name="name"/> reaches an instance member of
    /// <paramref name="type"/>, or of a base type, through an implicit <c>this</c>.
    /// </summary>
    public static bool IsImplicitInstanceMember(SimpleNameSyntax name, SemanticModel model, INamedTypeSymbol type, out ISymbol member)
    {
        member = null!;
        if (name.Parent is MemberAccessExpressionSyntax access && access.Name == name
            || name.Parent is QualifiedNameSyntax qualified && qualified.Right == name
            || name.Parent is MemberBindingExpressionSyntax
            || name.Parent is NameColonSyntax or NameEqualsSyntax
            || IsInitializerTarget(name))
        {
            return false;
        }

        var info = model.GetSymbolInfo(name);
        var symbol = info.Symbol ?? (info.CandidateSymbols.Length == 1 ? info.CandidateSymbols[0] : null);
        if (symbol is not (IFieldSymbol or IPropertySymbol or IEventSymbol or IMethodSymbol { MethodKind: MethodKind.Ordinary })
            || symbol.IsStatic
            || !InheritsFrom(type, symbol.ContainingType))
        {
            return false;
        }

        member = symbol;
        return true;
    }

    /// <summary>Whether <paramref name="type"/> is <paramref name="candidate"/> or derives from it.</summary>
    public static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol candidate)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, candidate.OriginalDefinition))
                return true;
        }

        return false;
    }

    /// <summary>Whether an expression is assigned, incremented or passed by reference.</summary>
    public static bool IsWrittenTo(ExpressionSyntax expression)
    {
        var node = expression.Parent is MemberAccessExpressionSyntax access && access.Name == expression ? access : (SyntaxNode)expression;
        return node.Parent switch
        {
            AssignmentExpressionSyntax assignment => assignment.Left == node,
            PrefixUnaryExpressionSyntax unary => unary.IsKind(SyntaxKind.PreIncrementExpression) || unary.IsKind(SyntaxKind.PreDecrementExpression),
            PostfixUnaryExpressionSyntax => true,
            ArgumentSyntax argument => !argument.RefKindKeyword.IsKind(SyntaxKind.None),
            _ => false,
        };
    }

    /// <summary>An expression that names a value and can be evaluated again without effects.</summary>
    public static bool IsSimple(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax or ThisExpressionSyntax => true,
        MemberAccessExpressionSyntax access => access.Name is IdentifierNameSyntax && IsSimple(access.Expression),
        ParenthesizedExpressionSyntax parenthesized => IsSimple(parenthesized.Expression),
        _ => false,
    };

    private static bool IsInitializerTarget(SimpleNameSyntax name) =>
        name.Parent is AssignmentExpressionSyntax assignment
        && assignment.Left == name
        && assignment.Parent is InitializerExpressionSyntax initializer
        && initializer.IsKind(SyntaxKind.ObjectInitializerExpression);
}
