using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using static RefactorMCP.Tests.Catalog.CatalogMapping;

namespace RefactorMCP.Tests.Catalog.Mappings;

/// <summary>Generators from the catalog that wrap an object behind an interface: Extract Decorator and Create Adapter.</summary>
internal sealed class GeneratorsWrapperMappings : ICatalogMappings
{
    public IEnumerable<CatalogMapping> Mappings => new[]
    {
        new CatalogMapping(
            "extract-decorator",
            "extract-decorator",
            async context =>
            {
                var location = await context.SymbolLocationAsync();
                var arguments = new Dictionary<string, JsonElement>
                {
                    ["solutionPath"] = Json(context.SolutionPath),
                    ["filePath"] = Json(location.FilePath),
                    ["typeName"] = Json(location.Symbol.Name),
                };
                if (context.HasArgument("name"))
                    arguments["decoratorName"] = context.RequiredArgument("name");
                return arguments;
            },
            Codes(
                ("no-interface", "implements no interface"),
                ("ambiguous-interface", "implements several interfaces"),
                ("type-already-exists", "already exists"))),

        new CatalogMapping(
            "create-adapter",
            "create-adapter",
            async context =>
            {
                var location = await context.SymbolLocationAsync();
                var arguments = new Dictionary<string, JsonElement>
                {
                    ["solutionPath"] = Json(context.SolutionPath),
                    ["filePath"] = Json(location.FilePath),
                    ["className"] = Json(location.Symbol.Name),
                    ["interfaceName"] = context.RequiredArgument("interface"),
                    ["adapterName"] = context.RequiredArgument("name"),
                };
                if (context.HasArgument("map"))
                {
                    var map = context.RequiredArgument("map").EnumerateObject()
                        .Select(entry => $"{entry.Name}:{entry.Value.GetString()}");
                    arguments["memberMap"] = Json(string.Join(",", map));
                }

                return arguments;
            },
            Codes(
                ("interface-not-found", "No type named"),
                ("not-an-interface", "is not an interface"),
                ("member-not-found", "has no member named"),
                ("incompatible-signature", "cannot stand in for"),
                ("type-already-exists", "already exists"))),
    };

    private static IReadOnlyDictionary<string, string> Codes(params (string Code, string Fragment)[] codes) =>
        codes.ToDictionary(c => c.Code, c => c.Fragment, StringComparer.Ordinal);
}
