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
        MoveMapping("move-instance-method", "instance-method"),
        MoveMapping("move-static-method", "static-method"),
        MoveMapping("move-field", "field"),
        MoveMapping("move-property", "property"),

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
                var arguments = await MethodArguments(context);
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
            MethodArguments,
            Codes(
                ("not-extension", "is not an extension method"),
                ("conditional-access", "null-conditional access"),
                ("method-group", "is used as a method group"))),

        new CatalogMapping(
            "make-method-static",
            "make-method-static",
            async context =>
            {
                var arguments = await MethodArguments(context);
                if (context.HasArgument("pass"))
                    arguments["pass"] = context.RequiredArgument("pass");
                if (context.HasArgument("name"))
                    arguments["parameterName"] = context.RequiredArgument("name");
                return arguments;
            },
            Codes(
                ("already-static", "is already static"),
                ("polymorphic-method", "callers rely on dispatch through the instance"),
                ("not-a-class", "is not a class"),
                ("uses-base", "calls through base"),
                ("assigns-instance-member", "assigns the instance member"),
                ("uses-instance", "pass the instance instead"),
                ("inaccessible-member", "is not accessible where"),
                ("complex-receiver", "would be evaluated once per parameter"),
                ("method-group", "is used as a method group"),
                ("conditional-access", "null-conditional access"),
                ("name-conflict", "already has a parameter or local named"))),

        new CatalogMapping(
            "make-method-instance",
            "make-method-instance",
            async context =>
            {
                var arguments = await MethodArguments(context);
                if (context.HasArgument("parameter"))
                    arguments["parameterName"] = context.RequiredArgument("parameter");
                return arguments;
            },
            Codes(
                ("not-static", "is not static"),
                ("no-parameter-of-type", "has no parameter of its own type"),
                ("by-reference-parameter", "is passed by reference"),
                ("parameter-assigned", "and this cannot be reassigned"),
                ("null-argument", "passes null for"),
                ("method-group", "is used as a method group"))),
    };

    /// <summary>
    /// The four moves share one tool: <c>via</c> names the field, property or
    /// parameter to move through, <c>to</c> the target type, <c>stub</c> whether
    /// a moved method leaves a delegating stub (default true), and <c>file</c> the
    /// file for a target type that has to be created.
    /// </summary>
    private static CatalogMapping MoveMapping(string refactoring, string kind) => new(
        refactoring,
        "move-member",
        async context =>
        {
            var arguments = await MemberArguments(context);
            arguments["kind"] = Json(kind);
            if (context.HasArgument("via"))
                arguments["via"] = context.RequiredArgument("via");
            if (context.HasArgument("to"))
                arguments["targetType"] = context.RequiredArgument("to");
            if (context.HasArgument("stub"))
                arguments["keepStub"] = context.RequiredArgument("stub");
            if (context.HasArgument("file"))
                arguments["targetFilePath"] = Json(context.WorkspacePath(context.RequiredString("file")));
            return arguments;
        },
        MoveErrorCodes);

    private static readonly IReadOnlyDictionary<string, string> MoveErrorCodes = Codes(
        ("via-not-found", "to move through"),
        ("no-reference-to-target", "has no field, property or parameter of type"),
        ("ambiguous-target", "is reachable through several members"),
        ("target-not-in-source", "the solution does not declare"),
        ("target-not-class", "cannot move into it"),
        ("generic-target", "constructed generic type"),
        ("same-type", "is already"),
        ("member-exists", "already has a"),
        ("method-is-static", "is static; move it as a static method"),
        ("static-member-via", "is static; name the target type"),
        ("method-not-static", "is an instance method; move it through"),
        ("polymorphic-method", "callers rely on dispatch through the instance"),
        ("uses-base", "calls through base"),
        ("recursive-method", "calls itself"),
        ("uses-protected-member", "uses the protected member"),
        ("uses-source-members", "cannot be given as a parameter"),
        ("via-assigned", "the member it would move through"),
        ("via-not-accessible", "is not accessible where"),
        ("object-initializer", "An object initializer sets"),
        ("conditional-access", "null-conditional access"),
        ("method-group", "is used as a method group"),
        ("complex-receiver", "store it in a local first"),
        ("null-argument", "passes null for"),
        ("name-conflict", "already has a parameter or local named"),
        ("target-cannot-see-source", "which cannot see"),
        ("readonly-assigned", "is readonly and assigned"));

    /// <summary><see cref="MemberArguments"/> for tools that call the member a method.</summary>
    private static async Task<Dictionary<string, JsonElement>> MethodArguments(StepContext context)
    {
        var arguments = await MemberArguments(context);
        arguments["methodName"] = arguments["memberName"];
        arguments.Remove("memberName");
        return arguments;
    }

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
