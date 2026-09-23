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
using RefactorMCP.ConsoleApp.Tools.Moving;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace RefactorMCP.ConsoleApp.Tools.Composites;

[McpServerToolType]
public static class ReplaceDelegationWithInheritanceTool
{
    [McpServerTool, Description("Replace Delegation with Inheritance: make a class derive from the class of a private field it creates and delegates to, " +
        "remove the members that only forward to the field, reach the rest through inheritance, and remove the field. " +
        "Refuses, changing nothing, when the field escapes, is assigned, or the class would hide a member of the new base.")]
    public static async Task<string> ReplaceDelegationWithInheritance(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file declaring the class")] string filePath,
        [Description("Name of the class")] string className,
        [Description("Name of the private field holding the delegate")] string fieldName,
        CancellationToken cancellationToken = default)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var document = MovingSupport.DocumentOrThrow(solution, filePath);
        var type = (INamedTypeSymbol)await MovingSupport.FindDeclaredSymbolAsync(
            document, className, null, s => s is INamedTypeSymbol, "type", cancellationToken);
        var field = type.GetMembers(fieldName).OfType<IFieldSymbol>().FirstOrDefault()
            ?? throw new McpException($"Error: {className} has no field named '{fieldName}'");

        var updated = await new Inheritance(solution, type, field).ReplaceAsync(cancellationToken);
        await SolutionEdits.EnsureCompilesAsync(solution, updated, cancellationToken);
        await MovingSupport.ApplyAsync(solution, updated, cancellationToken);
        return $"Successfully made {className} derive from {field.Type.Name} in place of {fieldName}";
    }
}

/// <summary>
/// Turns delegation to a field into inheritance: the field's class becomes
/// the base class, uses of the field become uses of the instance itself, and
/// members that only forward to the field give way to the inherited ones.
/// </summary>
internal sealed class Inheritance
{
    private readonly Solution _solution;
    private readonly INamedTypeSymbol _type;
    private readonly IFieldSymbol _field;
    private readonly INamedTypeSymbol _base;

    public Inheritance(Solution solution, INamedTypeSymbol type, IFieldSymbol field)
    {
        _solution = solution;
        _type = type;
        _field = field;
        _base = field.Type as INamedTypeSymbol
            ?? throw new McpException($"Error: {field.Name} is not of a class type");
    }

    public async Task<Solution> ReplaceAsync(CancellationToken cancellationToken)
    {
        if (_type.TypeKind != TypeKind.Class || _type.IsStatic || _type.IsRecord || _type.DeclaringSyntaxReferences.Length != 1)
            throw new McpException($"Error: {_type.Name} is not a single, non-static class declaration that can take a base class");
        if (_type.BaseType is { SpecialType: not SpecialType.System_Object })
            throw new McpException($"Error: {_type.Name} already derives from {_type.BaseType.Name}, and a class has only one base class");
        if (_base.TypeKind != TypeKind.Class || _base.IsSealed || _base.IsStatic)
            throw new McpException($"Error: {_base.Name} is not a class that can be derived from");
        if (_field.IsStatic || _field.DeclaredAccessibility != Accessibility.Private)
            throw new McpException($"Error: {_field.Name} is not a private instance field, so code outside {_type.Name} may rely on it");

        var variable = (VariableDeclaratorSyntax)await _field.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken);
        var fieldDeclaration = (FieldDeclarationSyntax)variable.Parent!.Parent!;
        var document = _solution.GetDocument(variable.SyntaxTree)!;
        var model = (await document.GetSemanticModelAsync(cancellationToken))!;
        EnsureCreatesItsOwnInstance(variable, fieldDeclaration, model);

