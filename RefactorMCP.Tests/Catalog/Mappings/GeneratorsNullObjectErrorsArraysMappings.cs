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

        new CatalogMapping(
            "replace-error-code-with-exception",
            "replace-error-code-with-exception",
            async context =>
            {
                var location = await context.SymbolLocationAsync();
                var arguments = new Dictionary<string, JsonElement>
                {
                    ["solutionPath"] = Json(context.SolutionPath),
                    ["filePath"] = Json(location.FilePath),
                    ["methodName"] = Json(location.Symbol.Name),
                    ["line"] = Json(location.Line),
                };
                if (context.HasArgument("exception"))
                    arguments["exceptionType"] = context.RequiredArgument("exception");
                return arguments;
            },
            Codes(
                ("in-hierarchy", "is virtual, abstract, an override or an interface implementation"),
                ("unsupported-return-type", "only an int or bool error code can be replaced"),
                ("non-constant-return", "which is not a constant error code"),
                ("no-error-code", "never returns an error code"),
                ("exception-type-not-found", "No exception type named"),
                ("unsupported-caller", "in a way a catch cannot replace"),
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
