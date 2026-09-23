using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using static RefactorMCP.Tests.Catalog.CatalogMapping;

namespace RefactorMCP.Tests.Catalog.Mappings;

/// <summary>The catalog's generators: refactorings that add structure or change behaviour.</summary>
internal sealed class GeneratorsMappings : ICatalogMappings
{
    public IEnumerable<CatalogMapping> Mappings => new[]
    {
        new CatalogMapping(
            "add-null-checks",
            "add-null-checks",
            MethodArguments,
            Codes(
                ("no-body", "has no body to guard"),
                ("nothing-to-guard", "has no reference-type parameter left to guard"))),

        new CatalogMapping(
            "add-observer",
            "add-observer",
            async context =>
            {
                var arguments = await MethodArguments(context);
                arguments["className"] = Json((await context.SymbolAsync()).ContainingType.Name);
                arguments["eventName"] = context.RequiredArgument("event");
                return arguments;
            },
            Codes(
                ("no-body", "has no body to raise the event from"),
                ("not-void", "returns a value"),
                ("ref-parameter", "has a ref, out or in parameter"),
                ("member-exists", "already has a member named"))),

        new CatalogMapping(
            "convert-to-nullable-aware",
            "convert-to-nullable-aware",
            context => new Dictionary<string, JsonElement>
            {
                ["solutionPath"] = Json(context.SolutionPath),
                ["filePath"] = Json(context.TargetFilePath()),
            },
            Codes(
                ("already-nullable-aware", "is already nullable-aware"),
                ("unresolved-warning", "leaves a warning no annotation fixes"))),

        new CatalogMapping(
            "feature-flag-wrapping",
            "feature-flag-refactor",
            context => new Dictionary<string, JsonElement>
            {
                ["solutionPath"] = Json(context.SolutionPath),
                ["filePath"] = Json(context.TargetFilePath()),
                ["flagName"] = context.RequiredArgument("flag"),
            },
            Codes(
                ("flag-not-found", "not found in"),
                ("ambiguous-flag-check", "is checked more than once"),
                ("flag-source-not-member", "which is not a field, property or type the class can read"),
                ("branch-leaves-early", "leaves the method early"),
                ("branch-uses-members", "that a strategy class cannot reach"),
                ("branch-assigns-outer-variable", "declared outside it"),
                ("type-already-exists", "already exists"),
                ("member-exists", "already has a member named"))),

        new CatalogMapping(
            "introduce-null-object",
            "introduce-null-object",
            async context =>
            {
                var location = await context.SymbolLocationAsync();
                return new Dictionary<string, JsonElement>
                {
                    ["solutionPath"] = Json(context.SolutionPath),
                    ["filePath"] = Json(location.FilePath),
                    ["fieldName"] = Json(location.Symbol.Name),
                };
            },
            Codes(
                ("not-an-interface", "which is not an interface"),
                ("no-null-checks", "is never checked for null"),
                ("unsupported-null-check", "is not one a null object can replace"),
                ("inconsistent-fallbacks", "fall back to different values"),
                ("type-already-exists", "already exists"),
                ("breaks-compilation", "would break the build"))),

        new CatalogMapping(
            "replace-error-code-with-exception",
            "replace-error-code-with-exception",
            async context =>
            {
                var location = await context.SymbolLocationAsync();
                var arguments = new Dictionary<string, JsonElement>
                {
                    ["solutionPath"] = Json(context.SolutionPath),
                    ["filePath"] = Json(location.FilePath),
                    ["methodName"] = Json(location.Symbol.Name),
                    ["line"] = Json(location.Line),
                };
                if (context.HasArgument("exception"))
                    arguments["exceptionType"] = context.RequiredArgument("exception");
                return arguments;
            },
            Codes(
                ("in-hierarchy", "is virtual, abstract, an override or an interface implementation"),
                ("unsupported-return-type", "only an int or bool error code can be replaced"),
                ("non-constant-return", "which is not a constant error code"),
                ("no-error-code", "never returns an error code"),
                ("exception-type-not-found", "No exception type named"),
                ("unsupported-caller", "in a way a catch cannot replace"),
                ("breaks-compilation", "would break the build"))),

        new CatalogMapping(
            "replace-array-with-object",
            "replace-array-with-object",
            async context =>
            {
                string filePath;
                int line, column;
                if (context.Step.Target?.Symbol is not null)
                {
                    var location = await context.SymbolLocationAsync();
                    (filePath, line, column) = (location.FilePath, location.Line, location.Column);
                }
                else
                {
                    filePath = context.TargetFilePath();
                    (line, column) = context.Caret();
                }

                return new Dictionary<string, JsonElement>
                {
                    ["solutionPath"] = Json(context.SolutionPath),
                    ["filePath"] = Json(filePath),
                    ["line"] = Json(line),
                    ["column"] = Json(column),
                    ["className"] = context.RequiredArgument("name"),
                    ["memberNames"] = context.RequiredArgument("members"),
                };
            },
            Codes(
                ("not-an-array", "is not a single-dimensional array"),
                ("generic-element-type", "uses a type parameter"),
                ("invalid-name", "is not a valid name"),
                ("type-already-exists", "already exists"),
                ("unsupported-use", "is used as an array"),
                ("index-out-of-range", "has no member to name it"),
                ("wrong-member-count", "member names were given"),
                ("breaks-compilation", "would break the build"))),

        new CatalogMapping(
            "replace-type-code-with-enum",
            "replace-type-code-with-enum",
            async context =>
            {
                var location = await context.SymbolLocationAsync();
                return new Dictionary<string, JsonElement>
                {
                    ["solutionPath"] = Json(context.SolutionPath),
                    ["filePath"] = Json(location.FilePath),
                    ["typeName"] = Json(location.Symbol.Name),
                    ["constantNames"] = context.RequiredArgument("constants"),
                    ["enumName"] = context.RequiredArgument("name"),
                };
            },
            Codes(
                ("constant-not-found", "has no constant named"),
                ("mixed-types", "mix int and string"),
                ("not-a-type-code", "is not an int or string constant"),
                ("type-already-exists", "already exists"),
                ("breaks-compilation", "would not compile"))),

        new CatalogMapping(
            "replace-type-code-with-subclasses",
            "replace-type-code-with-subclasses",
            async context =>
            {
                var location = await context.SymbolLocationAsync();
                return new Dictionary<string, JsonElement>
                {
                    ["solutionPath"] = Json(context.SolutionPath),
                    ["filePath"] = Json(location.FilePath),
                    ["fieldName"] = Json(location.Symbol.Name),
                };
            },
            Codes(
                ("not-an-enum", "not an enum"),
                ("class-sealed", "is not a class that can have subclasses"),
                ("type-already-exists", "already exists"),
                ("code-changes", "changes after construction"),
                ("unsupported-constructor", "must have one constructor that assigns"),
                ("breaks-compilation", "would not compile"))),

        new CatalogMapping(
            "replace-conditional-with-polymorphism",
            "replace-conditional-with-polymorphism",
            async context =>
            {
                var location = await context.SymbolLocationAsync();
                return new Dictionary<string, JsonElement>
                {
                    ["solutionPath"] = Json(context.SolutionPath),
                    ["filePath"] = Json(location.FilePath),
                    ["methodName"] = Json(location.Symbol.Name),
                    ["line"] = Json(location.Line),
                };
            },
            Codes(
                ("no-conditional", "is not a single switch or if chain"),
                ("unsupported-case", "cannot be moved to one subclass"),
                ("subclass-without-case", "has no case and there is no default"),
                ("base-not-abstract", "is not abstract"),
                ("uses-caller-members", "which the subclass cannot reach"),
                ("breaks-compilation", "would not compile"))),

        new CatalogMapping(
            "extract-decorator",
            "extract-decorator",
            async context =>
            {
                var location = await context.SymbolLocationAsync();
                var arguments = new Dictionary<string, JsonElement>
                {
                    ["solutionPath"] = Json(context.SolutionPath),
                    ["filePath"] = Json(location.FilePath),
                    ["typeName"] = Json(location.Symbol.Name),
                };
                if (context.HasArgument("name"))
                    arguments["decoratorName"] = context.RequiredArgument("name");
                return arguments;
            },
            Codes(
                ("no-interface", "implements no interface"),
                ("ambiguous-interface", "implements several interfaces"),
                ("type-already-exists", "already exists"))),

        new CatalogMapping(
            "create-adapter",
            "create-adapter",
            async context =>
            {
                var location = await context.SymbolLocationAsync();
                var arguments = new Dictionary<string, JsonElement>
                {
                    ["solutionPath"] = Json(context.SolutionPath),
                    ["filePath"] = Json(location.FilePath),
                    ["className"] = Json(location.Symbol.Name),
                    ["interfaceName"] = context.RequiredArgument("interface"),
                    ["adapterName"] = context.RequiredArgument("name"),
                };
                if (context.HasArgument("map"))
                {
                    var map = context.RequiredArgument("map").EnumerateObject()
                        .Select(entry => $"{entry.Name}:{entry.Value.GetString()}");
                    arguments["memberMap"] = Json(string.Join(",", map));
                }

                return arguments;
            },
            Codes(
                ("interface-not-found", "No type named"),
                ("not-an-interface", "is not an interface"),
                ("member-not-found", "has no member named"),
                ("incompatible-signature", "cannot stand in for"),
                ("type-already-exists", "already exists"))),
    };

    /// <summary>
    /// The file, name and line of the method <c>target.symbol</c> names. A
    /// constructor is named by its type.
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

    private static IReadOnlyDictionary<string, string> Codes(params (string Code, string Fragment)[] codes)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (code, fragment) in codes)
            map[code] = fragment;
        return map;
    }
}
