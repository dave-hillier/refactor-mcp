using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using static RefactorMCP.Tests.Catalog.CatalogMapping;

namespace RefactorMCP.Tests.Catalog.Mappings;

/// <summary>Primitives from the catalog's "Types and hierarchy" group.</summary>
internal sealed class TypesAndHierarchyMappings : ICatalogMappings
{
    public IEnumerable<CatalogMapping> Mappings => new[]
    {
        new CatalogMapping(
            "create-type",
            "create-type",
            context =>
            {
                var arguments = new Dictionary<string, JsonElement>
                {
                    ["solutionPath"] = Json(context.SolutionPath),
                    ["filePath"] = Json(context.WorkspacePath(context.RequiredString("file"))),
                    ["name"] = context.RequiredArgument("name"),
                };
                CopyOptional(context, arguments, "kind", "kind");
                CopyOptional(context, arguments, "namespace", "namespaceName");
                CopyOptional(context, arguments, "baseType", "baseType");
                return arguments;
            },
            Codes(
                ("type-already-exists", "already exists"),
                ("invalid-name", "is not a valid type name"),
                ("invalid-kind", "Unknown kind"),
                ("type-not-found", "No type named"),
                ("invalid-base-type", "cannot be a base type"))),

        new CatalogMapping(
            "change-base-type",
            "change-base-type",
            async context =>
            {
                var location = await context.SymbolLocationAsync();
                var arguments = new Dictionary<string, JsonElement>
                {
                    ["solutionPath"] = Json(context.SolutionPath),
                    ["filePath"] = Json(location.FilePath),
                    ["className"] = Json(location.Symbol.Name),
                };
                CopyOptional(context, arguments, "to", "newBaseType");
                return arguments;
            },
            Codes(
                ("not-a-class", "only a class has a base class"),
                ("no-base-class", "has no base class to remove"),
                ("type-not-found", "No type named"),
                ("invalid-base-type", "cannot be the base class"),
                ("circular-base", "already derives from"),
                ("base-members-in-use", "relies on members of"),
                ("base-conversion-in-use", "converting to"))),
    };

    private static void CopyOptional(
        StepContext context,
        Dictionary<string, JsonElement> arguments,
        string catalogName,
        string toolName)
    {
        if (context.HasArgument(catalogName))
            arguments[toolName] = context.RequiredArgument(catalogName);
    }

    private static IReadOnlyDictionary<string, string> Codes(params (string Code, string Fragment)[] codes)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (code, fragment) in codes)
            map[code] = fragment;
        return map;
    }
}
