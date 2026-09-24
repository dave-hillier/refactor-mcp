using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using RefactorMCP.ConsoleApp.Tools.Moving;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace RefactorMCP.ConsoleApp.Tools.Composites;

[McpServerToolType]
public static class ReplaceInheritanceWithDelegationTool
{
    [McpServerTool, Description("Replace Inheritance with Delegation: give a class a field holding an instance of its base class, " +
        "reach inherited members through it, add forwarding members for the inherited members other code uses, and remove the base class. " +
        "Refuses, changing nothing, when the class overrides base members, uses protected ones, or code relies on it being its base class.")]
    public static async Task<string> ReplaceInheritanceWithDelegation(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the class")] string filePath,
        [Description("Name of the class")] string className,
        [Description("Name of the field that holds the old base class (optional, defaults to _ and the base class name in camel case)")] string? fieldName = null,
        CancellationToken cancellationToken = default)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var document = MovingSupport.DocumentOrThrow(solution, filePath);
        var type = (INamedTypeSymbol)await MovingSupport.FindDeclaredSymbolAsync(
            document, className, null, s => s is INamedTypeSymbol, "type", cancellationToken);

        var delegation = new Delegation(solution, type, fieldName);
        var updated = await delegation.ReplaceAsync(cancellationToken);
        await SolutionEdits.EnsureCompilesAsync(solution, updated, cancellationToken);
        await MovingSupport.ApplyAsync(solution, updated, cancellationToken);
        return $"Successfully replaced {className}'s base class with the field {delegation.FieldName}";
    }
}

/// <summary>
/// Turns a class's inheritance into delegation: its inherited members are
/// reached through a field holding an instance of the old base class, and
/// the ones other code uses are forwarded by members of its own.
/// </summary>
internal sealed class Delegation
{
    private readonly Solution _solution;
    private readonly INamedTypeSymbol _type;
    private readonly INamedTypeSymbol _base;
    private readonly SyntaxEdits _edits = new();

    /// <summary>Inherited members other code uses through the class, and whether it writes them.</summary>
    private readonly Dictionary<ISymbol, bool> _forwarded = new(SymbolEqualityComparer.Default);

    public Delegation(Solution solution, INamedTypeSymbol type, string? fieldName)
    {
        if (type.TypeKind != TypeKind.Class || type.IsStatic || type.IsRecord)
            throw new McpException($"Error: {type.Name} is not a class that derives from another");
        if (type.BaseType is null || type.BaseType.SpecialType == SpecialType.System_Object)
            throw new McpException($"Error: {type.Name} has no base class to replace with delegation");

        _solution = solution;
        _type = type;
        _base = type.BaseType;
        FieldName = fieldName ?? "_" + MovingSupport.CamelCase(_base.Name).TrimStart('@');
    }

    public string FieldName { get; }

    public async Task<Solution> ReplaceAsync(CancellationToken cancellationToken)
    {
        for (var current = _type; current is not null; current = current.BaseType)
        {
            if (current.GetMembers(FieldName).Any(m => !m.IsImplicitlyDeclared))
                throw new McpException($"Error: {current.Name} already has a member named '{FieldName}'");
        }

        var overriding = _type.GetMembers().FirstOrDefault(m => m.IsOverride && !IsObjectMember(m));
        if (overriding is not null)
            throw new McpException($"Error: {_type.Name}.{overriding.Name} overrides a member of {_base.Name}, which {_base.Name} would no longer call");

        if (_type.DeclaringSyntaxReferences.Length != 1)
            throw new McpException($"Error: {_type.Name} is partial; merge its parts first");

        foreach (var member in InheritedMembers())
            await CollectUsesAsync(member, cancellationToken);

        var declaration = (TypeDeclarationSyntax)await _type.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken);
        var document = _solution.GetDocument(declaration.SyntaxTree)!;
        var model = (await document.GetSemanticModelAsync(cancellationToken))!;
        var baseSyntax = declaration.BaseList!.Types.First(t =>
            SymbolEqualityComparer.Default.Equals(model.GetTypeInfo(t.Type, cancellationToken).Type, _base));

