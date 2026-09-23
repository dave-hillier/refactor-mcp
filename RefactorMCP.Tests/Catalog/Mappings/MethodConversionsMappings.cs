using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using static RefactorMCP.Tests.Catalog.CatalogMapping;

namespace RefactorMCP.Tests.Catalog.Mappings;

/// <summary>Primitives from the catalog's "Method conversions" group.</summary>
internal sealed class MethodConversionsMappings : ICatalogMappings
{
    public IEnumerable<CatalogMapping> Mappings => new[]
    {
        new CatalogMapping(
            "convert-to-expression-body",
            "convert-to-expression-body",
            PositionArguments,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["not-single-statement"] = "is not a single return, expression or throw statement",
                ["several-accessors"] = "has more than a plain get accessor",
                ["already-expression-bodied"] = "is already expression-bodied",
                ["directive-in-body"] = "contains a preprocessor directive",
                ["no-body"] = "has no body",
                ["not-a-member-with-body"] = "There is no method, property, accessor, constructor, operator or local function",
            }),
        new CatalogMapping(
            "convert-to-block-body",
            "convert-to-block-body",
            PositionArguments,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["already-block-body"] = "already has a block body",
                ["no-body"] = "has no body",
                ["not-a-member-with-body"] = "There is no method, property, accessor, constructor, operator or local function",
            }),
        new CatalogMapping(
            "convert-lambda-to-method-group",
            "convert-lambda-to-method-group",
            PositionArguments,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["not-a-lambda"] = "There is no lambda",
                ["not-a-forwarding-call"] = "is not a single call of a method passing the lambda's parameters",
                ["unstable-receiver"] = "where a method group would evaluate it once",
                ["resolution-changes"] = "would change which method, delegate type or overload is chosen",
            }),
        new CatalogMapping(
            "convert-method-group-to-lambda",
            "convert-method-group-to-lambda",
            PositionArguments,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["not-a-method-group"] = "is not a method group",
                ["unstable-receiver"] = "where a lambda would evaluate it each time it runs",
                ["resolution-changes"] = "would change which method, delegate type or overload is chosen",
            }),
    };

    /// <summary>
    /// Refactorings of a declaration or an expression take a position: the caret, or
    /// the name of the declaration <c>target.symbol</c> identifies.
    /// </summary>
    private static async Task<Dictionary<string, JsonElement>> PositionArguments(StepContext context)
    {
        var (filePath, line, column) = await Position(context);
        return new Dictionary<string, JsonElement>
        {
            ["solutionPath"] = Json(context.SolutionPath),
            ["filePath"] = Json(filePath),
            ["line"] = Json(line),
            ["column"] = Json(column),
        };
    }

    private static async Task<(string FilePath, int Line, int Column)> Position(StepContext context)
    {
        if (context.Step.Target?.Caret is not null)
        {
            var (line, column) = context.Caret();
            return (context.TargetFilePath(), line, column);
        }

        var location = await context.SymbolLocationAsync();
        return (location.FilePath, location.Line, location.Column);
    }
}
