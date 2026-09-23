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
        new CatalogMapping(
            "convert-class-to-record",
            "convert-class-to-record",
            context => DeclarationArguments(context, "typeName"),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["unsupported-type"] = "only a class with instances can become a record",
                ["language-version"] = "raise the language version first",
                ["class-hierarchy"] = "is part of a class hierarchy",
                ["mutable-state"] = "has mutable state",
                ["declares-equality"] = "overrides Equals(object)",
                ["equality-observed"] = "value equality would change behaviour",
                ["to-string-observed"] = "ToString would change behaviour",
            }),
        new CatalogMapping(
            "convert-to-primary-constructor",
            "convert-to-primary-constructor",
            context => DeclarationArguments(context, "typeName"),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["unsupported-type"] = "is not a class or struct",
                ["language-version"] = "raise the language version first",
                ["several-constructors"] = "a primary constructor replaces exactly one",
                ["constructor-accessibility"] = "but a primary constructor is public",
                ["constructor-annotated"] = "which a primary constructor has nowhere to keep",
                ["constructor-has-logic"] = "does more than assign its parameters",
                ["parameter-shadowed"] = "would not read the primary constructor's parameter",
            }),
        new CatalogMapping(
            "convert-primary-constructor-to-constructor",
            "convert-primary-constructor-to-constructor",
            context => DeclarationArguments(context, "typeName"),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["unsupported-type"] = "is not a class or struct with a single declaration",
                ["no-primary-constructor"] = "has no primary constructor",
                ["name-conflict"] = "already has a member of that name",
            }),
        new CatalogMapping(
            "replace-constructor-with-factory-method",
            "replace-constructor-with-factory-method",
            async context =>
            {
                var arguments = await DeclarationArguments(context, "typeName");
                if (context.HasArgument("name"))
                    arguments["methodName"] = context.RequiredArgument("name");
                if (context.HasArgument("accessibility"))
                    arguments["accessibility"] = context.RequiredArgument("accessibility");
                return arguments;
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["invalid-accessibility"] = "is not an accessibility",
                ["abstract-type"] = "is not a concrete class",
                ["name-conflict"] = "that the factory method would clash with",
                ["object-initializer"] = "uses an object initializer",
                ["constructor-still-needed"] = "is still needed at its old accessibility",
            }),
        new CatalogMapping(
            "convert-anonymous-type-to-class",
            "convert-anonymous-type-to-class",
            context =>
            {
                var (line, column) = context.Caret();
                return new Dictionary<string, JsonElement>
                {
                    ["solutionPath"] = Json(context.SolutionPath),
                    ["filePath"] = Json(context.TargetFilePath()),
                    ["line"] = Json(line),
                    ["column"] = Json(column),
                    ["className"] = context.RequiredArgument("name"),
                };
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["no-anonymous-type"] = "There is no anonymous object creation at",
                ["unnamable-property-type"] = "has a type the class cannot name",
                ["name-conflict"] = "is already visible where the class would be declared",
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