        var constructors = declaration.Members.OfType<ConstructorDeclarationSyntax>().Where(c => !c.Modifiers.Any(SyntaxKind.StaticKeyword)).ToList();
        var initialiseInConstructors = constructors.Any(c => c.Initializer is { } init
            && init.IsKind(SyntaxKind.BaseConstructorInitializer) && init.ArgumentList.Arguments.Count > 0);
        foreach (var constructor in constructors)
            _edits.Replace(document.Id, constructor, current => Construct((ConstructorDeclarationSyntax)current, baseSyntax.Type, initialiseInConstructors));

        var field = Field(baseSyntax.Type, initialiseInConstructors);
        var forwarding = _forwarded
            .OrderBy(f => f.Key is IPropertySymbol { IsIndexer: false } ? 0 : f.Key is IPropertySymbol ? 1 : 2)
            .ThenBy(f => f.Key.Name, StringComparer.Ordinal)
            .Select(f => Forwarding(f.Key, f.Value))
            .ToList();
        _edits.Replace(document.Id, declaration, current =>
        {
            var type = ChangeBaseTypeTool.WithoutBase((TypeDeclarationSyntax)current,
                ((TypeDeclarationSyntax)current).BaseList!.Types.First(t => t.Type.IsEquivalentTo(baseSyntax.Type)));
            var members = type.Members.InsertRange(0, forwarding.Prepend<MemberDeclarationSyntax>(field));
            if (type.Members.Count > 0)
                members = members.Replace(members[forwarding.Count + 1], WithBlankLineBefore(members[forwarding.Count + 1]));
            return type.WithMembers(members);
        });

