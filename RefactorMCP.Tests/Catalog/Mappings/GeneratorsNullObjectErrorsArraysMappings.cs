using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using static RefactorMCP.Tests.Catalog.CatalogMapping;

namespace RefactorMCP.Tests.Catalog.Mappings;

/// <summary>Generators from the catalog: Introduce Null Object, Replace Error Code with Exception and Replace Array with Object.</summary>
internal sealed class GeneratorsNullObjectErrorsArraysMappings : ICatalogMappings
{
    public IEnumerable<CatalogMapping> Mappings => new[]
    {
        new CatalogMapping(
            "introduce-null-object",
            "introduce-null-object",
            async context =>
            {
                var location = await context.SymbolLocationAsync();
                return new Dictionary<string, JsonElement>
                {
                    ["solutionPath"] = Json(context.SolutionPath),
                    ["filePath"] = Json(location.FilePath),
                    ["fieldName"] = Json(location.Symbol.Name),
                };
            },
            Codes(
                ("not-an-interface", "which is not an interface"),
                ("no-null-checks", "is never checked for null"),
                ("unsupported-null-check", "is not one a null object can replace"),
                ("inconsistent-fallbacks", "fall back to different values"),
                ("type-already-exists", "already exists"),
                ("breaks-compilation", "would break the build"))),
    };

    private static IReadOnlyDictionary<string, string> Codes(params (string Code, string Fragment)[] codes)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (code, fragment) in codes)
            map[code] = fragment;
        return map;
    }
}
