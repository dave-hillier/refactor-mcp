using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using static RefactorMCP.Tests.Catalog.CatalogMapping;

namespace RefactorMCP.Tests.Catalog.Mappings;

/// <summary>Primitives from the catalog's "Moving members and types" group.</summary>
internal sealed class MovingMembersAndTypesMappings : ICatalogMappings
{
    public IEnumerable<CatalogMapping> Mappings => new[]
    {
        new CatalogMapping(
            "rename-file-to-match-type",
            "rename-file-to-match-type",
            context => new Dictionary<string, JsonElement>
            {
                ["solutionPath"] = Json(context.SolutionPath),
                ["filePath"] = Json(context.TargetFilePath()),
            },
            Codes(
                ("multiple-types", "declares more than one top-level type"),
                ("no-type", "declares no top-level type"),
                ("name-already-matches", "The file name already matches"),
                ("file-exists", "already exists"))),

        new CatalogMapping(
            "move-type-to-file",
            "move-to-separate-file",
            async context => new Dictionary<string, JsonElement>
            {
                ["solutionPath"] = Json(context.SolutionPath),
                ["filePath"] = Json(await context.SymbolFilePathAsync()),
                ["typeName"] = Json((await context.SymbolAsync()).Name),
            },
            Codes(
                ("only-type-in-file", "is the only type in"),
                ("file-exists", "already exists"),
                ("nested-type", "is nested in another type"))),

        new CatalogMapping(
            "move-type-to-namespace",
            "move-type-to-namespace",
            async context => new Dictionary<string, JsonElement>
            {
                ["solutionPath"] = Json(context.SolutionPath),
                ["filePath"] = Json(await context.SymbolFilePathAsync()),
                ["typeName"] = Json((await context.SymbolAsync()).Name),
                ["targetNamespace"] = context.RequiredArgument("namespace"),
            },
            Codes(
                ("namespace-unchanged", "is already in namespace"),
                ("type-exists", "already contains a type named"),
                ("nested-type", "its namespace is its container's"),
                ("invalid-namespace", "is not a valid namespace name"),
                ("partial-type", "is a partial type declared in several places"),
                ("nested-namespace-block", "is declared in a nested namespace block"),
                ("shares-file-scoped-namespace", "shares a file-scoped namespace"))),

        new CatalogMapping(
            "sync-namespace-with-folder",
            "sync-namespace-with-folder",
            context => new Dictionary<string, JsonElement>
            {
                ["solutionPath"] = Json(context.SolutionPath),
                ["filePath"] = Json(context.TargetFilePath()),
            },
            Codes(
                ("namespace-matches-folder", "already matches its folder"),
                ("multiple-namespaces", "declares more than one namespace"),
                ("no-namespace", "declares no namespace"),
                ("type-exists", "already contains a type named"),
                ("partial-type", "is a partial type declared in several places"),
                ("invalid-namespace", "is not a valid namespace name"))),

        new CatalogMapping(
            "move-member-to-partial-file",
            "move-member-to-partial-file",
            async context =>
            {
                var arguments = await MemberArguments(context);
                arguments["targetFilePath"] = Json(context.WorkspacePath(context.RequiredString("file")));
                return arguments;
            },
            Codes(
                ("type-not-partial", "is not partial"),
                ("same-file", "is already in"),
                ("no-part-in-file", "declares no part of"),
                ("nested-type", "is nested; add a part of it"))),

        new CatalogMapping(
            "convert-to-extension-method",
            "convert-to-extension-method",
            async context =>
            {
                var arguments = await MemberArguments(context);
                arguments["methodName"] = arguments["memberName"];
                arguments.Remove("memberName");
                if (context.HasArgument("to"))
                    arguments["extensionClass"] = context.RequiredArgument("to");
                return arguments;
            },
            Codes(
                ("already-extension", "is already an extension method"),
                ("not-static-class", "is not a static class"),
                ("nested-or-generic-class", "is nested or generic"),
                ("no-parameters", "has no parameters"),
                ("invalid-first-parameter", "cannot become the extended value"))),

        new CatalogMapping(
            "convert-extension-method-to-static",
            "convert-extension-method-to-static",
            async context =>
            {
                var arguments = await MemberArguments(context);
                arguments["methodName"] = arguments["memberName"];
                arguments.Remove("memberName");
                return arguments;
            },
            Codes(
                ("not-extension", "is not an extension method"),
                ("conditional-access", "null-conditional access"),
                ("method-group", "is used as a method group"))),
    };

    /// <summary>The file, name and line of <c>target.symbol</c>, as the member tools take them.</summary>
    private static async Task<Dictionary<string, JsonElement>> MemberArguments(StepContext context)
    {
        var location = await context.SymbolLocationAsync();
        return new Dictionary<string, JsonElement>
        {
            ["solutionPath"] = Json(context.SolutionPath),
            ["filePath"] = Json(location.FilePath),
            ["memberName"] = Json(location.Symbol.Name),
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
