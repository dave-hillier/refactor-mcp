using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[McpServerToolType]
public static class CreateAdapterTool
{
    [McpServerTool, Description("Generate an adapter: a class implementing an interface by wrapping an existing class and " +
        "forwarding each interface member to the member it maps to, or to one of the same name, in a new file beside the class")]
    public static async Task<string> CreateAdapter(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the class to adapt")] string filePath,
        [Description("Name of the class to adapt")] string className,
        [Description("The interface the adapter implements, as written in the class's file, such as IRepository<Order>")] string interfaceName,
        [Description("Name of the adapter class to create")] string adapterName,
        [Description("Interface members mapped to the class's members, such as 'Log:Write,Level:Severity' (optional; unmapped members forward to members of the same name)")] string? memberMap = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (solution, document) = await TypeRefactoringHelpers.LoadDocumentAsync(solutionPath, filePath, cancellationToken);
            var (adaptee, declaration) = await TypeRefactoringHelpers.FindTypeAsync(document, className, cancellationToken);
            var model = (await document.GetSemanticModelAsync(cancellationToken))!;

            var @interface = ResolveType(model, declaration.SpanStart, SyntaxFactory.ParseTypeName(interfaceName))
                ?? throw new McpException($"Error: No type named '{interfaceName}' found");
            if (@interface.TypeKind != TypeKind.Interface)
                throw new McpException($"Error: {@interface.ToDisplayString()} is not an interface; an adapter implements an interface");

            var path = InterfaceImplementation.PathFor(document, adapterName);
            InterfaceImplementation.EnsureNameIsFree(adaptee.ContainingNamespace, adapterName, path);

            var counterparts = Counterparts(@interface, adaptee, ParseMap(memberMap), model.Compilation);
            var field = SyntaxFactory.IdentifierName("_adaptee");
            var adapter = InterfaceImplementation.WrapperClass(
                adapterName,
                @interface,
                adaptee,
                "_adaptee",
                "adaptee",
                TypeRefactoringHelpers.EndOfLine((await document.GetSyntaxRootAsync(cancellationToken))!),
                (member, _, part) => counterparts.TryGetValue(member, out var counterpart)
                    ? InterfaceImplementation.Forward(field, counterpart.Name, member, part)
                    : InterfaceImplementation.NotImplemented());

            var updated = await InterfaceImplementation.AddTypeFileAsync(document, adaptee.ContainingNamespace, adapter, cancellationToken);
            await TypeRefactoringHelpers.ApplyIfCompilesAsync(
                solution,
                updated,
                errors => $"Error: The adapter {adapterName} would not compile: {TypeRefactoringHelpers.Describe(errors)}",
                cancellationToken);

            return $"Created adapter {adapterName} implementing {@interface.ToDisplayString()} over {className} in {path}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error creating adapter: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// A type named as the class's file would write it; failing that, a type
    /// anywhere the compilation sees with that name and arity, so the
    /// interface need not be imported there yet.
    /// </summary>
    private static INamedTypeSymbol? ResolveType(SemanticModel model, int position, TypeSyntax syntax)
    {
        if (model.GetSpeculativeTypeInfo(position, syntax, SpeculativeBindingOption.BindAsTypeOrNamespace).Type is INamedTypeSymbol { TypeKind: not TypeKind.Error } bound)
            return bound;

        var name = syntax switch
        {
            GenericNameSyntax generic => generic.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null,
        };
        if (name is null)
            return null;

        var arguments = syntax is GenericNameSyntax g ? g.TypeArgumentList.Arguments : default;
        var candidates = TypesNamed(model.Compilation, name, arguments.Count);
        if (candidates.Count != 1)
            return null;

        if (arguments.Count == 0)
            return candidates[0];

        var resolved = arguments.Select(a => (ITypeSymbol?)ResolveType(model, position, a)).ToList();
        return resolved.All(r => r is not null) ? candidates[0].Construct(resolved.ToArray()!) : null;
    }

    /// <summary>The accessible types with this name and arity, preferring those declared in the solution.</summary>
    private static List<INamedTypeSymbol> TypesNamed(Compilation compilation, string name, int arity)
    {
        var found = new List<INamedTypeSymbol>();
        var pending = new Stack<INamespaceSymbol>();
        pending.Push(compilation.GlobalNamespace);
        while (pending.Count > 0)
        {
            var ns = pending.Pop();
            found.AddRange(ns.GetTypeMembers(name, arity).Where(t => compilation.IsSymbolAccessibleWithin(t, compilation.Assembly)));
            foreach (var child in ns.GetNamespaceMembers())
                pending.Push(child);
        }

        var fromSource = found.Where(t => t.Locations.Any(l => l.IsInSource)).ToList();
        return fromSource.Count > 0 ? fromSource : found;
    }

    private static Dictionary<string, string> ParseMap(string? memberMap)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in (memberMap ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = entry.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
                throw new McpException($"Error: '{entry}' is not a mapping; write it as InterfaceMember:ClassMember");
            map[parts[0]] = parts[1];
        }

        return map;
    }

