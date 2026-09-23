using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using RefactorMCP.ConsoleApp.Tools.Moving;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace RefactorMCP.ConsoleApp.Tools.Composites;

[McpServerToolType]
public static class HideDelegateTool
{
    [McpServerTool, Description("Hide Delegate: give a class a member that forwards to a member of the object one of its fields or properties holds, " +
        "and repoint every client that reached through the field (person.Department.Manager becomes person.Manager).")]
    public static async Task<string> HideDelegate(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file declaring the field or property that holds the delegate")] string filePath,
        [Description("Name of the field or property holding the delegate, such as Department")] string delegateName,
        [Description("Name of the delegate's field, property or method that clients use, such as Manager")] string memberName,
        [Description("Name of the forwarding member (optional, defaults to memberName)")] string? newMemberName = null,
        [Description("Line of the delegate's declaration (1-based, optional)")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var document = MovingSupport.DocumentOrThrow(solution, filePath);
        var holder = await MovingSupport.FindDeclaredSymbolAsync(
            document, delegateName, line, s => s is IFieldSymbol or IPropertySymbol { IsIndexer: false }, "field or property", cancellationToken);
        if (holder.IsStatic)
            throw new McpException($"Error: {delegateName} is static, so there is no instance for clients to reach it through");

        var server = holder.ContainingType;
        var delegateType = holder is IFieldSymbol field ? field.Type : ((IPropertySymbol)holder).Type;
        var members = InstanceMembers(delegateType, memberName);
        if (members.Count == 0)
            throw new McpException($"Error: {delegateType.ToDisplayString()} has no instance member named '{memberName}' to hide");

        var name = newMemberName ?? memberName;
        for (var type = server; type is not null; type = type.BaseType)
        {
            if (type.GetMembers(name).Any(m => !m.IsImplicitlyDeclared))
                throw new McpException($"Error: {type.Name} already has a member named '{name}'");
        }

        var forwarding = members.Select(m => Forwarding(m, holder, name)).ToList();
        var edits = new SyntaxEdits();
        var holderNode = await holder.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken);
        var holderMember = holderNode as MemberDeclarationSyntax ?? holderNode.FirstAncestorOrSelf<MemberDeclarationSyntax>()!;
        var serverDeclaration = (TypeDeclarationSyntax)holderMember.Parent!;
        var index = serverDeclaration.Members.IndexOf(holderMember);
        edits.Replace(solution, serverDeclaration, current =>
        {
            var type = (TypeDeclarationSyntax)current;
            return type.WithMembers(type.Members.InsertRange(index + 1, forwarding));
        });

        var repointed = 0;
        foreach (var reference in await MemberReferences.FindAsync(solution, holder, cancellationToken))
        {
            if (reference.Receiver is null || reference.IsConditional
                || reference.Callee.Parent is not MemberAccessExpressionSyntax outer || outer.Expression != reference.Callee
                || MemberReferences.IsWrittenTo(outer.Name))
                continue;

            var used = reference.Model.GetSymbolInfo(outer.Name, cancellationToken).Symbol;
            if (used is null || !members.Contains(used.OriginalDefinition, SymbolEqualityComparer.Default))
                continue;
            if (used is IMethodSymbol && outer.Parent is not InvocationExpressionSyntax)
                continue;

            repointed++;
            edits.Replace(reference.Document.Id, outer, current =>
            {
                var access = (MemberAccessExpressionSyntax)current;
                var receiver = ((MemberAccessExpressionSyntax)access.Expression).Expression;
                return access.WithExpression(receiver).WithName(access.Name.WithIdentifier(Identifier(name).WithTriviaFrom(access.Name.Identifier)));
            });
        }

        var updated = await edits.ApplyAsync(solution, cancellationToken);
        await SolutionEdits.EnsureCompilesAsync(solution, updated, cancellationToken);
        await MovingSupport.ApplyAsync(solution, updated, cancellationToken);
        return $"Successfully added {server.Name}.{name}, forwarding to {delegateName}.{memberName}, and repointed {repointed} use(s)";
    }

