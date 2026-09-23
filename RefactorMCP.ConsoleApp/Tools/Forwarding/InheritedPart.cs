using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using ModelContextProtocol;
using RefactorMCP.ConsoleApp.Tools.Moving;

namespace RefactorMCP.ConsoleApp.Tools.Forwarding;

/// <summary>
/// The part of a class's instances that its base class makes up, paired with
/// a private field holding a new instance of that base class: the two start
/// out alike, so what one holds can be swapped for the other as long as
/// nothing else reaches either.
/// </summary>
internal sealed class InheritedPart
{
    private readonly Solution _solution;

    private InheritedPart(Solution solution, INamedTypeSymbol type, INamedTypeSymbol baseType, IFieldSymbol field, FieldDeclarationSyntax declaration)
    {
        _solution = solution;
        Type = type;
        Base = baseType;
        Field = field;
        Declaration = declaration;
    }

    public INamedTypeSymbol Type { get; }

    public INamedTypeSymbol Base { get; }

    public IFieldSymbol Field { get; }

    public FieldDeclarationSyntax Declaration { get; }

    /// <summary>
    /// Checks that the class derives from a class, that the field is a private
    /// instance field of exactly that class initialised with a new instance
    /// made as the class's constructors make the base class part, and that the
    /// class overrides nothing of the base class, which would make the base
    /// class part behave unlike the field's object.
    /// </summary>
    public static async Task<InheritedPart> FindAsync(Solution solution, INamedTypeSymbol type, string fieldName, CancellationToken cancellationToken)
    {
        if (type.TypeKind != TypeKind.Class)
            throw new McpException($"Error: {type.Name} is not a class, so it has no base class");
        if (type.BaseType is not { SpecialType: not SpecialType.System_Object } baseType)
            throw new McpException($"Error: {type.Name} has no base class other than object");

        var field = type.GetMembers(fieldName).OfType<IFieldSymbol>().FirstOrDefault()
            ?? throw new McpException($"Error: {type.Name} has no field named '{fieldName}'");
        if (!SymbolEqualityComparer.Default.Equals(field.Type, baseType))
            throw new McpException($"Error: {fieldName} is a {field.Type.ToDisplayString()}, not of the base class {baseType.ToDisplayString()}");
        if (field.IsStatic || field.DeclaredAccessibility != Accessibility.Private)
            throw new McpException($"Error: {fieldName} is not a private instance field, so code outside {type.Name} may change what it holds");

        var variable = (VariableDeclaratorSyntax)await field.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken);
        var model = (await solution.GetDocument(variable.SyntaxTree)!.GetSemanticModelAsync(cancellationToken))!;
        var created = variable.Initializer?.Value is BaseObjectCreationExpressionSyntax creation
            && (creation.ArgumentList is null || creation.ArgumentList.Arguments.Count == 0)
            && creation.Initializer is null
            && SymbolEqualityComparer.Default.Equals(model.GetTypeInfo(creation, cancellationToken).Type, baseType);
        if (!created)
            throw new McpException($"Error: {fieldName} is not initialised with a new {baseType.Name}() as the base class part of {type.Name} is constructed");

