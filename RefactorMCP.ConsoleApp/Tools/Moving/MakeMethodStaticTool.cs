using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace RefactorMCP.ConsoleApp.Tools.Moving;

[McpServerToolType]
public static class MakeMethodStaticTool
{
    [McpServerTool, Description("Make an instance method static and update every call across the solution. " +
        "With pass 'instance' the method takes the instance as its first parameter; with pass 'parameters' " +
        "each instance field or property it reads becomes a parameter instead.")]
    public static async Task<string> MakeMethodStatic(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file declaring the method")] string filePath,
        [Description("Name of the method")] string methodName,
        [Description("What the static method receives: 'instance' (default) or 'parameters'")] string pass = "instance",
        [Description("Name for the instance parameter (optional, defaults to the type name in camel case)")] string? parameterName = null,
        [Description("Line of the method's declaration (1-based, optional), to choose between overloads")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        if (pass is not ("instance" or "parameters"))
            throw new McpException($"Error: pass must be 'instance' or 'parameters', not '{pass}'");

        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var document = MovingSupport.DocumentOrThrow(solution, filePath);
        var method = (IMethodSymbol)await MovingSupport.FindDeclaredSymbolAsync(
            document, methodName, line, s => s is IMethodSymbol { MethodKind: MethodKind.Ordinary }, "method", cancellationToken);

        var updated = await StaticConversion.MakeStaticAsync(solution, method, pass == "parameters", parameterName, cancellationToken);
        await MovingSupport.ApplyAsync(solution, updated, cancellationToken);
        return $"Successfully made {methodName} static";
    }
}

/// <summary>
/// Makes an instance method static, passing either the instance or the
/// members it reads, and rewrites every call to pass them.
/// </summary>
internal static class StaticConversion
{
    public static async Task<Solution> MakeStaticAsync(
        Solution solution,
        IMethodSymbol method,
        bool passMembers,
        string? parameterName,
        CancellationToken cancellationToken)
    {
        var type = method.ContainingType;
        if (method.IsStatic)
            throw new McpException($"Error: {method.Name} is already static");
        if (method.IsVirtual || method.IsOverride || method.IsAbstract || method.ExplicitInterfaceImplementations.Length > 0 || ImplementsInterface(method))
            throw new McpException($"Error: {method.Name} is virtual, abstract, an override or an interface implementation; callers rely on dispatch through the instance");
        if (type.TypeKind != TypeKind.Class)
            throw new McpException($"Error: {type.Name} is not a class; only methods of classes can be made static");

        var declaration = (MethodDeclarationSyntax)await method.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken);
        var document = solution.GetDocument(declaration.SyntaxTree)!;
        var model = (await document.GetSemanticModelAsync(cancellationToken))!;
        var body = (SyntaxNode?)declaration.Body ?? declaration.ExpressionBody;
        if (body is null)
            throw new McpException($"Error: {method.Name} has no body");
        if (body.DescendantNodes().OfType<BaseExpressionSyntax>().Any())
            throw new McpException($"Error: {method.Name} calls through base, which a static method cannot do");

        var edits = new SyntaxEdits();
        var used = passMembers
            ? RewriteMembersAsParameters(body, model, type, method, document.Id, edits)
            : RewriteThroughInstance(body, model, type, method, document.Id, edits, InstanceName(parameterName, type, declaration));

        var newParameters = used.Select(u => SyntaxFactory.Parameter(SyntaxFactory.Identifier(u.Name)).WithType(u.Type)).ToList();
        edits.Replace(document.Id, declaration, rewritten =>
        {
            var updated = WithStatic((MethodDeclarationSyntax)rewritten);
            return updated.WithParameterList(updated.ParameterList.WithParameters(
                SyntaxEdits.Prepend(updated.ParameterList.Parameters, newParameters)));
        });

        foreach (var reference in await MemberReferences.FindAsync(solution, method, cancellationToken))
            RewriteCall(reference, method, declaration, used, passMembers, edits);

        return await edits.ApplyAsync(solution, cancellationToken);
    }

    /// <summary>A parameter the static method gains, and the member whose value it carries, if any.</summary>
    private sealed record AddedParameter(string Name, TypeSyntax Type, ISymbol? Member);

