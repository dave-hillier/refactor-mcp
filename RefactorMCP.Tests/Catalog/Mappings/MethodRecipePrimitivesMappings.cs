using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using static RefactorMCP.Tests.Catalog.CatalogMapping;

namespace RefactorMCP.Tests.Catalog.Mappings;

/// <summary>
/// Primitives that the method composites' recipes need: redirecting calls that
/// pass a constant, initialising a field from a constructor parameter,
/// replacing an expression with a field, and making a method async.
/// </summary>
internal sealed class MethodRecipePrimitivesMappings : ICatalogMappings
{
    public IEnumerable<CatalogMapping> Mappings => new[]
    {
        new CatalogMapping(
            "redirect-calls-with-constant-argument",
            "redirect-calls-with-constant-argument",
            async context =>
            {
                var arguments = await MethodArguments(context);
                arguments["parameterName"] = context.RequiredArgument("parameter");
                arguments["value"] = context.RequiredArgument("value");
                arguments["targetMethodName"] = context.RequiredArgument("method");
                return arguments;
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["not-a-method"] = "is not an ordinary method with a block body",
                ["polymorphic-method"] = "is virtual, an override or an interface implementation",
                ["generic-method"] = "is generic",
                ["unknown-parameter"] = "has no parameter named",
                ["not-a-constant"] = "is not a constant of type",
                ["unknown-method"] = "has no method named",
                ["does-more-than-call"] = "does more than call",
                ["does-not-compile"] = "The change would not compile",
            }),
        new CatalogMapping(
            "make-method-async",
            "make-method-async",
            MethodArguments,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["nothing-to-await"] = "does not block on a task, so it has nothing to await",
                ["already-async"] = "already returns a task",
                ["not-a-method"] = "is not an ordinary method",
                ["polymorphic-method"] = "is virtual, an override or an interface implementation",
                ["ref-parameters"] = "has ref, out or in parameters",
                ["iterator"] = "is an iterator",
                ["method-group-reference"] = "is used as a method group",
            }),
    };

    /// <summary>
    /// The file, name and line of the method <c>target.symbol</c> names. A constructor
    /// is named by its type.
    /// </summary>
    private static async Task<Dictionary<string, JsonElement>> MethodArguments(StepContext context)
    {
        var location = await context.SymbolLocationAsync();
        var name = location.Symbol is IMethodSymbol { MethodKind: MethodKind.Constructor } constructor
            ? constructor.ContainingType.Name
            : location.Symbol.Name;

        return new Dictionary<string, JsonElement>
        {
            ["solutionPath"] = Json(context.SolutionPath),
            ["filePath"] = Json(location.FilePath),
            ["methodName"] = Json(name),
            ["line"] = Json(location.Line),
        };
    }
}