        await EnsureBaseConstructedWithoutArgumentsAsync(type, baseType, cancellationToken);
        await EnsureNothingOverriddenAsync(solution, type, baseType, cancellationToken);
        return new InheritedPart(solution, type, baseType, field, (FieldDeclarationSyntax)variable.Parent!.Parent!);
    }

    private static async Task EnsureBaseConstructedWithoutArgumentsAsync(INamedTypeSymbol type, INamedTypeSymbol baseType, CancellationToken cancellationToken)
    {
        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            var declaration = (TypeDeclarationSyntax)await reference.GetSyntaxAsync(cancellationToken);
            var passes = declaration.Members.OfType<ConstructorDeclarationSyntax>()
                    .Any(c => c.Initializer is { } init && init.IsKind(SyntaxKind.BaseConstructorInitializer) && init.ArgumentList.Arguments.Count > 0)
                || declaration.BaseList?.Types.OfType<PrimaryConstructorBaseTypeSyntax>().Any(t => t.ArgumentList.Arguments.Count > 0) == true;
            if (passes)
                throw new McpException($"Error: {type.Name} passes arguments to the constructor of {baseType.Name}, so its base class part is not a new {baseType.Name}()");
        }
    }

    private static async Task EnsureNothingOverriddenAsync(Solution solution, INamedTypeSymbol type, INamedTypeSymbol baseType, CancellationToken cancellationToken)
    {
        var derived = await SymbolFinder.FindDerivedClassesAsync(type, solution, transitive: true, cancellationToken: cancellationToken);
        foreach (var current in derived.Prepend(type))
        {
            foreach (var member in current.GetMembers().Where(m => m.IsOverride))
            {
                for (var overridden = Overridden(member); overridden is not null; overridden = Overridden(overridden))
                {
                    if (overridden.ContainingType.SpecialType != SpecialType.System_Object && MemberReferences.InheritsFrom(baseType, overridden.ContainingType))
                        throw new McpException($"Error: {current.Name}.{member.Name} overrides a member of {baseType.Name}, which the base class part would call where the field's object would not");
                }
            }
        }
    }

    private static ISymbol? Overridden(ISymbol member) => member switch
    {
        IMethodSymbol method => method.OverriddenMethod,
        IPropertySymbol property => property.OverriddenProperty,
        IEventSymbol @event => @event.OverriddenEvent,
        _ => null,
    };

    /// <summary>
    /// A use of an inherited instance member: inside the class through an
    /// implicit <c>this</c>, <c>this</c> or <c>base</c>, or elsewhere through
    /// an instance of the class. Implicit uses, such as a <c>foreach</c>
    /// calling <c>GetEnumerator</c>, have no callee to rewrite.
    /// </summary>
    public sealed record Use(Document Document, SemanticModel Model, ISymbol Member, ExpressionSyntax? Callee, bool InsideOnThis, string Where);

    /// <summary>Every use of the base class part through the members the class inherits.</summary>
    public async Task<List<Use>> UsesAsync(CancellationToken cancellationToken)
    {
        var uses = new List<Use>();
        foreach (var member in InheritedMembers())
        {
            foreach (var location in (await SymbolFinder.FindReferencesAsync(member, _solution, cancellationToken)).SelectMany(r => r.Locations))
            {
                if (!location.Location.IsInSource)
                    continue;

                var root = (await location.Document.GetSyntaxRootAsync(cancellationToken))!;
                var model = (await location.Document.GetSemanticModelAsync(cancellationToken))!;
                var node = root.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true);
                var where = SolutionEdits.Describe(location.Location);
                if (location.IsImplicit)
                {
                    if (ReachesClass(node, model, cancellationToken))
                        uses.Add(new Use(location.Document, model, member, null, false, where));
                    continue;
                }

                var callee = ForwardingMembers.Callee(node, member);
                if (callee is null)
                    continue;

                var receiver = ForwardingMembers.Receiver(callee);
                if (InsideClass(callee, model, cancellationToken) && callee is not MemberBindingExpressionSyntax
                    && receiver is null or ThisExpressionSyntax or BaseExpressionSyntax)
                {
                    uses.Add(new Use(location.Document, model, member, callee, true, where));
                    continue;
                }

                var receiverType = receiver is null
                    ? model.GetEnclosingSymbol(callee.SpanStart, cancellationToken)?.ContainingType
                    : model.GetTypeInfo(receiver, cancellationToken).Type;
                if (receiverType is INamedTypeSymbol named && MemberReferences.InheritsFrom(named, Type))
                    uses.Add(new Use(location.Document, model, member, callee, false, where));
            }
        }

        return uses;
    }

    /// <summary>
    /// Whether an implicit use, such as <c>foreach</c> over an expression or a
    /// collection initializer, applies to an instance of the class.
    /// </summary>
    private bool ReachesClass(SyntaxNode node, SemanticModel model, CancellationToken cancellationToken)
    {
        var expression = node switch
        {
            ForEachStatementSyntax loop => loop.Expression,
            _ => node.AncestorsAndSelf().OfType<ExpressionSyntax>().FirstOrDefault(e => e is ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax)
                ?? node as ExpressionSyntax,
        };
        if (expression is null)
            return true;

        var type = model.GetTypeInfo(expression, cancellationToken).Type as INamedTypeSymbol;
        return type is null || MemberReferences.InheritsFrom(type, Type)
            || model.GetEnclosingSymbol(expression.SpanStart, cancellationToken)?.ContainingType is { } enclosing && MemberReferences.InheritsFrom(enclosing, Type);
    }

    private bool InsideClass(SyntaxNode node, SemanticModel model, CancellationToken cancellationToken)
    {
        var enclosing = node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        return enclosing is not null && SymbolEqualityComparer.Default.Equals(model.GetDeclaredSymbol(enclosing, cancellationToken), Type);
    }

    /// <summary>The instance members of the base class and its bases, short of object, that the class inherits.</summary>
    private IEnumerable<ISymbol> InheritedMembers()
    {
        for (var current = Base; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                if (member.IsImplicitlyDeclared || member.IsStatic || member.DeclaredAccessibility == Accessibility.Private
                    || member is IMethodSymbol { MethodKind: not MethodKind.Ordinary } or INamedTypeSymbol)
                    continue;
                yield return member;
            }
        }
    }

    /// <summary>
    /// Refuses when code converts an instance of the class to its base class,
    /// or to an interface the base class implements, and so could reach the
    /// base class part without naming an inherited member.
    /// </summary>
    public async Task EnsureNotConvertedAsync(CancellationToken cancellationToken)
    {
        var project = _solution.GetDocument(Declaration.SyntaxTree)!.Project;
        var projects = _solution.GetProjectDependencyGraph().GetProjectsThatTransitivelyDependOnThisProject(project.Id).Append(project.Id);
        foreach (var document in projects.SelectMany(id => _solution.GetProject(id)!.Documents))
        {
            var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
            var model = (await document.GetSemanticModelAsync(cancellationToken))!;
            foreach (var expression in root.DescendantNodes().OfType<ExpressionSyntax>())
            {
                var (from, to) = expression switch
                {
                    CastExpressionSyntax cast => (model.GetTypeInfo(cast.Expression, cancellationToken).Type, model.GetTypeInfo(cast.Type, cancellationToken).Type),
                    BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AsExpression) =>
                        (model.GetTypeInfo(binary.Left, cancellationToken).Type, model.GetTypeInfo(binary.Right, cancellationToken).Type),
                    _ => (model.GetTypeInfo(expression, cancellationToken).Type, model.GetTypeInfo(expression, cancellationToken).ConvertedType),
                };
                if (from is INamedTypeSymbol named && MemberReferences.InheritsFrom(named, Type) && IsBasePart(to))
                    throw new McpException($"Error: {SolutionEdits.Describe(expression.GetLocation())} converts {Type.Name} to {to!.ToDisplayString()}, which reaches its base class part");
            }
        }
    }

    private bool IsBasePart(ITypeSymbol? type) => type switch
    {
        INamedTypeSymbol { TypeKind: TypeKind.Interface } face => Base.AllInterfaces.Contains(face, SymbolEqualityComparer.Default),
        INamedTypeSymbol { SpecialType: not SpecialType.System_Object } named => MemberReferences.InheritsFrom(Base, named),
        _ => false,
    };

    /// <summary>Refuses a use of an inherited member, which reaches the base class part.</summary>
    public McpException InUse(Use use) =>
        new($"Error: {use.Where} uses the inherited member {use.Member.Name} of {Type.Name}, which reaches its base class part");
}