    private static List<AddedParameter> RewriteThroughInstance(
        SyntaxNode body,
        SemanticModel model,
        INamedTypeSymbol type,
        IMethodSymbol method,
        DocumentId document,
        SyntaxEdits edits,
        string instance)
    {
        var usesInstance = false;
        foreach (var node in body.DescendantNodes())
        {
            if (node is ThisExpressionSyntax)
            {
                usesInstance = true;
                edits.Replace(document, node, rewritten => SyntaxFactory.IdentifierName(instance).WithTriviaFrom(rewritten));
            }
            else if (node is SimpleNameSyntax name
                && MemberReferences.IsImplicitInstanceMember(name, model, type, out var member)
                && !SymbolEqualityComparer.Default.Equals(member, method))
            {
                usesInstance = true;
                edits.Replace(document, node, rewritten => SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(instance),
                        ((SimpleNameSyntax)rewritten).WithoutTrivia())
                    .WithTriviaFrom(rewritten));
            }
        }

        return usesInstance
            ? new List<AddedParameter> { new(instance, MovingSupport.QualifiedType(type), null) }
            : new List<AddedParameter>();
    }

    private static List<AddedParameter> RewriteMembersAsParameters(
        SyntaxNode body,
        SemanticModel model,
        INamedTypeSymbol type,
        IMethodSymbol method,
        DocumentId document,
        SyntaxEdits edits)
    {
        var parameters = new List<AddedParameter>();
        var taken = method.Parameters.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var node in body.DescendantNodes())
        {
            ISymbol? member = null;
            ExpressionSyntax? use = null;
            if (node is MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } access)
            {
                member = model.GetSymbolInfo(access).Symbol;
                use = access;
            }
            else if (node is ThisExpressionSyntax { Parent: not MemberAccessExpressionSyntax })
            {
                throw new McpException($"Error: {method.Name} uses the instance itself (this); pass the instance instead");
            }
            else if (node is SimpleNameSyntax name && MemberReferences.IsImplicitInstanceMember(name, model, type, out var implicitMember))
            {
                if (SymbolEqualityComparer.Default.Equals(implicitMember, method))
                    throw new McpException($"Error: {method.Name} calls itself, so its members cannot be passed as parameters");
                member = implicitMember;
                use = name;
            }

            if (member is null || use is null)
                continue;
            if (member is not (IFieldSymbol or IPropertySymbol))
                throw new McpException($"Error: {method.Name} uses the instance member {member.Name}, which is not a field or property; pass the instance instead");
            if (MemberReferences.IsWrittenTo(use))
                throw new McpException($"Error: {method.Name} assigns the instance member {member.Name}, which a parameter cannot carry back; pass the instance instead");

            var parameter = parameters.FirstOrDefault(p => SymbolEqualityComparer.Default.Equals(p.Member, member));
            if (parameter is null)
            {
                var parameterName = MovingSupport.CamelCase(member.Name);
                if (!taken.Add(parameterName))
                    throw new McpException($"Error: {method.Name} already has a parameter or local named {parameterName}");
                var memberType = member is IFieldSymbol field ? field.Type : ((IPropertySymbol)member).Type;
                parameter = new AddedParameter(parameterName, MovingSupport.QualifiedType(memberType), member);
                parameters.Add(parameter);
            }

            var replacement = parameter.Name;
            edits.Replace(document, use, rewritten => SyntaxFactory.IdentifierName(replacement).WithTriviaFrom(rewritten));
        }

        return parameters;
    }

    private static void RewriteCall(
        MemberReference reference,
        IMethodSymbol method,
        MethodDeclarationSyntax declaration,
        IReadOnlyList<AddedParameter> added,
        bool passMembers,
        SyntaxEdits edits)
    {
        if (reference.IsConditional)
            throw new McpException($"Error: {method.Name} is called with null-conditional access (?.), which a static call cannot express");

        var insideMethod = reference.Name.SyntaxTree == declaration.SyntaxTree && declaration.Span.Contains(reference.Name.Span);
        if (reference.Invocation is null)
        {
            if (added.Count > 0)
                throw new McpException($"Error: {method.Name} is used as a method group, whose signature would change");
            if (!reference.IsOnThis)
                edits.Replace(reference.Document.Id, reference.Callee, _ => StaticName(reference, method).WithTriviaFrom(reference.Callee));
            return;
        }

        var arguments = new List<ArgumentSyntax>();
        if (!passMembers && added.Count > 0)
        {
            ExpressionSyntax instance = insideMethod
                ? SyntaxFactory.IdentifierName(added[0].Name)
                : reference.IsOnThis ? SyntaxFactory.ThisExpression() : reference.Receiver!.WithoutTrivia();
            arguments.Add(SyntaxFactory.Argument(instance));
        }
        else if (passMembers)
        {
            if (!reference.IsOnThis && !MemberReferences.IsSimple(reference.Receiver!))
                throw new McpException($"Error: A call to {method.Name} is made on '{reference.Receiver}', which would be evaluated once per parameter; store it in a local first");

            foreach (var parameter in added)
            {
                if (!reference.Model.IsAccessible(reference.Invocation.SpanStart, parameter.Member!))
                    throw new McpException($"Error: {parameter.Member!.Name} is not accessible where {method.Name} is called in {System.IO.Path.GetFileName(reference.Document.FilePath)}; pass the instance instead");

                ExpressionSyntax value = reference.IsOnThis
                    ? SyntaxFactory.IdentifierName(parameter.Member.Name)
                    : SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, reference.Receiver!.WithoutTrivia(), SyntaxFactory.IdentifierName(parameter.Member.Name));
                arguments.Add(SyntaxFactory.Argument(value));
            }
        }

        var name = StaticName(reference, method);
        edits.Replace(reference.Document.Id, reference.Invocation, rewritten =>
        {
            var call = (InvocationExpressionSyntax)rewritten;
            return call
                .WithExpression(name.WithTriviaFrom(call.Expression))
                .WithArgumentList(call.ArgumentList.WithArguments(SyntaxEdits.Prepend(call.ArgumentList.Arguments, arguments)));
        });
    }

    /// <summary>
    /// The method qualified by the type it was called on, reduced by the
    /// simplifier to what the calling code needs.
    /// </summary>
    private static ExpressionSyntax StaticName(MemberReference reference, IMethodSymbol method)
    {
        var called = reference.Model.GetSymbolInfo(reference.Callee).Symbol as IMethodSymbol;
        var type = called?.ContainingType ?? method.ContainingType;
        var access = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            MovingSupport.QualifiedType(type),
            reference.Name.WithoutTrivia());
        return reference.Name is IdentifierNameSyntax ? access.WithAdditionalAnnotations(Simplifier.Annotation) : access;
    }

    private static string InstanceName(string? requested, INamedTypeSymbol type, MethodDeclarationSyntax declaration)
    {
        var name = requested ?? MovingSupport.CamelCase(type.Name);
        var taken = declaration.ParameterList.Parameters.Select(p => p.Identifier.ValueText)
            .Concat(declaration.DescendantNodes().OfType<VariableDeclaratorSyntax>().Select(v => v.Identifier.ValueText))
            .Concat(declaration.DescendantNodes().OfType<SingleVariableDesignationSyntax>().Select(v => v.Identifier.ValueText));
        if (taken.Contains(name.TrimStart('@')))
            throw new McpException($"Error: {declaration.Identifier.ValueText} already has a parameter or local named {name}; choose another name for the instance");
        return name;
    }

    private static bool ImplementsInterface(IMethodSymbol method) =>
        method.ContainingType.AllInterfaces
            .SelectMany(i => i.GetMembers().OfType<IMethodSymbol>())
            .Any(m => SymbolEqualityComparer.Default.Equals(method.ContainingType.FindImplementationForInterfaceMember(m), method));

    /// <summary>Adds <c>static</c> after the accessibility modifiers, keeping the declaration's leading trivia in front.</summary>
    internal static MethodDeclarationSyntax WithStatic(MethodDeclarationSyntax method)
    {
        var modifiers = method.Modifiers;
        var index = 0;
        for (var i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].Kind() is SyntaxKind.PublicKeyword or SyntaxKind.PrivateKeyword or SyntaxKind.ProtectedKeyword or SyntaxKind.InternalKeyword)
                index = i + 1;
        }

        var keyword = SyntaxFactory.Token(SyntaxKind.StaticKeyword).WithTrailingTrivia(SyntaxFactory.Space);
        if (index == 0)
        {
            var leading = method.GetLeadingTrivia();
            method = method.WithoutLeadingTrivia();
            return method.WithModifiers(method.Modifiers.Insert(0, keyword.WithLeadingTrivia(leading)));
        }

        return method.WithModifiers(modifiers.Insert(index, keyword));
    }
}
