using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using static RefactorMCP.Tests.Catalog.CatalogMapping;

namespace RefactorMCP.Tests.Catalog.Mappings;

/// <summary>Primitives from the catalog's "Type conversions" group.</summary>
internal sealed class TypeConversionsMappings : ICatalogMappings
{
    public IEnumerable<CatalogMapping> Mappings => new[]
    {
        new CatalogMapping(
            "make-type-partial",
            "make-type-partial",
            context => DeclarationArguments(context, "typeName"),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["already-partial"] = "is already partial",
                ["unsupported-type-kind"] = "which cannot be partial",
            }),
        new CatalogMapping(
            "merge-partial-declarations",
            "merge-partial-declarations",
            ReceivingPartArguments,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["single-declaration"] = "has only one declaration",
                ["conflicting-imports"] = "import conflicting names",
            }),
        new CatalogMapping(
            "convert-record-to-class",
            "convert-record-to-class",
            context => DeclarationArguments(context, "typeName"),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["record-struct"] = "is a record struct",
                ["record-hierarchy"] = "is part of a record hierarchy",
                ["with-expression"] = "is copied with a with expression",
                ["partial-type"] = "merge them first",
            }),
    };

    /// <summary>
    /// The part that receives the members: the first in <c>target.file</c>
    /// when given, otherwise the declaration the symbol id resolves to first.
    /// </summary>
    private static async Task<Dictionary<string, JsonElement>> ReceivingPartArguments(StepContext context)
    {
        if (context.Step.Target?.File is null)
            return await DeclarationArguments(context, "typeName");

        var symbol = await context.SymbolAsync();
        return new Dictionary<string, JsonElement>
        {
            ["solutionPath"] = Json(context.SolutionPath),
            ["filePath"] = Json(context.TargetFilePath()),
            ["typeName"] = Json(symbol.Name),
        };
    }

    /// <summary>
    /// The file, name and line of the declaration <c>target.symbol</c> names.
    /// A constructor is named by its type.
    /// </summary>
    private static async Task<Dictionary<string, JsonElement>> DeclarationArguments(StepContext context, string nameParameter)
    {
        var location = await context.SymbolLocationAsync();
        var name = location.Symbol is IMethodSymbol { MethodKind: MethodKind.Constructor } constructor
            ? constructor.ContainingType.Name
            : location.Symbol.Name;

        return new Dictionary<string, JsonElement>
        {
            ["solutionPath"] = Json(context.SolutionPath),
            ["filePath"] = Json(location.FilePath),
            [nameParameter] = Json(name),
            ["line"] = Json(location.Line),
        };
    }

    private static Dictionary<string, JsonElement> With(
        Dictionary<string, JsonElement> arguments,
        params (string Name, JsonElement Value)[] more)
    {
        foreach (var (name, value) in more)
            arguments[name] = value;

        return arguments;
    }
}
