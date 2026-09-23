using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using static RefactorMCP.Tests.Catalog.CatalogMapping;

namespace RefactorMCP.Tests.Catalog.Mappings;

/// <summary>
/// Composites from the catalog that rework conditionals or remove a class or
/// a layer of delegation, each run by a dedicated tool.
/// </summary>
internal sealed class ConditionalAndRemovalCompositesMappings : ICatalogMappings
{
    public IEnumerable<CatalogMapping> Mappings => new[]
    {
        new CatalogMapping(
            "convert-if-to-switch-expression",
            "convert-if-to-switch-expression",
            CaretArguments,
            Codes(
                ("not-an-if", "is not on an if statement"),
                ("too-few-cases", "at least two cases"),
                ("different-values", "do not all compare the same value"),
                ("unsupported-condition", "cannot become a case label"),
                ("side-effects", "has side effects"),
                ("contains-break", "contains a break"),
                ("unsupported-section", "cannot become an arm"),
                ("different-targets", "assign different variables"),
                ("no-default", "has no default"))),
        new CatalogMapping(
            "replace-nested-conditional-with-guard-clauses",
            "replace-nested-conditional-with-guard-clauses",
            CaretArguments,
            Codes(
                ("not-an-if", "is not on an if statement"),
                ("not-in-block", "is not in a block"),
                ("no-guard-clause", "has no guard clause to become"))),
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

    private static IReadOnlyDictionary<string, string> Codes(params (string Code, string Fragment)[] codes)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (code, fragment) in codes)
            map[code] = fragment;
        return map;
    }
}