        return await _edits.ApplyAsync(_solution, cancellationToken);
    }

    private static bool IsObjectMember(ISymbol member)
    {
        for (var overridden = member; overridden is not null; overridden = Overridden(overridden))
        {
            if (overridden.ContainingType.SpecialType == SpecialType.System_Object)
                return true;
        }

        return false;
    }

    private static ISymbol? Overridden(ISymbol member) => member switch
    {
        IMethodSymbol method => method.OverriddenMethod,
        IPropertySymbol property => property.OverriddenProperty,
        IEventSymbol @event => @event.OverriddenEvent,
        _ => null,
    };

    /// <summary>The members of the base class and its bases, short of object, that the class inherits.</summary>
    private IEnumerable<ISymbol> InheritedMembers()
    {
        for (var current = _base; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                if (member.IsImplicitlyDeclared || member.DeclaredAccessibility == Accessibility.Private
                    || member is IMethodSymbol { MethodKind: not MethodKind.Ordinary } or INamedTypeSymbol)
                    continue;
                yield return member;
            }
        }
    }

    /// <summary>
    /// Records each use of an inherited member: inside the class, through an
    /// implicit <c>this</c>, <c>this</c> or <c>base</c>, it is rewritten to go
    /// through the field; elsewhere, through an expression of the class's type,
    /// the member is forwarded.
    /// </summary>
    private async Task CollectUsesAsync(ISymbol member, CancellationToken cancellationToken)
    {
        var references = await SymbolFinder.FindReferencesAsync(member, _solution, cancellationToken);
        foreach (var location in references.SelectMany(r => r.Locations))
        {
            if (location.IsImplicit || !location.Location.IsInSource)
                continue;

            var root = (await location.Document.GetSyntaxRootAsync(cancellationToken))!;
            var model = (await location.Document.GetSemanticModelAsync(cancellationToken))!;
            var node = root.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true);
            var used = member is IPropertySymbol { IsIndexer: true }
                ? node.AncestorsAndSelf().OfType<ElementAccessExpressionSyntax>().FirstOrDefault()
                : node as SimpleNameSyntax as ExpressionSyntax;
            if (used is null)
                continue;

            var (receiver, access) = used switch
            {
                ElementAccessExpressionSyntax element => (element.Expression, (ExpressionSyntax)element),
                SimpleNameSyntax { Parent: MemberAccessExpressionSyntax outer } name when outer.Name == name => (outer.Expression, outer),
                SimpleNameSyntax { Parent: MemberBindingExpressionSyntax binding } => (binding.FirstAncestorOrSelf<ConditionalAccessExpressionSyntax>()!.Expression, binding),
                _ => ((ExpressionSyntax?)null, used),
            };

            var insideClass = InsideClass(used, model, cancellationToken);
            if (insideClass && receiver is null or ThisExpressionSyntax or BaseExpressionSyntax)
            {
                UseInside(location.Document.Id, member, used, receiver);
                continue;
            }

            var receiverType = receiver is null
                ? model.GetEnclosingSymbol(used.SpanStart, cancellationToken)?.ContainingType
                : model.GetTypeInfo(receiver, cancellationToken).Type;
            if (receiverType is INamedTypeSymbol named && MemberReferences.InheritsFrom(named, _type) && !member.IsStatic)
                Forward(member, MemberReferences.IsWrittenTo(access) || access is ElementAccessExpressionSyntax && IsAssigned(access));
        }
    }

    private bool InsideClass(SyntaxNode node, SemanticModel model, CancellationToken cancellationToken)
    {
        var enclosing = node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        return enclosing is not null && SymbolEqualityComparer.Default.Equals(model.GetDeclaredSymbol(enclosing, cancellationToken), _type);
    }

    private static bool IsAssigned(ExpressionSyntax access) => access.Parent switch
    {
        AssignmentExpressionSyntax assignment => assignment.Left == access,
        PrefixUnaryExpressionSyntax or PostfixUnaryExpressionSyntax => true,
        _ => false,
    };

    private void UseInside(DocumentId document, ISymbol member, ExpressionSyntax used, ExpressionSyntax? receiver)
    {
        if (member.DeclaredAccessibility is Accessibility.Protected or Accessibility.ProtectedAndInternal)
            throw new McpException($"Error: {_type.Name} uses the protected member {member.Name}, which a field of type {_base.Name} cannot reach");

        if (member.IsStatic)
        {
            if (receiver is null)
            {
                var owner = member.ContainingType;
                _edits.Replace(document, used, current => MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression, MovingSupport.QualifiedType(owner), ((SimpleNameSyntax)current).WithoutTrivia())
                    .WithTriviaFrom(current));
            }

            return;
        }

        if (receiver is not null)
            _edits.Replace(document, receiver, current => IdentifierName(FieldName).WithTriviaFrom(current));
        else
            _edits.Replace(document, used, current => MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression, IdentifierName(FieldName), ((SimpleNameSyntax)current).WithoutTrivia())
                .WithTriviaFrom(current));
    }

    private void Forward(ISymbol member, bool written)
    {
        if (member is IEventSymbol)
            throw new McpException($"Error: Code outside {_type.Name} uses the inherited event {member.Name}, which is not forwarded");
        if (member is IMethodSymbol method && (method.TypeParameters.Length > 0
            || method.Parameters.Any(p => p.RefKind != RefKind.None || p.IsParams || p.HasExplicitDefaultValue)))
            throw new McpException($"Error: Code outside {_type.Name} uses the inherited method {member.Name}, whose type parameters or ref, out, params or optional parameters are not forwarded");

        _forwarded[member] = _forwarded.TryGetValue(member, out var before) && before || written;
    }

    private FieldDeclarationSyntax Field(TypeSyntax baseType, bool initialiseInConstructors)
    {
        var variable = VariableDeclarator(FieldName);
        if (!initialiseInConstructors)
            variable = variable.WithInitializer(EqualsValueClause(
                ObjectCreationExpression(baseType.WithoutTrivia()).WithArgumentList(ArgumentList())));

        return FieldDeclaration(VariableDeclaration(baseType.WithoutTrivia(), SingletonSeparatedList(variable)))
            .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.ReadOnlyKeyword)))
            .WithAdditionalAnnotations(Formatter.Annotation);
    }

    /// <summary>
    /// A constructor that passed arguments to the base class creates the
    /// field's instance with them instead; one chaining to another constructor
    /// of the class leaves that to it.
    /// </summary>
    private ConstructorDeclarationSyntax Construct(ConstructorDeclarationSyntax constructor, TypeSyntax baseType, bool initialiseInConstructors)
    {
        if (constructor.Initializer?.IsKind(SyntaxKind.ThisConstructorInitializer) == true)
            return constructor;

        var arguments = constructor.Initializer?.ArgumentList ?? ArgumentList();
        var withoutInitializer = constructor.Initializer is null
            ? constructor
            : constructor.WithInitializer(null).WithParameterList(constructor.ParameterList.WithTrailingTrivia(constructor.Initializer.GetTrailingTrivia()));
        if (!initialiseInConstructors)
            return withoutInitializer;

        var assignment = ExpressionStatement(AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                IdentifierName(FieldName),
                ObjectCreationExpression(baseType.WithoutTrivia()).WithArgumentList(arguments.WithoutTrivia())))
            .WithAdditionalAnnotations(Formatter.Annotation);

        if (withoutInitializer.Body is { } body)
            return withoutInitializer.WithBody(body.WithStatements(body.Statements.Insert(0, assignment)));

        var expression = ExpressionStatement(withoutInitializer.ExpressionBody!.Expression.WithoutTrivia()).WithAdditionalAnnotations(Formatter.Annotation);
        return withoutInitializer
            .WithExpressionBody(null)
            .WithSemicolonToken(default)
            .WithBody(Block(assignment, expression).WithAdditionalAnnotations(Formatter.Annotation));
    }

    /// <summary>A member of the class that forwards to the inherited member through the field.</summary>
    private MemberDeclarationSyntax Forwarding(ISymbol member, bool written)
    {
        var modifiers = TokenList(Token(member.DeclaredAccessibility == Accessibility.Public ? SyntaxKind.PublicKeyword : SyntaxKind.InternalKeyword));
        var field = IdentifierName(FieldName);
        MemberDeclarationSyntax declaration;
        switch (member)
        {
            case IPropertySymbol { IsIndexer: true } indexer:
            {
                var parameters = indexer.Parameters.Select(p => Parameter(Identifier(p.Name)).WithType(MovingSupport.QualifiedType(p.Type)));
                var target = ElementAccessExpression(field, BracketedArgumentList(SeparatedList(indexer.Parameters.Select(p => Argument(IdentifierName(p.Name))))));
                declaration = IndexerDeclaration(MovingSupport.QualifiedType(indexer.Type))
                    .WithModifiers(modifiers)
                    .WithParameterList(BracketedParameterList(SeparatedList(parameters)))
                    .WithAccessorList(Accessors(target, written && indexer.SetMethod is not null));
                break;
            }

            case IPropertySymbol property:
            {
                var target = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, field, IdentifierName(property.Name));
                var named = PropertyDeclaration(MovingSupport.QualifiedType(property.Type), property.Name).WithModifiers(modifiers);
                declaration = written && property.SetMethod is not null
                    ? named.WithAccessorList(Accessors(target, settable: true))
                    : named.WithExpressionBody(ArrowExpressionClause(target)).WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
                break;
            }

            case IFieldSymbol inheritedField:
            {
                var target = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, field, IdentifierName(inheritedField.Name));
                var named = PropertyDeclaration(MovingSupport.QualifiedType(inheritedField.Type), inheritedField.Name).WithModifiers(modifiers);
                declaration = written && !inheritedField.IsReadOnly
                    ? named.WithAccessorList(Accessors(target, settable: true))
                    : named.WithExpressionBody(ArrowExpressionClause(target)).WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
                break;
            }

            case IMethodSymbol method:
            {
                var parameters = method.Parameters.Select(p => Parameter(Identifier(p.Name)).WithType(MovingSupport.QualifiedType(p.Type)));
                var call = InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, field, IdentifierName(method.Name)),
                    ArgumentList(SeparatedList(method.Parameters.Select(p => Argument(IdentifierName(p.Name))))));
                declaration = MethodDeclaration(method.ReturnsVoid ? PredefinedType(Token(SyntaxKind.VoidKeyword)) : MovingSupport.QualifiedType(method.ReturnType), method.Name)
                    .WithModifiers(modifiers)
                    .WithParameterList(ParameterList(SeparatedList(parameters)))
                    .WithExpressionBody(ArrowExpressionClause(call))
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
                break;
            }

            default:
                throw new McpException($"Error: Code outside {_type.Name} uses the inherited member {member.Name}, which is not forwarded");
        }

        return WithBlankLineBefore(declaration.WithAdditionalAnnotations(Formatter.Annotation));
    }

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

    private static TMember WithBlankLineBefore<TMember>(TMember member) where TMember : MemberDeclarationSyntax =>
        member.WithLeadingTrivia(MemberLayout.WithoutLeadingBlankLines(member.GetLeadingTrivia()).Insert(0, EndOfLine("\n")));
}
