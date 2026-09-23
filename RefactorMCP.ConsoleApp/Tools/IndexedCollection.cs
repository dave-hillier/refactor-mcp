using Microsoft.CodeAnalysis;
using System.Linq;

/// <summary>
/// What the loop conversions need to know about a collection: whether it can be
/// walked by an <c>int</c> index up to its Length or Count, and what a foreach
/// over it yields.
/// </summary>
internal sealed record IndexedCollection(string CountProperty, ITypeSymbol ElementType)
{
    /// <summary>The collection's length property and indexer, or null when it has no such pair.</summary>
    public static IndexedCollection? For(ITypeSymbol type, SemanticModel model, int position)
    {
        if (type is IArrayTypeSymbol array)
            return array.Rank == 1 ? new IndexedCollection("Length", array.ElementType) : null;

        var properties = Members(type)
            .OfType<IPropertySymbol>()
            .Where(p => !p.IsStatic && p.GetMethod is not null && model.IsAccessible(position, p))
            .ToList();
        var indexer = properties.FirstOrDefault(p =>
            p.IsIndexer && p.Parameters.Length == 1 && p.Parameters[0].Type.SpecialType == SpecialType.System_Int32);
        var count = new[] { "Length", "Count" }.FirstOrDefault(name =>
            properties.Any(p => p.Name == name && !p.IsIndexer && p.Type.SpecialType == SpecialType.System_Int32));

        return indexer is null || count is null ? null : new IndexedCollection(count, indexer.Type);
    }

    /// <summary>
    /// The type of the elements a foreach over <paramref name="type"/> yields: the
    /// Current of its GetEnumerator, preferring a generic enumerator to the
    /// non-generic one. Null when the type cannot be enumerated.
    /// </summary>
    public static ITypeSymbol? EnumeratedType(ITypeSymbol type, SemanticModel model, int position)
    {
        if (type is IArrayTypeSymbol array)
            return array.ElementType;

        var currents = Members(type)
            .OfType<IMethodSymbol>()
            .Where(m => m.Name == "GetEnumerator" && m.Parameters.Length == 0 && !m.IsStatic && model.IsAccessible(position, m))
            .Select(m => Members(m.ReturnType)
                .OfType<IPropertySymbol>()
                .FirstOrDefault(p => p.Name == "Current" && !p.IsStatic)?.Type)
            .Where(t => t is not null)
            .ToList();

        return currents.FirstOrDefault(t => t!.SpecialType != SpecialType.System_Object) ?? currents.FirstOrDefault();
    }

    /// <summary>
    /// The members of a type, its base types and, for interfaces and type
    /// parameters, the interfaces it inherits.
    /// </summary>
    private static IEnumerable<ISymbol> Members(ITypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
                yield return member;
        }

        if (type.TypeKind is TypeKind.Interface or TypeKind.TypeParameter)
        {
            foreach (var member in type.AllInterfaces.SelectMany(i => i.GetMembers()))
                yield return member;
        }

        if (type is ITypeParameterSymbol parameter)
        {
            foreach (var member in parameter.ConstraintTypes.SelectMany(Members))
                yield return member;
        }
    }
}