        var declaration = (TypeDeclarationSyntax)await _type.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken);
        var forwarding = declaration.Members.Where(m => IsForwarding(m, model)).ToList();
        EnsureNothingHidden(declaration, forwarding, model);

        var edits = new SyntaxEdits();
        foreach (var reference in await FieldUsesAsync(cancellationToken))
        {
            var (referenceDocument, name) = reference;
            if (forwarding.Any(f => f.Span.Contains(name.Span)))
                continue;

            if (MemberReferences.IsWrittenTo(name))
                throw new McpException($"Error: {_field.Name} is assigned at {SolutionEdits.Describe(name.GetLocation())}, so it is not always the instance its initializer creates");

            var use = name.Parent is MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } self && self.Name == name ? self : (ExpressionSyntax)name;
            if (use.Parent is not MemberAccessExpressionSyntax access || access.Expression != use)
                throw new McpException($"Error: {_field.Name} is used as a value at {SolutionEdits.Describe(name.GetLocation())}, where the instance itself would not be the same object");

            edits.Replace(referenceDocument, use, current => ThisExpression()
                .WithTriviaFrom(current));
            edits.Replace(referenceDocument, access, current => current.WithAdditionalAnnotations(Simplifier.Annotation));
        }

        // Members are only edited inside or removed, never moved, so each keeps
        // its index; removing from the last keeps the earlier indexes valid.
        var removed = forwarding.Append<MemberDeclarationSyntax>(fieldDeclaration)
            .Select(m => declaration.Members.IndexOf(m))
            .OrderByDescending(i => i)
            .ToList();
        edits.Replace(document.Id, declaration, current =>
        {
            var type = (TypeDeclarationSyntax)current;
            var members = type.Members;
            foreach (var index in removed)
                members = MemberLayout.Remove(members, members[index]);
            return AddBase(type.WithMembers(members));
        });

        return await edits.ApplyAsync(_solution, cancellationToken);
    }

    private async Task<List<(DocumentId Document, SimpleNameSyntax Name)>> FieldUsesAsync(CancellationToken cancellationToken)
    {
        var uses = new List<(DocumentId, SimpleNameSyntax)>();
        foreach (var reference in await MemberReferences.FindAsync(_solution, _field, cancellationToken))
            uses.Add((reference.Document.Id, reference.Name));
        return uses;
    }

    /// <summary>
    /// The field must hold an instance only it created, with the base class's
    /// parameterless constructor, which is what the class's own constructors
    /// will call once they chain to the base class.
    /// </summary>
    private void EnsureCreatesItsOwnInstance(VariableDeclaratorSyntax variable, FieldDeclarationSyntax declaration, SemanticModel model)
    {
        var created = variable.Initializer?.Value is BaseObjectCreationExpressionSyntax creation
            && (creation.ArgumentList is null || creation.ArgumentList.Arguments.Count == 0)
            && creation.Initializer is null
            && SymbolEqualityComparer.Default.Equals(model.GetTypeInfo(creation).Type, _base);
        if (!created)
            throw new McpException($"Error: {_field.Name} is not initialised with a new {_base.Name}(), so the instance it holds is not one the class could be itself");
        if (declaration.Declaration.Variables.Count > 1)
            throw new McpException($"Error: {_field.Name} is declared alongside other fields; split the declaration first");
    }

    /// <summary>
    /// Whether a member only forwards to the same member of the field, with
    /// the same signature and accessibility, so the inherited member can
    /// take its place.
    /// </summary>
    private bool IsForwarding(MemberDeclarationSyntax member, SemanticModel model)
    {
        var symbol = model.GetDeclaredSymbol(member);
        if (symbol is null || symbol.IsStatic)
            return false;

        var inherited = Inherited(symbol);
        if (inherited is null || inherited.DeclaredAccessibility != symbol.DeclaredAccessibility)
            return false;

        return member switch
        {
            MethodDeclarationSyntax method => ForwardsCall(Body(method.Body, method.ExpressionBody), model, (IMethodSymbol)symbol),
            PropertyDeclarationSyntax property => ForwardsProperty(property, model),
            _ => false,
        };
    }

    /// <summary>The member of the new base class with the same name and signature, if any.</summary>
    private ISymbol? Inherited(ISymbol member)
    {
        for (var type = _base; type is not null && type.SpecialType != SpecialType.System_Object; type = type.BaseType)
        {
            foreach (var candidate in type.GetMembers(member.Name))
            {
                var same = (member, candidate) switch
                {
                    (IMethodSymbol m, IMethodSymbol c) => !c.IsStatic
                        && SymbolEqualityComparer.Default.Equals(m.ReturnType, c.ReturnType)
                        && m.Parameters.Select(p => (p.Type, p.RefKind)).SequenceEqual(
                            c.Parameters.Select(p => (p.Type, p.RefKind)),
                            new ParameterComparer()),
                    (IPropertySymbol p, IPropertySymbol c) => !c.IsStatic && !p.IsIndexer && SymbolEqualityComparer.Default.Equals(p.Type, c.Type)
                        && (p.SetMethod is null || c.SetMethod is { DeclaredAccessibility: var access } && access == p.SetMethod.DeclaredAccessibility),
                    _ => false,
                };
                if (same)
                    return candidate;
            }
        }

        return null;
    }

    private sealed class ParameterComparer : IEqualityComparer<(ITypeSymbol Type, RefKind RefKind)>
    {
        public bool Equals((ITypeSymbol Type, RefKind RefKind) x, (ITypeSymbol Type, RefKind RefKind) y) =>
            x.RefKind == y.RefKind && SymbolEqualityComparer.Default.Equals(x.Type, y.Type);

        public int GetHashCode((ITypeSymbol Type, RefKind RefKind) obj) => obj.RefKind.GetHashCode();
    }

    private static ExpressionSyntax? Body(BlockSyntax? block, ArrowExpressionClauseSyntax? arrow) => (block, arrow) switch
    {
        (null, { } expression) => expression.Expression,
        ({ Statements: [ReturnStatementSyntax { Expression: { } value }] }, _) => value,
        ({ Statements: [ExpressionStatementSyntax { Expression: var value }] }, _) => value,
        _ => null,
    };

    /// <summary><c>_field.M(a, b)</c>, passing the method's own parameters in order.</summary>
    private bool ForwardsCall(ExpressionSyntax? body, SemanticModel model, IMethodSymbol method) =>
        body is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } call
        && access.Name.Identifier.ValueText == method.Name
        && IsField(access.Expression, model)
        && call.ArgumentList.Arguments.Count == method.Parameters.Length
        && call.ArgumentList.Arguments.Select((a, i) => a.NameColon is null
            && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(a.Expression).Symbol, method.Parameters[i])).All(same => same);

    /// <summary><c>_field.P</c> for the getter, and <c>_field.P = value</c> for a setter if there is one.</summary>
    private bool ForwardsProperty(PropertyDeclarationSyntax property, SemanticModel model)
    {
        if (property.ExpressionBody is { } arrow)
            return IsFieldMember(arrow.Expression, property.Identifier.ValueText, model);
        if (property.AccessorList is null || property.Initializer is not null)
            return false;

        return property.AccessorList.Accessors.All(accessor =>
        {
            var body = Body(accessor.Body, accessor.ExpressionBody);
            return accessor.Kind() switch
            {
                SyntaxKind.GetAccessorDeclaration => IsFieldMember(body, property.Identifier.ValueText, model),
                SyntaxKind.SetAccessorDeclaration => body is AssignmentExpressionSyntax { Right: IdentifierNameSyntax { Identifier.ValueText: "value" } } assignment
                    && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                    && IsFieldMember(assignment.Left, property.Identifier.ValueText, model),
                _ => false,
            };
        });
    }

    private bool IsFieldMember(ExpressionSyntax? expression, string name, SemanticModel model) =>
        expression is MemberAccessExpressionSyntax access && access.Name.Identifier.ValueText == name && IsField(access.Expression, model);

    private bool IsField(ExpressionSyntax expression, SemanticModel model) =>
        SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(expression).Symbol, _field);

    /// <summary>
    /// A member the class keeps must not share a name with an accessible
    /// member of the new base class, which it would hide or be confused with.
    /// </summary>
    private void EnsureNothingHidden(TypeDeclarationSyntax declaration, IReadOnlyList<MemberDeclarationSyntax> forwarding, SemanticModel model)
    {
        foreach (var member in declaration.Members.Except(forwarding))
        {
            var names = member is FieldDeclarationSyntax field
                ? field.Declaration.Variables.Select(v => v.Identifier.ValueText)
                : new[] { model.GetDeclaredSymbol(member)?.Name };
            foreach (var name in names.OfType<string>())
            {
                if (name == _field.Name || member is ConstructorDeclarationSyntax)
                    continue;

                for (var type = _base; type is not null && type.SpecialType != SpecialType.System_Object; type = type.BaseType)
                {
                    if (type.GetMembers(name).Any(m => m.DeclaredAccessibility != Accessibility.Private && !m.IsImplicitlyDeclared))
                        throw new McpException($"Error: {_type.Name}.{name} would hide {type.Name}.{name}, which does not only forward to it");
                }
            }
        }
    }

    /// <summary>The new base class goes first in the base list, ahead of any interfaces.</summary>
    private TypeDeclarationSyntax AddBase(TypeDeclarationSyntax type)
    {
        var baseType = SimpleBaseType(MovingSupport.QualifiedType(_base));
        if (type.BaseList is { } list)
            return type.WithBaseList(list.WithTypes(list.Types.Insert(0, baseType)));

        var name = type.Identifier;
        var trailing = type.TypeParameterList?.GetTrailingTrivia() ?? name.TrailingTrivia;
        type = type.TypeParameterList is { } parameters
            ? type.WithTypeParameterList(parameters.WithTrailingTrivia(Space))
            : type.WithIdentifier(name.WithTrailingTrivia(Space));
        return type.WithBaseList(BaseList(SingletonSeparatedList<BaseTypeSyntax>(baseType))
            .WithColonToken(Token(SyntaxKind.ColonToken).WithTrailingTrivia(Space))
            .WithTrailingTrivia(trailing));
    }
}
