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
        new CatalogMapping(
            "consolidate-conditional-expression",
            "consolidate-conditional-expression",
            context =>
            {
                var arguments = CaretArguments(context);
                if (context.HasArgument("name"))
                    arguments["methodName"] = context.RequiredArgument("name");
                return arguments;
            },
            Codes(
                ("not-an-if", "is not on an if statement"),
                ("nothing-to-consolidate", "has nothing to consolidate"),
                ("body-falls-through", "share a body that can fall through"),
                ("declares-variable", "declares"),
                ("invalid-name", "is not a valid method name"),
                ("name-conflict", "already has a member named"))),
        new CatalogMapping(
            "consolidate-duplicate-conditional-fragments",
            "consolidate-duplicate-conditional-fragments",
            CaretArguments,
            Codes(
                ("not-an-if", "is not on an if statement"),
                ("not-in-block", "is not in a block"),
                ("no-final-else", "has no final else"),
                ("no-common-fragments", "no common fragments to move"),
                ("uses-branch-local", "its branch declares"),
                ("condition-depends-on-fragment", "The conditions depend on"))),
        new CatalogMapping(
            "remove-middle-man",
            "remove-middle-man",
            async context =>
            {
                var arguments = await TypeArguments(context);
                arguments["via"] = context.RequiredArgument("via");
                return arguments;
            },
            Codes(
                ("via-not-found", "to delegate through"),
                ("no-delegating-members", "has no method or property that only delegates"),
                ("via-not-accessible", "make the delegate accessible first"),
                ("method-group-reference", "is used without being called"))),
        new CatalogMapping(
            "inline-class",
            "inline-class",
            TypeArguments,
            Codes(
                ("unsupported-class", "cannot be inlined because it"),
                ("type-referenced", "would be left naming a class that no longer exists"),
                ("no-holder", "No field or property holds an instance"),
                ("several-holders", "is held by several fields or properties"),
                ("holder-not-created", "creates the object in its initializer"),
                ("member-exists", "already has a member named"),
                ("holder-escapes", "other than to reach a member of"))),
    };

    /// <summary>The file declaring the <c>target.symbol</c> type, and its name.</summary>
    private static async Task<Dictionary<string, JsonElement>> TypeArguments(StepContext context)
    {
        var location = await context.SymbolLocationAsync();
        return new Dictionary<string, JsonElement>
        {
            ["solutionPath"] = Json(context.SolutionPath),
            ["filePath"] = Json(location.FilePath),
            ["className"] = Json(location.Symbol.Name),
        };
    }

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
