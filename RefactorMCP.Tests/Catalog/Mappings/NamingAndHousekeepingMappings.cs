using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using static RefactorMCP.Tests.Catalog.CatalogMapping;

namespace RefactorMCP.Tests.Catalog.Mappings;

/// <summary>Primitives from the catalog's "Naming and housekeeping" group.</summary>
internal sealed class NamingAndHousekeepingMappings : ICatalogMappings
{
    public IEnumerable<CatalogMapping> Mappings => new[]
    {
        new CatalogMapping("rename", "rename-symbol", RenameArguments, NoErrorCodes),
    };

    private static async Task<Dictionary<string, JsonElement>> RenameArguments(StepContext context)
    {
        if (context.Step.Target?.Caret is not null)
        {
            var (line, column) = context.Caret();
            return new Dictionary<string, JsonElement>
            {
                ["solutionPath"] = Json(context.SolutionPath),
                ["filePath"] = Json(context.TargetFilePath()),
                ["oldName"] = Json(context.TokenAtCaret()),
                ["newName"] = context.RequiredArgument("name"),
                ["line"] = Json(line),
                ["column"] = Json(column),
            };
        }

        var location = await context.SymbolLocationAsync();
        return new Dictionary<string, JsonElement>
        {
            ["solutionPath"] = Json(context.SolutionPath),
            ["filePath"] = Json(location.FilePath),
            ["oldName"] = Json(location.Symbol.Name),
            ["newName"] = context.RequiredArgument("name"),
            ["line"] = Json(location.Line),
            ["column"] = Json(location.Column),
        };
    }
}
