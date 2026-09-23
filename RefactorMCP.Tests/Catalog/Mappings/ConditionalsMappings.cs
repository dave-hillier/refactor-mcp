using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using static RefactorMCP.Tests.Catalog.CatalogMapping;

namespace RefactorMCP.Tests.Catalog.Mappings;

/// <summary>Primitives from the catalog's "Conditionals" group.</summary>
internal sealed class ConditionalsMappings : ICatalogMappings
{
    public IEnumerable<CatalogMapping> Mappings => new[]
    {
        new CatalogMapping(
            "invert-if",
            "invert-if",
            CaretArguments,
            Codes(
                ("not-an-if", "is not on an if statement"),
                ("code-follows", "is followed by code"),
                ("no-implicit-exit", "no implicit exit"))),
        new CatalogMapping(
            "merge-nested-if",
            "merge-nested-if",
            CaretArguments,
            Codes(
                ("not-an-if", "is not on an if statement"),
                ("outer-has-else", "The outer if has an else"),
                ("inner-has-else", "The inner if has an else"),
                ("not-only-statement", "does more than the inner if"),
                ("no-inner-if", "does not contain an if statement"))),
        new CatalogMapping(
            "split-if",
            "split-if",
            CaretArguments,
            Codes(
                ("not-an-if", "is not on an if statement"),
                ("not-splittable", "is not joined by && or ||"),
                ("and-with-else", "splits on && and has an else"))),
        new CatalogMapping(
            "convert-if-chain-to-switch",
            "convert-if-chain-to-switch",
            CaretArguments,
            Codes(
                ("not-an-if", "is not on an if statement"),
                ("too-few-cases", "at least two cases"),
                ("different-values", "do not all compare the same value"),
                ("unsupported-condition", "cannot become a case label"),
                ("side-effects", "has side effects"),
                ("contains-break", "contains a break"))),
        new CatalogMapping(
            "convert-switch-statement-to-expression",
            "convert-switch-statement-to-expression",
            CaretArguments,
            Codes(
                ("not-a-switch", "is not on a switch statement"),
                ("unsupported-section", "cannot become an arm"),
                ("different-targets", "assign different variables"),
                ("no-default", "has no default"))),
        new CatalogMapping(
            "convert-switch-expression-to-statement",
            "convert-switch-expression-to-statement",
            CaretArguments,
            Codes(
                ("not-a-switch", "is not on a switch expression"),
                ("unsupported-context", "has no statement to become"),
                ("anonymous-type", "has an anonymous type"))),
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
