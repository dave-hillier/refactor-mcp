using System;
using System.Collections.Generic;
using System.Text.Json;
using static RefactorMCP.Tests.Catalog.CatalogMapping;

namespace RefactorMCP.Tests.Catalog.Mappings;

/// <summary>Primitives from the catalog's "Loops and expressions" group.</summary>
internal sealed class LoopsAndExpressionsMappings : ICatalogMappings
{
    public IEnumerable<CatalogMapping> Mappings => new[]
    {
        new CatalogMapping(
            "convert-for-to-foreach",
            "convert-for-to-foreach",
            CaretArguments,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["not-a-for-loop"] = "is not in a for loop",
                ["unsupported-loop-shape"] = "does not count an index up from 0",
                ["index-used-otherwise"] = "is used other than to read",
                ["collection-modified"] = "is modified in the loop",
                ["not-enumerable"] = "cannot be enumerated with foreach",
                ["name-conflict"] = "is already declared",
            }),
        new CatalogMapping(
            "convert-foreach-to-for",
            "convert-foreach-to-for",
            CaretArguments,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["not-a-foreach-loop"] = "is not in a foreach loop",
                ["not-indexable"] = "cannot be indexed",
                ["collection-not-simple"] = "would be evaluated on every iteration",
                ["name-conflict"] = "is already declared",
            }),
        new CatalogMapping(
            "convert-foreach-to-linq",
            "convert-foreach-to-linq",
            CaretArguments,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["not-a-foreach-loop"] = "is not in a foreach loop",
                ["unsupported-loop-body"] = "is not a filter, projection or aggregation",
                ["side-effects"] = "has side effects",
                ["early-exit"] = "leaves the loop early",
                ["accumulator-used-in-body"] = "reads the accumulator",
                ["not-queryable"] = "is not a generic sequence",
            }),
        new CatalogMapping(
            "convert-linq-to-foreach",
            "convert-linq-to-foreach",
            CaretArguments,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["not-a-query"] = "is not a LINQ query",
                ["unsupported-query"] = "cannot be written as a loop",
                ["unsupported-statement"] = "is not assigned to a local or returned",
                ["name-conflict"] = "is already declared",
            }),
        new CatalogMapping(
            "convert-concatenation-to-interpolation",
            "convert-concatenation-to-interpolation",
            CaretArguments,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["not-a-concatenation"] = "is not a string concatenation",
                ["numeric-addition"] = "adds numbers",
                ["contains-comments"] = "has comments between its operands",
            }),
        new CatalogMapping(
            "introduce-using-declaration",
            "introduce-using-declaration",
            CaretArguments,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["not-a-using-statement"] = "is not on a using statement",
                ["no-variable"] = "declares no variable",
                ["not-last-statement"] = "is not the last statement of its block",
                ["in-switch-section"] = "is directly in a switch section",
                ["not-in-block"] = "is not in a block",
                ["name-conflict"] = "is already declared",
                ["language-version"] = "needs C# 8",
            }),
    };

    /// <summary>These refactorings act on the statement or expression under a caret, with an optional name.</summary>
    private static Dictionary<string, JsonElement> CaretArguments(StepContext context)
    {
        var (line, column) = context.Caret();
        var arguments = new Dictionary<string, JsonElement>
        {
            ["solutionPath"] = Json(context.SolutionPath),
            ["filePath"] = Json(context.TargetFilePath()),
            ["line"] = Json(line),
            ["column"] = Json(column),
        };
        if (context.HasArgument("name"))
            arguments["name"] = context.RequiredArgument("name");
        return arguments;
    }
}
