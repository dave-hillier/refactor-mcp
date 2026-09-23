using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using RefactorMCP.ConsoleApp.Tools.Moving;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace RefactorMCP.ConsoleApp.Tools.Forwarding;

/// <summary>
/// Members that do nothing but forward to the member of the same name of
/// something else, a field or the base class: writing one, and recognising one.
/// </summary>
internal static class ForwardingMembers
{
    /// <summary>
    /// A member forwarding to <paramref name="member"/> through
    /// <paramref name="receiver"/>, as accessible as the member itself.
    /// Properties and fields are forwarded by properties, settable when
    /// <paramref name="settable"/>; methods by expression-bodied methods.
    /// </summary>
    public static MemberDeclarationSyntax Create(ISymbol member, ExpressionSyntax receiver, bool settable)
    {
        var modifiers = Modifiers(member.DeclaredAccessibility);
        MemberDeclarationSyntax declaration;
        switch (member)
        {
            case IPropertySymbol { IsIndexer: true } indexer:
            {
                var parameters = indexer.Parameters.Select(p => Parameter(Identifier(p.Name)).WithType(MovingSupport.QualifiedType(p.Type)));
                var target = ElementAccessExpression(receiver, BracketedArgumentList(SeparatedList(indexer.Parameters.Select(p => Argument(IdentifierName(p.Name))))));
                declaration = IndexerDeclaration(MovingSupport.QualifiedType(indexer.Type))
                    .WithModifiers(modifiers)
                    .WithParameterList(BracketedParameterList(SeparatedList(parameters)))
                    .WithAccessorList(Accessors(target, settable));
                break;
            }

            case IPropertySymbol or IFieldSymbol:
            {
                var type = member is IPropertySymbol property ? property.Type : ((IFieldSymbol)member).Type;
                var target = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, receiver, IdentifierName(member.Name));
                var named = PropertyDeclaration(MovingSupport.QualifiedType(type), member.Name).WithModifiers(modifiers);
                declaration = settable
                    ? named.WithAccessorList(Accessors(target, settable: true))
                    : named.WithExpressionBody(ArrowExpressionClause(target)).WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
                break;
            }

            case IMethodSymbol method:
            {
                var parameters = method.Parameters.Select(p => Parameter(Identifier(p.Name)).WithType(MovingSupport.QualifiedType(p.Type)));
                var call = InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, receiver, IdentifierName(method.Name)),
                    ArgumentList(SeparatedList(method.Parameters.Select(p => Argument(IdentifierName(p.Name))))));
                declaration = MethodDeclaration(method.ReturnsVoid ? PredefinedType(Token(SyntaxKind.VoidKeyword)) : MovingSupport.QualifiedType(method.ReturnType), method.Name)
                    .WithModifiers(modifiers)
                    .WithParameterList(ParameterList(SeparatedList(parameters)))
                    .WithExpressionBody(ArrowExpressionClause(call))
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
                break;
            }

            default:
                throw new ArgumentException($"{member.Name} cannot be forwarded", nameof(member));
        }

        return declaration.WithTrailingTrivia(EndOfLine("\n")).WithAdditionalAnnotations(Formatter.Annotation);
    }

    /// <summary>
    /// The member <paramref name="declaration"/> forwards to, when all it does
    /// is reach the member of the same name through a receiver
    /// <paramref name="isReceiver"/> accepts: a method whose body is
    /// <c>r.M(a, b)</c> passing its own parameters in order, a property whose
    /// getter reads <c>r.P</c> and whose setter, if any, assigns
    /// <c>r.P = value</c>, or an indexer that does the same with <c>r[i]</c>.
    /// </summary>
    public static ISymbol? Forwarded(MemberDeclarationSyntax declaration, SemanticModel model, Func<ExpressionSyntax, bool> isReceiver)
    {
        if (model.GetDeclaredSymbol(declaration) is not { IsStatic: false } symbol)
            return null;

        return declaration switch
        {
            MethodDeclarationSyntax method => ForwardedCall(Body(method.Body, method.ExpressionBody), model, (IMethodSymbol)symbol, isReceiver),
            PropertyDeclarationSyntax property when property.Initializer is null =>
                ForwardedAccessors(property.ExpressionBody, property.AccessorList, model,
                    e => e is MemberAccessExpressionSyntax access && access.Name.Identifier.ValueText == symbol.Name && isReceiver(access.Expression)),
            IndexerDeclarationSyntax indexer =>
                ForwardedAccessors(indexer.ExpressionBody, indexer.AccessorList, model,
                    e => e is ElementAccessExpressionSyntax access && isReceiver(access.Expression)
                        && PassesParameters(access.ArgumentList.Arguments, ((IPropertySymbol)symbol).Parameters, model)),
            _ => null,
        };
    }

    private static ISymbol? ForwardedCall(ExpressionSyntax? body, SemanticModel model, IMethodSymbol method, Func<ExpressionSyntax, bool> isReceiver)
    {
        if (method.TypeParameters.Length > 0
            || body is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name: IdentifierNameSyntax name } access } call
            || name.Identifier.ValueText != method.Name
            || !isReceiver(access.Expression)
            || !PassesParameters(call.ArgumentList.Arguments, method.Parameters, model))
        {
            return null;
        }

        return model.GetSymbolInfo(call).Symbol;
    }

    private static ISymbol? ForwardedAccessors(
        ArrowExpressionClauseSyntax? arrow,
        AccessorListSyntax? accessors,
        SemanticModel model,
        Func<ExpressionSyntax?, bool> isTarget)
    {
        if (arrow is not null)
            return isTarget(arrow.Expression) ? model.GetSymbolInfo(arrow.Expression).Symbol : null;
        if (accessors is null || accessors.Accessors.Count == 0)
            return null;

        ISymbol? forwarded = null;
        foreach (var accessor in accessors.Accessors)
        {
            var body = Body(accessor.Body, accessor.ExpressionBody);
            var target = accessor.Kind() switch
            {
                SyntaxKind.GetAccessorDeclaration => body,
                SyntaxKind.SetAccessorDeclaration when body is AssignmentExpressionSyntax { Right: IdentifierNameSyntax { Identifier.ValueText: "value" } } assignment
                    && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) => assignment.Left,
                _ => null,
            };
            if (target is null || !isTarget(target) || accessor.AttributeLists.Count > 0 || accessor.Modifiers.Count > 0)
                return null;

            var symbol = model.GetSymbolInfo(target).Symbol;
            if (symbol is null || forwarded is not null && !SymbolEqualityComparer.Default.Equals(forwarded, symbol))
                return null;
            forwarded = symbol;
        }

        return forwarded;
    }

    /// <summary>
    /// Whether the arguments are the parameters, in order. Inside an indexer's
    /// accessor a parameter binds to the accessor's copy of it, so parameters
    /// are matched by position and name.
    /// </summary>
    private static bool PassesParameters(SeparatedSyntaxList<ArgumentSyntax> arguments, IReadOnlyList<IParameterSymbol> parameters, SemanticModel model) =>
        arguments.Count == parameters.Count
        && arguments.Select((a, i) => a.NameColon is null
            && RefKindOf(a) == parameters[i].RefKind
            && model.GetSymbolInfo(a.Expression).Symbol is IParameterSymbol parameter
            && parameter.Ordinal == i
            && parameter.Name == parameters[i].Name
            && parameter.ContainingSymbol is IMethodSymbol { MethodKind: not MethodKind.AnonymousFunction and not MethodKind.LocalFunction }).All(same => same);

    private static RefKind RefKindOf(ArgumentSyntax argument) => argument.RefKindKeyword.Kind() switch
    {
        SyntaxKind.RefKeyword => RefKind.Ref,
        SyntaxKind.OutKeyword => RefKind.Out,
        SyntaxKind.InKeyword => RefKind.In,
        _ => RefKind.None,
    };

    private static ExpressionSyntax? Body(BlockSyntax? block, ArrowExpressionClauseSyntax? arrow) => (block, arrow) switch
    {
        (null, { } expression) => expression.Expression,
        ({ Statements: [ReturnStatementSyntax { Expression: { } value }] }, _) => value,
        ({ Statements: [ExpressionStatementSyntax { Expression: var value }] }, _) => value,
        _ => null,
    };

    /// <summary>
    /// Whether a caller of one member would see no difference calling the
    /// other: the same kind, type, and parameters with the same names, types,
    /// modifiers and default values.
    /// </summary>
    public static bool SameSignature(ISymbol first, ISymbol second) => (first, second) switch
    {
        (IMethodSymbol a, IMethodSymbol b) => a.Name == b.Name
            && a.RefKind == b.RefKind
            && SymbolEqualityComparer.Default.Equals(a.ReturnType, b.ReturnType)
            && SameParameters(a.Parameters, b.Parameters),
        (IPropertySymbol a, IPropertySymbol b) => a.Name == b.Name
            && a.IsIndexer == b.IsIndexer
            && SymbolEqualityComparer.Default.Equals(a.Type, b.Type)
            && SameParameters(a.Parameters, b.Parameters),
        _ => false,
    };

    private static bool SameParameters(IReadOnlyList<IParameterSymbol> first, IReadOnlyList<IParameterSymbol> second) =>
        first.Count == second.Count
        && first.Zip(second).All(pair => pair.First.Name == pair.Second.Name
            && pair.First.RefKind == pair.Second.RefKind
            && pair.First.IsParams == pair.Second.IsParams
            && SymbolEqualityComparer.Default.Equals(pair.First.Type, pair.Second.Type)
            && pair.First.HasExplicitDefaultValue == pair.Second.HasExplicitDefaultValue
            && (!pair.First.HasExplicitDefaultValue || Equals(pair.First.ExplicitDefaultValue, pair.Second.ExplicitDefaultValue)));

    /// <summary>
    /// The expression naming a member at a reference the symbol finder
    /// reports: <c>M</c>, <c>x.M</c>, <c>x?.M</c>'s binding, or for an
    /// indexer the element access.
    /// </summary>
    public static ExpressionSyntax? Callee(SyntaxNode found, ISymbol member)
    {
        if (member is IPropertySymbol { IsIndexer: true })
            return found.AncestorsAndSelf().OfType<ElementAccessExpressionSyntax>().FirstOrDefault();

        return found switch
        {
            SimpleNameSyntax { Parent: MemberAccessExpressionSyntax access } name when access.Name == name => access,
            SimpleNameSyntax { Parent: MemberBindingExpressionSyntax binding } => binding,
            SimpleNameSyntax name => name,
            _ => null,
        };
    }

    /// <summary>What a callee is reached through, or null for an implicit <c>this</c>.</summary>
    public static ExpressionSyntax? Receiver(ExpressionSyntax callee) => callee switch
    {
        MemberAccessExpressionSyntax access => access.Expression,
        ElementAccessExpressionSyntax element => element.Expression,
        MemberBindingExpressionSyntax binding => binding.FirstAncestorOrSelf<ConditionalAccessExpressionSyntax>()!.Expression,
        _ => null,
    };

    /// <summary>The same member access or element access, reached through <paramref name="receiver"/> instead.</summary>
    public static ExpressionSyntax Through(ExpressionSyntax callee, ExpressionSyntax receiver) => callee switch
    {
        MemberAccessExpressionSyntax access => access.WithExpression(receiver.WithTriviaFrom(access.Expression)),
        ElementAccessExpressionSyntax element => element.WithExpression(receiver.WithTriviaFrom(element.Expression)),
        SimpleNameSyntax name => MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, receiver, name.WithoutTrivia()).WithTriviaFrom(name),
        _ => throw new ArgumentException("Not a member access", nameof(callee)),
    };

    /// <summary>
    /// <c>x.M</c> or <c>x[i]</c> reached through the instance itself: the bare
    /// member where that means the same member, else <c>this.M</c> or
    /// <c>this[i]</c>, else null, which leaves <c>base</c> as the only way.
    /// Whether virtual dispatch then reaches the same code is for the caller.
    /// </summary>
    public static ExpressionSyntax? OnInstance(ExpressionSyntax callee, SemanticModel model)
    {
        var bound = callee.Parent is InvocationExpressionSyntax call && call.Expression == callee ? (ExpressionSyntax)call : callee;
        var meaning = model.GetSymbolInfo(bound).Symbol;
        if (meaning is null)
            return null;

        IEnumerable<ExpressionSyntax> candidates = callee switch
        {
            MemberAccessExpressionSyntax access => new ExpressionSyntax[]
            {
                access.Name.WithoutTrivia(),
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, ThisExpression(), access.Name.WithoutTrivia()),
            },
            _ => new[] { Through(callee, ThisExpression()) },
        };

        foreach (var candidate in candidates)
        {
            var speculative = bound == callee ? candidate : ((InvocationExpressionSyntax)bound).WithExpression(candidate);
            var symbol = model.GetSpeculativeSymbolInfo(bound.SpanStart, speculative, SpeculativeBindingOption.BindAsExpression).Symbol;
            if (SymbolEqualityComparer.Default.Equals(symbol, meaning))
                return candidate;
        }

        return null;
    }

    public static SyntaxTokenList Modifiers(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Public => TokenList(Token(SyntaxKind.PublicKeyword)),
        Accessibility.Internal => TokenList(Token(SyntaxKind.InternalKeyword)),
        Accessibility.Protected => TokenList(Token(SyntaxKind.ProtectedKeyword)),
        Accessibility.ProtectedOrInternal => TokenList(Token(SyntaxKind.ProtectedKeyword), Token(SyntaxKind.InternalKeyword)),
        Accessibility.ProtectedAndInternal => TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.ProtectedKeyword)),
        _ => TokenList(Token(SyntaxKind.PrivateKeyword)),
    };

    private static AccessorListSyntax Accessors(ExpressionSyntax target, bool settable)
    {
        var accessors = new List<AccessorDeclarationSyntax>
        {
            AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                .WithExpressionBody(ArrowExpressionClause(target))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
        };
        if (settable)
        {
            accessors.Add(AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                .WithExpressionBody(ArrowExpressionClause(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, target, IdentifierName("value"))))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
        }

        return AccessorList(List(accessors));
    }

    /// <summary>
    /// Inserts a member with one blank line between it and each neighbour,
    /// whatever blank lines the neighbours carried at that edge before.
    /// </summary>
    public static SyntaxList<MemberDeclarationSyntax> Insert(SyntaxList<MemberDeclarationSyntax> members, int index, MemberDeclarationSyntax member)
    {
        if (index > 0)
        {
            member = WithBlankLineBefore(member);
            members = members.Replace(members[index - 1], WithoutTrailingBlankLines(members[index - 1]));
        }

        members = members.Insert(index, member);
        if (index + 1 < members.Count)
            members = members.Replace(members[index + 1], WithBlankLineBefore(members[index + 1]));
        return members;
    }

    /// <summary>A member's trailing trivia up to the end of its last line, without the blank lines after it.</summary>
    private static MemberDeclarationSyntax WithoutTrailingBlankLines(MemberDeclarationSyntax member)
    {
        var trivia = member.GetTrailingTrivia();
        var end = trivia.IndexOf(SyntaxKind.EndOfLineTrivia);
        return end < 0 ? member : member.WithTrailingTrivia(trivia.Take(end + 1));
    }

    public static TMember WithBlankLineBefore<TMember>(TMember member) where TMember : MemberDeclarationSyntax =>
        member.WithLeadingTrivia(MemberLayout.WithoutLeadingBlankLines(member.GetLeadingTrivia()).Insert(0, EndOfLine("\n")));
}
