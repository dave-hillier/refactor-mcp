using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using static RefactorMCP.Tests.Catalog.CatalogMapping;

namespace RefactorMCP.Tests.Catalog.Mappings;

/// <summary>Composites from the catalog that restructure types and move members.</summary>
internal sealed class StructuralCompositesMappings : ICatalogMappings
{
    public IEnumerable<CatalogMapping> Mappings => new[]
    {
        new CatalogMapping(
            "extract-class",
            "extract-class",
            async context =>
            {
                var location = await context.SymbolLocationAsync();
                var arguments = new Dictionary<string, JsonElement>
                {
                    ["solutionPath"] = Json(context.SolutionPath),
                    ["filePath"] = Json(location.FilePath),
                    ["className"] = Json(location.Symbol.Name),
                    ["newClassName"] = context.RequiredArgument("name"),
                    ["memberNames"] = context.RequiredArgument("members"),
                };
                CopyOptional(context, arguments, "field", "fieldName");
                CopyOptional(context, arguments, "stub", "keepStubs");
                if (context.HasArgument("file"))
                    arguments["targetFilePath"] = Json(context.WorkspacePath(context.RequiredString("file")));
                return arguments;
            },
            Codes(
                ("not-a-class", "is not a class"),
                ("no-members", "No members were named"),
                ("member-not-found", "has no member named"),
                ("member-not-movable", "cannot move to the new class"),
                ("type-already-exists", "already exists"),
                ("name-conflict", "already has a member named"),
                ("polymorphic-method", "callers rely on dispatch through the instance"),
                ("uses-protected-member", "uses the protected member"),
                ("via-not-accessible", "is not accessible where"))),

        new CatalogMapping(
            "extract-superclass",
            "extract-superclass",
            async context =>
            {
                var location = await context.SymbolLocationAsync();
                var arguments = new Dictionary<string, JsonElement>
                {
                    ["solutionPath"] = Json(context.SolutionPath),
                    ["filePath"] = Json(location.FilePath),
                    ["className"] = Json(location.Symbol.Name),
                    ["superclassName"] = context.RequiredArgument("name"),
                };
                CopyOptional(context, arguments, "members", "memberNames");
                if (context.HasArgument("file"))
                    arguments["targetFilePath"] = Json(context.WorkspacePath(context.RequiredString("file")));
                return arguments;
            },
            Codes(
                ("not-a-class", "cannot be given a superclass"),
                ("member-not-found", "has no member named"),
                ("member-not-movable", "cannot be pulled up"),
                ("type-already-exists", "already exists"),
                ("uses-subclass-members", "which only"),
                ("member-exists-in-base", "already has a member named"),
                ("breaks-compilation", "would break the build"))),

        new CatalogMapping(
            "introduce-interface-for-dependency",
            "introduce-interface-for-dependency",
            async context =>
            {
                var location = await context.SymbolLocationAsync();
                var arguments = new Dictionary<string, JsonElement>
                {
                    ["solutionPath"] = Json(context.SolutionPath),
                    ["filePath"] = Json(location.FilePath),
                    ["name"] = Json(location.Symbol is IMethodSymbol { MethodKind: MethodKind.Constructor }
                        ? location.Symbol.ContainingType.Name
                        : location.Symbol.Name),
                    ["line"] = Json(location.Line),
                    ["interfaceName"] = context.RequiredArgument("name"),
                };
                CopyOptional(context, arguments, "parameter", "parameterName");
                CopyOptional(context, arguments, "members", "memberNames");
                if (context.HasArgument("file"))
                    arguments["interfaceFilePath"] = Json(context.WorkspacePath(context.RequiredString("file")));
                return arguments;
            },
            Codes(
                ("type-not-in-source", "is not a class declared in the solution"),
                ("parameter-not-found", "has no parameter named"),
                ("member-not-found", "No member named"),
                ("type-already-exists", "already exists"),
                ("incompatible-use", "breaks code that uses it"),
                ("changes-overload", "would change which member"))),

        new CatalogMapping(
            "make-static-then-move",
            "make-static-then-move",
            async context =>
            {
                var location = await context.SymbolLocationAsync();
                var arguments = new Dictionary<string, JsonElement>
                {
                    ["solutionPath"] = Json(context.SolutionPath),
                    ["filePath"] = Json(location.FilePath),
                    ["methodName"] = Json(location.Symbol.Name),
                    ["line"] = Json(location.Line),
                    ["targetClass"] = context.RequiredArgument("to"),
                };
                CopyOptional(context, arguments, "name", "instanceParameterName");
                CopyOptional(context, arguments, "stub", "keepStub");
                if (context.HasArgument("file"))
                    arguments["targetFilePath"] = Json(context.WorkspacePath(context.RequiredString("file")));
                return arguments;
            },
            Codes(
                ("already-static", "is already static"),
                ("polymorphic-method", "callers rely on dispatch through the instance"),
                ("not-a-class", "is not a class"),
                ("uses-base", "calls through base"),
                ("method-group", "is used as a method group"),
                ("conditional-access", "null-conditional access"),
                ("name-conflict", "already has a parameter or local named"),
                ("same-type", "is already"),
                ("member-exists", "already has a"),
                ("target-not-class", "cannot move into it"),
                ("uses-protected-member", "uses the protected member"))),
    };

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
