using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using static RefactorMCP.Tests.Catalog.CatalogMapping;

namespace RefactorMCP.Tests.Catalog.Mappings;

/// <summary>Primitives from the catalog's "Types and hierarchy" group.</summary>
internal sealed class TypesAndHierarchyMappings : ICatalogMappings
{
    public IEnumerable<CatalogMapping> Mappings => new[]
    {
        new CatalogMapping(
            "create-type",
            "create-type",
            context =>
            {
                var arguments = new Dictionary<string, JsonElement>
                {
                    ["solutionPath"] = Json(context.SolutionPath),
                    ["filePath"] = Json(context.WorkspacePath(context.RequiredString("file"))),
                    ["name"] = context.RequiredArgument("name"),
                };
                CopyOptional(context, arguments, "kind", "kind");
                CopyOptional(context, arguments, "namespace", "namespaceName");
                CopyOptional(context, arguments, "baseType", "baseType");
                return arguments;
            },
            Codes(
                ("type-already-exists", "already exists"),
                ("invalid-name", "is not a valid type name"),
                ("invalid-kind", "Unknown kind"),
                ("type-not-found", "No type named"),
                ("invalid-base-type", "cannot be a base type"))),

        new CatalogMapping(
            "change-base-type",
            "change-base-type",
            async context =>
            {
                var location = await context.SymbolLocationAsync();
                var arguments = new Dictionary<string, JsonElement>
                {
                    ["solutionPath"] = Json(context.SolutionPath),
                    ["filePath"] = Json(location.FilePath),
                    ["className"] = Json(location.Symbol.Name),
                };
                CopyOptional(context, arguments, "to", "newBaseType");
                return arguments;
            },
            Codes(
                ("not-a-class", "only a class has a base class"),
                ("no-base-class", "has no base class to remove"),
                ("type-not-found", "No type named"),
                ("invalid-base-type", "cannot be the base class"),
                ("circular-base", "already derives from"),
                ("base-members-in-use", "relies on members of"),
                ("base-conversion-in-use", "converting to"))),

        new CatalogMapping(
            "pull-up-field",
            "pull-up-field",
            async context => await MemberArguments(context, "fieldName"),
            Codes(
                ("no-base-class", "has no base class"),
                ("base-not-in-source", "is not declared in the solution"),
                ("member-exists-in-base", "already has a member named"),
                ("conflicts-with-sibling", "which would hide the pulled-up"),
                ("uses-subclass-members", "which only"),
                ("breaks-compilation", "would break the build"))),

        new CatalogMapping(
            "pull-up-method",
            "pull-up-method",
            async context =>
            {
                var arguments = await MemberArguments(context, "methodName", withLine: true);
                if (context.HasArgument("abstract"))
                    arguments["makeAbstract"] = context.RequiredArgument("abstract");
                return arguments;
            },
            Codes(
                ("no-base-class", "has no base class"),
                ("base-not-in-source", "is not declared in the solution"),
                ("member-exists-in-base", "already has a member named"),
                ("conflicts-with-sibling", "which would hide the pulled-up"),
                ("uses-subclass-members", "which only"),
                ("base-not-abstract", "is not abstract"),
                ("sibling-lacks-implementation", "does not implement"),
                ("breaks-compilation", "would break the build"))),

        new CatalogMapping(
            "pull-up-constructor-body",
            "pull-up-constructor-body",
            async context =>
            {
                var location = await context.SymbolLocationAsync();
                return new Dictionary<string, JsonElement>
                {
                    ["solutionPath"] = Json(context.SolutionPath),
                    ["filePath"] = Json(location.FilePath),
                    ["className"] = Json(location.Symbol.ContainingType.Name),
                    ["line"] = Json(location.Line),
                };
            },
            Codes(
                ("no-base-class", "has no base class"),
                ("base-not-in-source", "is not declared in the solution"),
                ("already-chains", "already calls"),
                ("nothing-to-pull-up", "nothing can move"),
                ("base-constructor-exists", "already has a constructor taking these parameters"),
                ("breaks-compilation", "would break the build"))),

        new CatalogMapping(
            "extract-interface",
            "extract-interface",
            async context =>
            {
                var location = await context.SymbolLocationAsync();
                var name = context.RequiredString("name");
                var members = context.HasArgument("members")
                    ? string.Join(",", context.RequiredArgument("members").EnumerateArray().Select(m => m.GetString()))
                    : "";
                var interfaceFile = context.OptionalString("file") is { } file
                    ? context.WorkspacePath(file)
                    : Path.Combine(Path.GetDirectoryName(location.FilePath)!, name + ".cs");
                return new Dictionary<string, JsonElement>
                {
                    ["solutionPath"] = Json(context.SolutionPath),
                    ["filePath"] = Json(location.FilePath),
                    ["className"] = Json(location.Symbol.Name),
                    ["memberList"] = Json(members),
                    ["interfaceFilePath"] = Json(interfaceFile),
                    ["interfaceName"] = Json(name),
                };
            },
            Codes(
                ("member-not-found", "No member named"),
                ("member-not-eligible", "is not a public instance member"),
                ("no-members", "No matching members found"),
                ("type-already-exists", "already exists"),
                ("breaks-compilation", "would break the build"))),

        new CatalogMapping(
            "push-down-field",
            "push-down-field",
            async context => await MemberArguments(context, "fieldName"),
            Codes(
                ("no-subclasses", "has no subclasses"),
                ("used-by-base", "cannot move to the subclasses"),
                ("used-through-base", "which would no longer have it"),
                ("uses-base-private-members", "which is private to"),
                ("member-exists-in-subclass", "already has a member named"),
                ("breaks-compilation", "would break the build"))),

        new CatalogMapping(
            "push-down-method",
            "push-down-method",
            async context => await MemberArguments(context, "methodName", withLine: true),
            Codes(
                ("no-subclasses", "has no subclasses"),
                ("used-by-base", "cannot move to the subclasses"),
                ("used-through-base", "which would no longer have it"),
                ("uses-base-private-members", "which is private to"),
                ("member-exists-in-subclass", "already has a member named"),
                ("method-is-overridden", "is overridden in"),
                ("method-is-override", "so callers reach it without naming"),
                ("breaks-compilation", "would break the build"))),
    };

    /// <summary>
    /// Arguments for a tool that finds a member by its class and name, and by
    /// the line of its declaration when overloads share the name.
    /// </summary>
    private static async Task<Dictionary<string, JsonElement>> MemberArguments(
        StepContext context,
        string memberParameter,
        bool withLine = false)
    {
        var location = await context.SymbolLocationAsync();
        var arguments = new Dictionary<string, JsonElement>
        {
            ["solutionPath"] = Json(context.SolutionPath),
            ["filePath"] = Json(location.FilePath),
            ["className"] = Json(location.Symbol.ContainingType.Name),
            [memberParameter] = Json(location.Symbol.Name),
        };
        if (withLine)
            arguments["line"] = Json(location.Line);
        return arguments;
    }

    private static void CopyOptional(
        StepContext context,
        Dictionary<string, JsonElement> arguments,
        string catalogName,
        string toolName)
    {
        if (context.HasArgument(catalogName))
            arguments[toolName] = context.RequiredArgument(catalogName);
    }

    private static IReadOnlyDictionary<string, string> Codes(params (string Code, string Fragment)[] codes)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (code, fragment) in codes)
            map[code] = fragment;
        return map;
    }
}
