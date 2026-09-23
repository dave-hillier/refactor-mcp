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
using ModelContextProtocol;
using ModelContextProtocol.Server;
using RefactorMCP.ConsoleApp.Tools.Moving;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace RefactorMCP.ConsoleApp.Tools.Forwarding;

[McpServerToolType]
public static class AddDelegatingMemberTool
{
    [McpServerTool, Description("Add to a class a member that forwards to the member of the same name of one of its fields, or of its base class. " +
        "Uses of an inherited member the new one hides inside the class are written with base so they keep their meaning; " +
        "refuses when code elsewhere would reach something different.")]
    public static async Task<string> AddDelegatingMember(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file declaring the class")] string filePath,
        [Description("Name of the class")] string className,
        [Description("The member to forward to: its name, 'this' for an indexer, or its documentation comment id when overloads share the name")] string memberName,
        [Description("The name of the field to forward through, or 'base' for the base class")] string via,
        CancellationToken cancellationToken = default)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var document = MovingSupport.DocumentOrThrow(solution, filePath);
        var type = (INamedTypeSymbol)await MovingSupport.FindDeclaredSymbolAsync(
            document, className, null, s => s is INamedTypeSymbol, "type", cancellationToken);

        var adder = new DelegatingMemberAdder(solution, type, via);
        var member = await adder.FindMemberAsync(memberName, cancellationToken);
        var updated = await adder.AddAsync(member, cancellationToken);
        await SolutionEdits.EnsureCompilesAsync(solution, updated, cancellationToken);
        await MovingSupport.ApplyAsync(solution, updated, cancellationToken);
        return $"Successfully added {className}.{member.Name} forwarding to {via}";
    }
}

/// <summary>
/// Adds a member to a class that forwards to a member of a field or of the
/// base class, keeping every existing reference bound as it was, or bound to
/// the new member only where that reaches the same code.
/// </summary>
internal sealed class DelegatingMemberAdder
{
    private const string Base = "base";

    private readonly Solution _solution;
    private readonly INamedTypeSymbol _type;
    private readonly IFieldSymbol? _field;
    private readonly ITypeSymbol _target;

