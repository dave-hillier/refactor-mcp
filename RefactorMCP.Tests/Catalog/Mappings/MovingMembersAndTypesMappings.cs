using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using static RefactorMCP.Tests.Catalog.CatalogMapping;

namespace RefactorMCP.Tests.Catalog.Mappings;

/// <summary>Primitives from the catalog's "Moving members and types" group.</summary>
internal sealed class MovingMembersAndTypesMappings : ICatalogMappings
{
    public IEnumerable<CatalogMapping> Mappings => new[]
    {
        new CatalogMapping(
            "rename-file-to-match-type",
            "rename-file-to-match-type",
            context => new Dictionary<string, JsonElement>
            {
                ["solutionPath"] = Json(context.SolutionPath),
                ["filePath"] = Json(context.TargetFilePath()),
            },
            Codes(
                ("multiple-types", "declares more than one top-level type"),
                ("no-type", "declares no top-level type"),
                ("name-already-matches", "The file name already matches"),
                ("file-exists", "already exists"))),

        new CatalogMapping(
            "move-type-to-file",
            "move-to-separate-file",
            async context => new Dictionary<string, JsonElement>
            {
                ["solutionPath"] = Json(context.SolutionPath),
                ["filePath"] = Json(await context.SymbolFilePathAsync()),
                ["typeName"] = Json((await context.SymbolAsync()).Name),
            },
            Codes(
                ("only-type-in-file", "is the only type in"),
                ("file-exists", "already exists"),
                ("nested-type", "is nested in another type"))),
    };

    private static IReadOnlyDictionary<string, string> Codes(params (string Code, string Fragment)[] codes)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (code, fragment) in codes)
            map[code] = fragment;
        return map;
    }
}
