using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using static RefactorMCP.Tests.Catalog.CatalogMapping;

namespace RefactorMCP.Tests.Catalog.Mappings;

/// <summary>Primitives from the catalog's "Signatures" group.</summary>
internal sealed class SignaturesMappings : ICatalogMappings
{
    public IEnumerable<CatalogMapping> Mappings => new[]
    {
        new CatalogMapping(
            "change-signature",
            "change-signature",
            async context => With(await MemberArguments(context, "methodName"), ("parameters", context.RequiredArgument("parameters"))),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["removed-parameter-in-use"] = "so it cannot be removed",
                ["unknown-parameter"] = "give a type to add it as a new parameter",
                ["duplicate-parameter"] = "already has a parameter named",
                ["missing-value"] = "needs a value for existing calls or a default",
                ["optional-before-required"] = "would follow optional parameter",
                ["params-not-last"] = "must stay last",
                ["method-group-reference"] = "is used as a method group",
                ["external-member"] = "which is declared outside the solution",
                ["extension-this-moved"] = "of an extension method must stay first",
            }),
    };

    /// <summary>
    /// The file, name and line of the declaration <c>target.symbol</c> names.
    /// A constructor is named by its type.
    /// </summary>
    private static async Task<Dictionary<string, JsonElement>> MemberArguments(StepContext context, string nameParameter)
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