    public DelegatingMemberAdder(Solution solution, INamedTypeSymbol type, string via)
    {
        if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct))
            throw new McpException($"Error: {type.Name} is not a class or struct, so it cannot hold a forwarding member");

        _solution = solution;
        _type = type;
        if (via == Base)
        {
            _target = type.TypeKind == TypeKind.Class && type.BaseType is { SpecialType: not SpecialType.System_Object } baseType
                ? baseType
                : throw new McpException($"Error: {type.Name} has no base class to forward to");
        }
        else
        {
            _field = type.GetMembers(via).OfType<IFieldSymbol>().FirstOrDefault()
                ?? throw new McpException($"Error: {type.Name} has no field named '{via}'");
            _target = _field.Type;
        }
    }

    /// <summary>
    /// The member of the field's type, or of the base class, that the class
    /// can reach: named, <c>this</c> for an indexer, or by documentation
    /// comment id.
    /// </summary>
    public async Task<ISymbol> FindMemberAsync(string memberName, CancellationToken cancellationToken)
    {
        var compilation = (await _solution.GetDocument(_type.DeclaringSyntaxReferences[0].SyntaxTree)!.Project.GetCompilationAsync(cancellationToken))!;
        var byId = memberName.Length > 2 && memberName[1] == ':'
            ? DocumentationCommentId.GetFirstSymbolForDeclarationId(memberName, compilation)
                ?? throw new McpException($"Error: {_target.Name} has no member '{memberName}' that {_type.Name} can reach")
            : null;
        var name = byId?.Name ?? (memberName == "this" ? WellKnownMemberNames.Indexer : memberName);

        var candidates = new List<ISymbol>();
        foreach (var member in Members(name))
        {
            if (member.IsImplicitlyDeclared
                || member is IMethodSymbol { MethodKind: not MethodKind.Ordinary }
                || !compilation.IsSymbolAccessibleWithin(member, _type, _field is null ? null : _target)
                || byId is not null && !SymbolEqualityComparer.Default.Equals(member.OriginalDefinition, byId)
                || candidates.Any(c => ForwardingMembers.SameSignature(c, member)))
            {
                continue;
            }

            candidates.Add(member);
        }

        var found = candidates.Count switch
        {
            0 => throw new McpException($"Error: {_target.Name} has no member '{memberName}' that {_type.Name} can reach"),
            1 => candidates[0],
            _ => throw new McpException($"Error: Several members of {_target.Name} are named '{memberName}'; name the one to forward by its documentation comment id"),
        };

        var unsupported = found switch
        {
            { IsStatic: true } => "it is static",
            IEventSymbol => "it is an event",
            IMethodSymbol { TypeParameters.Length: > 0 } => "it has type parameters",
            IMethodSymbol { ReturnsByRef: true } or IMethodSymbol { ReturnsByRefReadonly: true } => "it returns by reference",
            IMethodSymbol method when method.Parameters.Any(p => p.RefKind != RefKind.None || p.IsParams || p.HasExplicitDefaultValue) =>
                "it has ref, out, in, params or optional parameters",
            IPropertySymbol { ReturnsByRef: true } or IPropertySymbol { ReturnsByRefReadonly: true } => "it returns by reference",
            IPropertySymbol or IFieldSymbol or IMethodSymbol => null,
            _ => $"it is a {found.Kind.ToString().ToLowerInvariant()}",
        };
        if (unsupported is not null)
            throw new McpException($"Error: {found.Name} cannot be forwarded: {unsupported}");

        return found;
    }

    /// <summary>
    /// The members of the target type and the classes it inherits from, most
    /// derived first; an interface's own interfaces stand in for base classes.
    /// </summary>
    private IEnumerable<ISymbol> Members(string name)
    {
        var types = new List<ITypeSymbol>();
        for (var current = _target; current is not null; current = current.BaseType)
            types.Add(current);
        if (_target.TypeKind == TypeKind.Interface)
            types.AddRange(_target.AllInterfaces);

        return types.SelectMany(t => t.GetMembers(name));
    }

    public async Task<Solution> AddAsync(ISymbol member, CancellationToken cancellationToken)
    {
        var name = member.Name;
        var conflict = _type.GetMembers(name).FirstOrDefault(m => !m.IsImplicitlyDeclared
            && (m is not IMethodSymbol || member is not IMethodSymbol || SameParameterTypes((IMethodSymbol)m, (IMethodSymbol)member)));
        if (conflict is not null)
            throw new McpException($"Error: {_type.Name} already has a member named '{DisplayName(member)}'");

        var receiver = _field is null ? (ExpressionSyntax)BaseExpression() : IdentifierName(_field.Name);
        var forwarding = ForwardingMembers.Create(member, receiver, Settable(member));

        var declaration = (TypeDeclarationSyntax)await _type.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken);
        var document = _solution.GetDocument(declaration.SyntaxTree)!;
        var model = (await document.GetSemanticModelAsync(cancellationToken))!;

        // Every reference to a member of the same name is marked, so that once
        // the member is added it can be checked still to mean the same thing.
        var edits = new SyntaxEdits();
        var references = await ReferencesAsync(name, edits, cancellationToken);

        var added = new SyntaxAnnotation();
        var index = InsertionIndex(declaration, model);
        edits.Replace(document.Id, declaration, current =>
        {
            var type = (TypeDeclarationSyntax)current;
            return type.WithMembers(ForwardingMembers.Insert(type.Members, index, forwarding.WithAdditionalAnnotations(added)));
        });
        var updated = await edits.ApplyAsync(_solution, cancellationToken);
        updated = await WithNewModifierIfHidingAsync(updated, document.Id, added, cancellationToken);
        return await KeepMeaningsAsync(updated, references, member, added, cancellationToken);
    }

    private static bool SameParameterTypes(IMethodSymbol first, IMethodSymbol second) =>
        first.Parameters.Select(p => p.Type).SequenceEqual(second.Parameters.Select(p => p.Type), SymbolEqualityComparer.Default);

    private static string DisplayName(ISymbol member) => member is IPropertySymbol { IsIndexer: true } ? "this" : member.Name;

    /// <summary>A property or field is forwarded with a setter when the class could set it.</summary>
    private static bool Settable(ISymbol member) => member switch
    {
        IPropertySymbol property => property.SetMethod is { IsInitOnly: false } setter
            && setter.DeclaredAccessibility == property.DeclaredAccessibility,
        IFieldSymbol field => !field.IsReadOnly && !field.IsConst,
        _ => false,
    };

    /// <summary>
    /// After the class's fields and the members that already forward through
    /// the same field or base, or first when there are none.
    /// </summary>
    private int InsertionIndex(TypeDeclarationSyntax declaration, SemanticModel model)
    {
        var members = declaration.Members;
        var index = 0;
        for (var i = 0; i < members.Count; i++)
        {
            if (members[i] is FieldDeclarationSyntax)
                index = i + 1;
        }

        while (index < members.Count && ForwardingMembers.Forwarded(members[index], model, IsReceiver(model)) is not null)
            index++;
        return index;
    }

    private Func<ExpressionSyntax, bool> IsReceiver(SemanticModel model) => expression => _field is null
        ? expression is BaseExpressionSyntax
        : SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(expression).Symbol, _field);

    private sealed record Reference(DocumentId Document, SyntaxAnnotation Mark, string? Meaning, bool InsideOnThis, bool Inherited, string Where);

    /// <summary>
    /// Marks every reference to a member of the class, or one it inherits,
    /// with the same name as the new member.
    /// </summary>
    private async Task<List<Reference>> ReferencesAsync(string name, SyntaxEdits edits, CancellationToken cancellationToken)
    {
        var symbols = new List<ISymbol>();
        for (var current = _type; current is not null; current = current.BaseType)
            symbols.AddRange(current.GetMembers(name).Where(m => !m.IsImplicitlyDeclared));

        var found = new List<Reference>();
        foreach (var symbol in symbols)
        {
            foreach (var location in (await SymbolFinder.FindReferencesAsync(symbol, _solution, cancellationToken)).SelectMany(r => r.Locations))
            {
                if (location.IsImplicit || !location.Location.IsInSource)
                    continue;

                var root = (await location.Document.GetSyntaxRootAsync(cancellationToken))!;
                var model = (await location.Document.GetSemanticModelAsync(cancellationToken))!;
                var callee = ForwardingMembers.Callee(root.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true), symbol);
                if (callee is null)
                    continue;

                var receiver = ForwardingMembers.Receiver(callee);
                var enclosing = callee.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
                var inside = enclosing is not null && SymbolEqualityComparer.Default.Equals(model.GetDeclaredSymbol(enclosing, cancellationToken), _type);
                var mark = new SyntaxAnnotation();
                edits.Replace(location.Document.Id, callee, current => current.WithAdditionalAnnotations(mark));
                found.Add(new Reference(
                    location.Document.Id,
                    mark,
                    Meaning(model.GetSymbolInfo(callee, cancellationToken).Symbol),
                    inside && callee is not MemberBindingExpressionSyntax && receiver is null or ThisExpressionSyntax,
                    !SymbolEqualityComparer.Default.Equals(symbol.ContainingType, _type),
                    SolutionEdits.Describe(callee.GetLocation())));
            }
        }

        return found;
    }

    private static string? Meaning(ISymbol? symbol) => symbol?.OriginalDefinition.GetDocumentationCommentId();

    /// <summary>A member that hides an inherited one says so with <c>new</c>.</summary>
    private static async Task<Solution> WithNewModifierIfHidingAsync(Solution solution, DocumentId id, SyntaxAnnotation added, CancellationToken cancellationToken)
    {
        var document = solution.GetDocument(id)!;
        var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
        var model = (await document.GetSemanticModelAsync(cancellationToken))!;
        var member = (MemberDeclarationSyntax)root.GetAnnotatedNodes(added).Single();
        var hides = model.GetDiagnostics(member.Span, cancellationToken).Any(d => d.Id is "CS0108" or "CS0114");
        if (!hides)
            return solution;

        var modifiers = member.Modifiers.Add(Token(SyntaxKind.NewKeyword).WithTrailingTrivia(Space));
        var last = modifiers[^2];
        modifiers = modifiers.Replace(last, last.WithTrailingTrivia(Space));
        return document.WithSyntaxRoot(root.ReplaceNode(member, member.WithModifiers(modifiers))).Project.Solution;
    }

    /// <summary>
    /// Every marked reference must mean what it meant before. The class's own
    /// uses of an inherited member the new one hides are written with
    /// <c>base</c>; elsewhere, only a member that forwards to the base class
    /// may take the place of the very member it forwards to.
    /// </summary>
    private async Task<Solution> KeepMeaningsAsync(Solution solution, List<Reference> references, ISymbol member, SyntaxAnnotation added, CancellationToken cancellationToken)
    {
        var edits = new SyntaxEdits();
        foreach (var reference in references)
        {
            var document = solution.GetDocument(reference.Document)!;
            var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
            var model = (await document.GetSemanticModelAsync(cancellationToken))!;
            var callee = (ExpressionSyntax)root.GetAnnotatedNodes(reference.Mark).Single();
            if (callee.Ancestors().Any(a => a.HasAnnotation(added)))
                continue;

            var now = Meaning(model.GetSymbolInfo(callee, cancellationToken).Symbol);
            if (now == reference.Meaning)
                continue;

            if (reference.InsideOnThis && reference.Inherited)
            {
                edits.Replace(reference.Document, callee, current => ForwardingMembers.Through((ExpressionSyntax)current, BaseExpression()));
                continue;
            }

            if (_field is null && reference.Meaning == Meaning(member))
                continue;

            throw new McpException($"Error: Adding {_type.Name}.{DisplayName(member)} would change what {reference.Where} refers to");
        }

        return await edits.ApplyAsync(solution, cancellationToken);
    }
}
