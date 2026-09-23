using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using static RefactorMCP.Tests.Catalog.CatalogMapping;

namespace RefactorMCP.Tests.Catalog.Mappings;

/// <summary>
/// Primitives that give composite recipes a step they had no primitive for:
/// removing an else after a branch that jumps away, and joining ifs that share
/// a body.
/// </summary>
internal sealed class RecipeGapsMappings : ICatalogMappings
{
    public IEnumerable<CatalogMapping> Mappings => new[]
    {
        new CatalogMapping(
            "remove-redundant-else",
            "remove-redundant-else",
            CaretArguments,
            Codes(
                ("not-an-if", "is not on an if statement"),
                ("no-else", "has no else to remove"),
                ("not-in-block", "is not in a block"),
                ("branch-falls-through", "can run on past its end"),
                ("name-conflict", "which is also declared elsewhere"))),
        new CatalogMapping(
            "merge-sibling-ifs",
            "merge-sibling-ifs",
            CaretArguments,
            Codes(
                ("not-an-if", "is not on an if statement"),
                ("no-sibling-if", "is not followed by an else if or an if statement with the same body"),
                ("body-falls-through", "share a body that can fall through"),
                ("declares-variable", "A condition declares a variable"))),
    };

    /// <summary>The file and the 1-based position of the caret, for tools that act on the statement under it.</summary>
    private static Dictionary<string, JsonElement> CaretArguments(StepContext context)
    {
        var (line, column) = context.Caret();
        return new Dictionary<string, JsonElement>
        {
            ["solutionPath"] = Json(context.SolutionPath),
            ["filePath"] = Json(context.TargetFilePath()),
            ["line"] = Json(line),
            ["column"] = Json(column),
        };
    }

    private static IReadOnlyDictionary<string, string> Codes(params (string Code, string Fragment)[] codes) =>
        codes.ToDictionary(c => c.Code, c => c.Fragment);
}
