using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using static RefactorMCP.Tests.Catalog.CatalogMapping;

namespace RefactorMCP.Tests.Catalog.Mappings;

/// <summary>Primitives from the catalog's "Naming and housekeeping" group.</summary>
internal sealed class NamingAndHousekeepingMappings : ICatalogMappings
{
    public IEnumerable<CatalogMapping> Mappings => new[]
    {
        new CatalogMapping(
            "rename",
            "rename-symbol",
            RenameArguments,
            Codes(
                ("name-conflict", "conflicts with existing code"),
                ("invalid-name", "is not a valid identifier"),
                ("file-exists", "already exists"))),

        new CatalogMapping(
            "introduce-type-alias",
            "introduce-type-alias",
            context =>
            {
                var (line, column) = context.Caret();
                return new Dictionary<string, JsonElement>
                {
                    ["solutionPath"] = Json(context.SolutionPath),
                    ["filePath"] = Json(context.TargetFilePath()),
                    ["line"] = Json(line),
                    ["column"] = Json(column),
                    ["aliasName"] = context.RequiredArgument("name"),
                };
            },
            Codes(
                ("invalid-name", "is not a valid identifier"),
                ("name-conflict", "already means something"),
                ("uses-type-parameter", "type parameter"),
                ("not-a-type", "is not on the name of a type"))),

        new CatalogMapping(
            "inline-type-alias",
            "inline-type-alias",
            context => new Dictionary<string, JsonElement>
            {
                ["solutionPath"] = Json(context.SolutionPath),
                ["filePath"] = Json(context.TargetFilePath()),
                ["aliasName"] = context.RequiredArgument("alias"),
            },
            Codes(
                ("not-a-type-alias", "names a namespace"),
                ("alias-not-found", "declares no alias"))),

        new CatalogMapping(
            "safe-delete-member",
            "safe-delete-member",
            context => DeclarationArguments(context, "memberName"),
            Codes(
                ("member-referenced", "is referenced"),
                ("overrides-member", "overrides"),
                ("has-overrides", "is overridden by"),
                ("implements-interface", "implements"),
                ("has-implementations", "is implemented by"),
                ("not-a-member", "safe-delete-type"))),

        new CatalogMapping(
            "safe-delete-type",
            "safe-delete-type",
            context => DeclarationArguments(context, "typeName"),
            Codes(("type-referenced", "is referenced"))),

        new CatalogMapping(
            "safe-delete-local",
            "safe-delete-local",
            context =>
            {
                var (line, column) = context.Caret();
                return new Dictionary<string, JsonElement>
                {
                    ["solutionPath"] = Json(context.SolutionPath),
                    ["filePath"] = Json(context.TargetFilePath()),
                    ["line"] = Json(line),
                    ["column"] = Json(column),
                };
            },
            Codes(
                ("local-referenced", "is referenced"),
                ("using-declaration", "using declaration"),
                ("not-a-local", "no local variable"))),

        new CatalogMapping("cleanup-usings", "cleanup-usings", FileArguments),

        new CatalogMapping(
            "convert-to-file-scoped-namespace",
            "convert-to-file-scoped-namespace",
            FileArguments,
            Codes(
                ("several-namespaces", "more than one namespace"),
                ("members-outside-namespace", "outside the namespace"),
                ("language-version", "C# 10"),
                ("no-block-namespace", "no namespace block"))),

        new CatalogMapping(
            "convert-to-block-namespace",
            "convert-to-block-namespace",
            FileArguments,
            Codes(("no-file-scoped-namespace", "no file-scoped namespace"))),
    };

    private static async Task<Dictionary<string, JsonElement>> RenameArguments(StepContext context)
    {
        if (context.Step.Target?.Caret is not null)
        {
            var (line, column) = context.Caret();
            return new Dictionary<string, JsonElement>
            {
                ["solutionPath"] = Json(context.SolutionPath),
                ["filePath"] = Json(context.TargetFilePath()),
                ["oldName"] = Json(context.TokenAtCaret()),
                ["newName"] = context.RequiredArgument("name"),
                ["line"] = Json(line),
                ["column"] = Json(column),
            };
        }

        var location = await context.SymbolLocationAsync();
        return new Dictionary<string, JsonElement>
        {
            ["solutionPath"] = Json(context.SolutionPath),
            ["filePath"] = Json(location.FilePath),
            ["oldName"] = Json(location.Symbol.Name),
            ["newName"] = context.RequiredArgument("name"),
            ["line"] = Json(location.Line),
            ["column"] = Json(location.Column),
        };
    }

    /// <summary>A declaration found by its file, name and the line of its name.</summary>
    private static async Task<Dictionary<string, JsonElement>> DeclarationArguments(StepContext context, string nameParameter)
    {
        var location = await context.SymbolLocationAsync();
        return new Dictionary<string, JsonElement>
        {
            ["solutionPath"] = Json(context.SolutionPath),
            ["filePath"] = Json(location.FilePath),
            [nameParameter] = Json(location.Symbol.Name),
            ["line"] = Json(location.Line),
        };
    }

    private static Dictionary<string, JsonElement> FileArguments(StepContext context) => new()
    {
        ["solutionPath"] = Json(context.SolutionPath),
        ["filePath"] = Json(context.TargetFilePath()),
    };

    private static IReadOnlyDictionary<string, string> Codes(params (string Code, string Fragment)[] codes)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (code, fragment) in codes)
            map[code] = fragment;
        return map;
    }
}
