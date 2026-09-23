using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using static RefactorMCP.Tests.Catalog.CatalogMapping;

namespace RefactorMCP.Tests.Catalog.Mappings;

/// <summary>Generators from the catalog that replace a type code: with an enum, with subclasses, and the conditionals on it with polymorphism.</summary>
internal sealed class GeneratorsTypeCodeMappings : ICatalogMappings
{
    public IEnumerable<CatalogMapping> Mappings => new[]
    {
        new CatalogMapping(
            "replace-type-code-with-enum",
            "replace-type-code-with-enum",
            async context =>
            {
                var location = await context.SymbolLocationAsync();
                return new Dictionary<string, JsonElement>
                {
                    ["solutionPath"] = Json(context.SolutionPath),
                    ["filePath"] = Json(location.FilePath),
                    ["typeName"] = Json(location.Symbol.Name),
                    ["constantNames"] = context.RequiredArgument("constants"),
                    ["enumName"] = context.RequiredArgument("name"),
                };
            },
            Codes(
                ("constant-not-found", "has no constant named"),
                ("mixed-types", "mix int and string"),
                ("not-a-type-code", "is not an int or string constant"),
                ("type-already-exists", "already exists"),
                ("breaks-compilation", "would not compile"))),
    };

    private static IReadOnlyDictionary<string, string> Codes(params (string Code, string Fragment)[] codes) =>
        codes.ToDictionary(c => c.Code, c => c.Fragment, StringComparer.Ordinal);
}
