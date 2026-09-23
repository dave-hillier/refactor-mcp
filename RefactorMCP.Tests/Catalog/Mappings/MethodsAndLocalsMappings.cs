using System;
using System.Collections.Generic;
using System.Text.Json;
using static RefactorMCP.Tests.Catalog.CatalogMapping;

namespace RefactorMCP.Tests.Catalog.Mappings;

/// <summary>Primitives from the catalog's "Methods and locals" group.</summary>
internal sealed class MethodsAndLocalsMappings : ICatalogMappings
{
    public IEnumerable<CatalogMapping> Mappings => new[]
    {
        new CatalogMapping(
            "extract-method",
            "extract-method",
            context => new Dictionary<string, JsonElement>
            {
                ["solutionPath"] = Json(context.SolutionPath),
                ["filePath"] = Json(context.TargetFilePath()),
                ["selectionRange"] = Json(context.SelectionRange()),
                ["methodName"] = context.RequiredArgument("name"),
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["expression-bodied-member"] = "Extraction from expression-bodied methods is not supported",
                ["declared-local-used-after"] = "The extracted block declares",
                ["not-in-method"] = "Selected code is not within a method",
                ["no-statements-selected"] = "does not contain extractable statements",
            }),
    };
}
