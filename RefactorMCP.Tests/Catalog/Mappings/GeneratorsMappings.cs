using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using static RefactorMCP.Tests.Catalog.CatalogMapping;

namespace RefactorMCP.Tests.Catalog.Mappings;

/// <summary>The catalog's generators: refactorings that add structure or change behaviour.</summary>
internal sealed class GeneratorsMappings : ICatalogMappings
{
    public IEnumerable<CatalogMapping> Mappings => new[]
    {
        new CatalogMapping(
            "add-null-checks",
            "add-null-checks",
            MethodArguments,
            Codes(
                ("no-body", "has no body to guard"),
                ("nothing-to-guard", "has no reference-type parameter left to guard"))),

        new CatalogMapping(
            "add-observer",
            "add-observer",
            async context =>
            {
                var arguments = await MethodArguments(context);
                arguments["className"] = Json((await context.SymbolAsync()).ContainingType.Name);
                arguments["eventName"] = context.RequiredArgument("event");
                return arguments;
            },
            Codes(
                ("no-body", "has no body to raise the event from"),
                ("not-void", "returns a value"),
                ("ref-parameter", "has a ref, out or in parameter"),
                ("member-exists", "already has a member named"))),
    };

    /// <summary>
    /// The file, name and line of the method <c>target.symbol</c> names. A
    /// constructor is named by its type.
    /// </summary>
    private static async Task<Dictionary<string, JsonElement>> MethodArguments(StepContext context)
    {
        var location = await context.SymbolLocationAsync();
        var name = location.Symbol is IMethodSymbol { MethodKind: MethodKind.Constructor } constructor
            ? constructor.ContainingType.Name
            : location.Symbol.Name;
        return new Dictionary<string, JsonElement>
        {
            ["solutionPath"] = Json(context.SolutionPath),
            ["filePath"] = Json(location.FilePath),
            ["methodName"] = Json(name),
            ["line"] = Json(location.Line),
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