    /// <summary>
    /// The member of the adaptee each interface member forwards to. A mapped
    /// member must have a compatible counterpart; an unmapped one forwards to
    /// a compatible member of the same name when there is one, and is left
    /// throwing otherwise.
    /// </summary>
    private static Dictionary<ISymbol, ISymbol> Counterparts(
        INamedTypeSymbol @interface,
        INamedTypeSymbol adaptee,
        IReadOnlyDictionary<string, string> map,
        Compilation compilation)
    {
        var members = InterfaceImplementation.MembersToImplement(@interface);
        var unknown = map.Keys.FirstOrDefault(key => members.All(m => m.Name != key));
        if (unknown is not null)
            throw new McpException($"Error: The interface {@interface.Name} has no member named '{unknown}'");

        var counterparts = new Dictionary<ISymbol, ISymbol>(SymbolEqualityComparer.Default);
        foreach (var member in members)
        {
            var isMapped = map.TryGetValue(member.Name, out var targetName);
            var candidates = InstanceMembers(adaptee, isMapped ? targetName! : member.Name, compilation);
            if (isMapped && candidates.Count == 0)
                throw new McpException($"Error: {adaptee.Name} has no member named '{targetName}'");

            var match = candidates.FirstOrDefault(c => Fits(member, c, compilation, identity: true))
                ?? candidates.FirstOrDefault(c => Fits(member, c, compilation, identity: false));
            if (match is not null)
                counterparts[member] = match;
            else if (isMapped)
                throw new McpException(
                    $"Error: {adaptee.Name}.{targetName} cannot stand in for {@interface.Name}.{member.Name}: its signature does not match");
        }

        return counterparts;
    }

    private static List<ISymbol> InstanceMembers(INamedTypeSymbol type, string name, Compilation compilation)
    {
        var members = new List<ISymbol>();
        for (var current = type; current is not null; current = current.BaseType)
        {
            members.AddRange(current.GetMembers(name).Where(m =>
                !m.IsStatic
                && m is IMethodSymbol { MethodKind: MethodKind.Ordinary } or IPropertySymbol or IEventSymbol
                && m.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal
                && compilation.IsSymbolAccessibleWithin(m, compilation.Assembly)));
        }

        return members;
    }

    /// <summary>
    /// Whether <paramref name="candidate"/> can implement <paramref name="member"/>:
    /// the interface's arguments convert to its parameters, and its result
    /// converts to the interface's, exactly when <paramref name="identity"/>
    /// is set and implicitly otherwise.
    /// </summary>
    private static bool Fits(ISymbol member, ISymbol candidate, Compilation compilation, bool identity)
    {
        bool Converts(ITypeSymbol from, ITypeSymbol to)
        {
            var conversion = compilation.ClassifyConversion(from, to);
            return identity ? conversion.IsIdentity : conversion.IsImplicit;
        }

        bool ParametersFit(IReadOnlyList<IParameterSymbol> ours, IReadOnlyList<IParameterSymbol> theirs) =>
            ours.Count == theirs.Count
            && ours.Zip(theirs).All(p => p.First.RefKind == p.Second.RefKind
                && (p.First.RefKind == RefKind.None ? Converts(p.First.Type, p.Second.Type) : compilation.ClassifyConversion(p.First.Type, p.Second.Type).IsIdentity));

        static bool Usable(IMethodSymbol? accessor) =>
            accessor is { DeclaredAccessibility: Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal, IsInitOnly: false };

        switch (member, candidate)
        {
            case (IMethodSymbol method, IMethodSymbol other) when method.Arity == other.Arity:
                var constructed = other.Arity == 0 ? other : other.Construct(method.TypeArguments.ToArray());
                return ParametersFit(method.Parameters, constructed.Parameters)
                    && (method.ReturnsVoid || !constructed.ReturnsVoid && Converts(constructed.ReturnType, method.ReturnType));

            case (IPropertySymbol property, IPropertySymbol other) when property.IsIndexer == other.IsIndexer:
                return (!property.IsIndexer || ParametersFit(property.Parameters, other.Parameters))
                    && (property.GetMethod is null || Usable(other.GetMethod) && Converts(other.Type, property.Type))
                    && (property.SetMethod is null || Usable(other.SetMethod) && Converts(property.Type, other.Type));

            case (IEventSymbol @event, IEventSymbol other):
                return compilation.ClassifyConversion(@event.Type, other.Type).IsIdentity;

            default:
                return false;
        }
    }
}
