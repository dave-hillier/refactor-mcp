using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using static RefactorMCP.Tests.Catalog.CatalogMapping;

namespace RefactorMCP.Tests.Catalog.Mappings;

/// <summary>Primitives from the catalog's "Fields, properties and constants" group.</summary>
internal sealed class FieldsPropertiesAndConstantsMappings : ICatalogMappings
{
    public IEnumerable<CatalogMapping> Mappings => new[]
    {
        new CatalogMapping(
            "introduce-field",
            "introduce-field",
            IntroduceFieldArguments,
            Codes(
                ("not-an-expression", "The selection is not an expression"),
                ("no-value", "has no value"),
                ("name-conflict", "already has a member named"),
                ("conditionally-evaluated", "may not be evaluated every time"),
                ("expression-bodied-member", "expression-bodied member"),
                ("method-type-parameter", "type parameter of the method"),
                ("not-in-block", "is not in a block"),
                ("assigned-expression", "is assigned to"),
                ("unknown-type", "No type named"))),
    };

    /// <summary>
    /// A selection introduces a field from the selected expression. A symbol
    /// target naming a type, with a <c>type</c> argument, adds a field of that
    /// type to it; the tool finds the type from a selection of its name.
    /// </summary>
    private static async Task<Dictionary<string, JsonElement>> IntroduceFieldArguments(StepContext context)
    {
        if (context.Step.Target?.Symbol is null)
        {
            return new Dictionary<string, JsonElement>
            {
                ["solutionPath"] = Json(context.SolutionPath),
                ["filePath"] = Json(context.TargetFilePath()),
                ["selectionRange"] = Json(context.SelectionRange()),
                ["fieldName"] = context.RequiredArgument("name"),
            };
        }

        var location = await context.SymbolLocationAsync();
        return new Dictionary<string, JsonElement>
        {
            ["solutionPath"] = Json(context.SolutionPath),
            ["filePath"] = Json(location.FilePath),
            ["selectionRange"] = Json(NameRange(location)),
            ["fieldName"] = context.RequiredArgument("name"),
            ["fieldType"] = context.RequiredArgument("type"),
        };
    }

    /// <summary>A symbol-targeted step as the tools that take a member name expect it.</summary>
    private static async Task<Dictionary<string, JsonElement>> MemberArguments(StepContext context, string memberParameter)
    {
        var location = await context.SymbolLocationAsync();
        return new Dictionary<string, JsonElement>
        {
            ["solutionPath"] = Json(context.SolutionPath),
            ["filePath"] = Json(location.FilePath),
            [memberParameter] = Json(location.Symbol.Name),
        };
    }

    private static string NameRange(SymbolLocation location) =>
        $"{location.Line}:{location.Column}-{location.Line}:{location.Column + location.Symbol.Name.Length}";

    private static IReadOnlyDictionary<string, string> Codes(params (string Code, string Fragment)[] codes)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (code, fragment) in codes)
            map[code] = fragment;
        return map;
    }
}
