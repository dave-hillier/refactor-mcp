using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using static RefactorMCP.Tests.Catalog.CatalogMapping;

namespace RefactorMCP.Tests.Catalog.Mappings;

/// <summary>
/// Primitives that let the delegation and inheritance composites run as
/// recipes: adding and removing forwarding members, and moving a class's use
/// of its base class onto a field and back.
/// </summary>
internal sealed class RecipePrimitivesMappings : ICatalogMappings
{
    public IEnumerable<CatalogMapping> Mappings => new[]
    {
        new CatalogMapping(
            "add-delegating-member",
            "add-delegating-member",
            async context =>
            {
                var arguments = await ClassArguments(context);
                arguments["memberName"] = context.RequiredArgument("member");
                arguments["via"] = context.RequiredArgument("via");
                return arguments;
            },
            Codes(
                ("not-a-class", "is not a class or struct"),
                ("field-not-found", "has no field named"),
                ("no-base-class", "has no base class to forward to"),
                ("member-not-found", "can reach"),
                ("ambiguous-member", "name the one to forward by its documentation comment id"),
                ("unsupported-member", "cannot be forwarded"),
                ("name-conflict", "already has a member named"),
                ("changes-meaning", "would change what"),
                ("breaks-compilation", "would not compile"))),

        new CatalogMapping(
            "replace-base-uses-with-field",
            "replace-base-uses-with-field",
            FieldArguments,
            Codes(
                ("not-a-class", "is not a class"),
                ("no-base-class", "has no base class other than object"),
                ("field-not-found", "has no field named"),
                ("field-type-not-base", "not of the base class"),
                ("field-not-private", "is not a private instance field"),
                ("not-created-by-field", "is not initialised with a new"),
                ("field-in-use", "is already used at"),
                ("base-constructed-with-arguments", "passes arguments to the constructor of"),
                ("overrides-base-member", "overrides a member of"),
                ("uses-protected-member", "uses the protected member"),
                ("base-members-in-use", "uses the inherited member"),
                ("base-conversion-in-use", "which reaches its base class part"))),

        new CatalogMapping(
            "replace-field-uses-with-base",
            "replace-field-uses-with-base",
            FieldArguments,
            Codes(
                ("not-a-class", "is not a class"),
                ("no-base-class", "has no base class other than object"),
                ("field-not-found", "has no field named"),
                ("field-type-not-base", "not of the base class"),
                ("field-not-private", "is not a private instance field"),
                ("not-created-by-field", "is not initialised with a new"),
                ("base-constructed-with-arguments", "passes arguments to the constructor of"),
                ("overrides-base-member", "overrides a member of"),
                ("field-assigned", "is assigned at"),
                ("field-escapes", "is used other than through its members"),
                ("base-members-in-use", "uses the inherited member"),
                ("base-conversion-in-use", "which reaches its base class part"))),

        new CatalogMapping(
            "remove-delegating-member",
            "remove-delegating-member",
            async context =>
            {
                var location = await context.SymbolLocationAsync();
                return new Dictionary<string, JsonElement>
                {
                    ["solutionPath"] = Json(context.SolutionPath),
                    ["filePath"] = Json(location.FilePath),
                    ["className"] = Json(location.Symbol.ContainingType.Name),
                    ["memberName"] = Json(location.Symbol.Name),
                    ["line"] = Json(location.Line),
                };
            },
            Codes(
                ("not-delegating", "does not only forward"),
                ("signature-differs", "signature differs"),
                ("inherited-less-accessible", "is less accessible than"),
                ("member-overridden", "is overridden by"))),
    };

    private static async Task<Dictionary<string, JsonElement>> FieldArguments(StepContext context)
    {
        var arguments = await ClassArguments(context);
        arguments["fieldName"] = context.RequiredArgument("field");
        return arguments;
    }

    private static async Task<Dictionary<string, JsonElement>> ClassArguments(StepContext context)
    {
        var location = await context.SymbolLocationAsync();
        return new Dictionary<string, JsonElement>
        {
            ["solutionPath"] = Json(context.SolutionPath),
            ["filePath"] = Json(location.FilePath),
            ["className"] = Json(location.Symbol.Name),
        };
    }

    private static IReadOnlyDictionary<string, string> Codes(params (string Code, string Fragment)[] codes)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (code, fragment) in codes)
            map[code] = fragment;
        return map;
    }
}