    /// <summary>
    /// The instance fields, properties and methods of that name the delegate's
    /// type declares, or else the nearest base type or, for an interface, base
    /// interface that does.
    /// </summary>
    private static List<ISymbol> InstanceMembers(ITypeSymbol type, string name)
    {
        var searched = type.TypeKind == TypeKind.Interface
            ? type.AllInterfaces.Prepend(type)
            : BaseTypes(type);
        foreach (var current in searched)
        {
            var found = current.GetMembers(name)
                .Where(m => !m.IsStatic && m is IFieldSymbol or IPropertySymbol { IsIndexer: false } or IMethodSymbol { MethodKind: MethodKind.Ordinary })
                .Select(m => m.OriginalDefinition)
                .ToList();
            if (found.Count > 0)
                return found;
        }

        return new List<ISymbol>();
    }

    private static IEnumerable<ITypeSymbol> BaseTypes(ITypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
            yield return current;
    }

    /// <summary>
    /// A member of the server that forwards to the delegate's member, as
    /// accessible as the field or property holding the delegate, since
    /// clients reached the delegate through it.
    /// </summary>
    private static MemberDeclarationSyntax Forwarding(ISymbol member, ISymbol holder, string name)
    {
        var modifiers = AccessibilityModifiers(holder.DeclaredAccessibility);
        var target = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName(holder.Name), IdentifierName(member.Name));
        MemberDeclarationSyntax declaration = member switch
        {
            IMethodSymbol method => ForwardingMethod(method, name, modifiers, target),
            IPropertySymbol property => PropertyDeclaration(MovingSupport.QualifiedType(property.Type), name)
                .WithModifiers(modifiers)
                .WithExpressionBody(ArrowExpressionClause(target))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
            IFieldSymbol field => PropertyDeclaration(MovingSupport.QualifiedType(field.Type), name)
                .WithModifiers(modifiers)
                .WithExpressionBody(ArrowExpressionClause(target))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
            _ => throw new McpException($"Error: {member.Name} is not a field, property or method"),
        };

        return declaration
            .WithLeadingTrivia(EndOfLine("\n"))
            .WithAdditionalAnnotations(Formatter.Annotation);
    }

    private static MethodDeclarationSyntax ForwardingMethod(IMethodSymbol method, string name, SyntaxTokenList modifiers, ExpressionSyntax target)
    {
        if (method.TypeParameters.Length > 0 || method.Parameters.Any(p => p.RefKind != RefKind.None || p.IsParams || p.HasExplicitDefaultValue))
            throw new McpException($"Error: {method.Name} has type parameters, or ref, out, params or optional parameters, which a forwarding method does not reproduce");

        var parameters = method.Parameters.Select(p => Parameter(Identifier(p.Name)).WithType(MovingSupport.QualifiedType(p.Type)));
        var arguments = method.Parameters.Select(p => Argument(IdentifierName(p.Name)));
        return MethodDeclaration(method.ReturnsVoid ? PredefinedType(Token(SyntaxKind.VoidKeyword)) : MovingSupport.QualifiedType(method.ReturnType), name)
            .WithModifiers(modifiers)
            .WithParameterList(ParameterList(SeparatedList(parameters)))
            .WithExpressionBody(ArrowExpressionClause(InvocationExpression(target, ArgumentList(SeparatedList(arguments)))))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
    }

    private static SyntaxTokenList AccessibilityModifiers(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Public => TokenList(Token(SyntaxKind.PublicKeyword)),
        Accessibility.Internal => TokenList(Token(SyntaxKind.InternalKeyword)),
        Accessibility.Protected => TokenList(Token(SyntaxKind.ProtectedKeyword)),
        Accessibility.ProtectedOrInternal => TokenList(Token(SyntaxKind.ProtectedKeyword), Token(SyntaxKind.InternalKeyword)),
        Accessibility.ProtectedAndInternal => TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.ProtectedKeyword)),
        _ => TokenList(Token(SyntaxKind.PrivateKeyword)),
    };
}
