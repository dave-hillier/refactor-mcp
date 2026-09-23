using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static RefactorMCP.Tests.Catalog.CatalogMapping;

namespace RefactorMCP.Tests.Catalog.Mappings;

/// <summary>Primitives from the catalog's "Fields, properties and constants" group.</summary>
internal sealed class FieldsPropertiesAndConstantsMappings : ICatalogMappings
{
    public IEnumerable<CatalogMapping> Mappings => new[]
    {
        new CatalogMapping(
            "introduce-field",
            "introduce-field",
            IntroduceFieldArguments,
            Codes(
                ("not-an-expression", "The selection is not an expression"),
                ("no-value", "has no value"),
                ("name-conflict", "already has a member named"),
                ("conditionally-evaluated", "may not be evaluated every time"),
                ("expression-bodied-member", "expression-bodied member"),
                ("method-type-parameter", "type parameter of the method"),
                ("not-in-block", "is not in a block"),
                ("assigned-expression", "is assigned to"),
                ("unknown-type", "No type named"))),
        new CatalogMapping(
            "inline-field",
            "inline-field",
            context => MemberArguments(context, "fieldName"),
            Codes(
                ("no-initializer", "has no initializer"),
                ("written-after-initialization", "is assigned outside its initializer"),
                ("initializer-not-inlinable", "may give a different value"),
                ("used-in-nameof", "is named by nameof"))),
        new CatalogMapping(
            "introduce-constant",
            "introduce-constant",
            context => new Dictionary<string, JsonElement>
            {
                ["solutionPath"] = Json(context.SolutionPath),
                ["filePath"] = Json(context.TargetFilePath()),
                ["selectionRange"] = Json(context.SelectionRange()),
                ["constantName"] = context.RequiredArgument("name"),
                ["replaceAll"] = context.HasArgument("replaceAll") ? context.RequiredArgument("replaceAll") : Json(false),
            },
            Codes(
                ("not-an-expression", "The selection is not an expression"),
                ("not-constant", "is not a compile-time constant"),
                ("references-local", "uses the local constant"),
                ("name-conflict", "already has a member named"))),
        new CatalogMapping(
            "inline-constant",
            "inline-constant",
            context => MemberArguments(context, "constantName"),
            Codes(
                ("not-a-constant", "is not a constant"),
                ("used-in-nameof", "is named by nameof"))),
        new CatalogMapping(
            "encapsulate-field",
            "encapsulate-field",
            context => MemberArguments(context, "fieldName", ("name", "propertyName")),
            Codes(
                ("constant-field", "is a constant"),
                ("multiple-declarators", "is declared alongside other fields"),
                ("name-conflict", "already has a member named"),
                ("passed-by-reference", "is passed by reference"))),
        new CatalogMapping(
            "convert-to-auto-property",
            "convert-to-auto-property",
            context => MemberArguments(context, "propertyName"),
            Codes(
                ("no-backing-field", "has no backing field"),
                ("not-trivial", "does more than read and write a single field"),
                ("backing-field-not-private", "is not private"),
                ("field-passed-by-reference", "is passed by reference"))),
        new CatalogMapping(
            "convert-auto-property-to-backing-field",
            "convert-auto-property-to-backing-field",
            context => MemberArguments(context, "propertyName", ("name", "fieldName")),
            Codes(
                ("not-auto-property", "is not an auto-property"),
                ("name-conflict", "already has a member named"))),
    };

    /// <summary>
    /// A selection introduces a field from the selected expression. A symbol
    /// target naming a type, with a <c>type</c> argument, adds a field of that
    /// type to it; the tool finds the type from a selection of its name.
    /// </summary>
    private static async Task<Dictionary<string, JsonElement>> IntroduceFieldArguments(StepContext context)
    {
        if (context.Step.Target?.Symbol is null)
        {
            return new Dictionary<string, JsonElement>
            {
                ["solutionPath"] = Json(context.SolutionPath),
                ["filePath"] = Json(context.TargetFilePath()),
                ["selectionRange"] = Json(context.SelectionRange()),
                ["fieldName"] = context.RequiredArgument("name"),
            };
        }

        var location = await SymbolLocationAsync(context);
        return new Dictionary<string, JsonElement>
        {
            ["solutionPath"] = Json(context.SolutionPath),
            ["filePath"] = Json(location.FilePath),
            ["selectionRange"] = Json(NameRange(location)),
            ["fieldName"] = context.RequiredArgument("name"),
            ["fieldType"] = context.RequiredArgument("type"),
        };
    }

    /// <summary>
    /// A symbol-targeted step as the tools that take a member name expect it,
    /// with any optional catalog arguments passed on under the tool's names.
    /// </summary>
    private static async Task<Dictionary<string, JsonElement>> MemberArguments(
        StepContext context,
        string memberParameter,
        params (string Argument, string Parameter)[] optional)
    {
        var location = await SymbolLocationAsync(context);
        var arguments = new Dictionary<string, JsonElement>
        {
            ["solutionPath"] = Json(context.SolutionPath),
            ["filePath"] = Json(location.FilePath),
            [memberParameter] = Json(location.Symbol.Name),
        };

        foreach (var (argument, parameter) in optional)
        {
            if (context.HasArgument(argument))
                arguments[parameter] = context.RequiredArgument(argument);
        }

        return arguments;
    }

    /// <summary>
    /// Resolves <c>target.symbol</c> as <see cref="StepContext.SymbolLocationAsync"/>
    /// does, falling back to comparing the ids of declared symbols. Roslyn's
    /// id lookup misses a property whose type is a type parameter, such as
    /// <c>P:Shop.Box`1.Item</c> for <c>T Item</c>.
    /// </summary>
    private static async Task<SymbolLocation> SymbolLocationAsync(StepContext context)
    {
        try
        {
            return await context.SymbolLocationAsync();
        }
        catch (InvalidOperationException) when (context.Step.Target?.Symbol is { } id)
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(context.SolutionPath);
            foreach (var document in solution.Projects.SelectMany(p => p.Documents))
            {
                var model = (await document.GetSemanticModelAsync())!;
                var root = await document.GetSyntaxRootAsync();
                var symbol = root!.DescendantNodes()
                    .Where(node => node is MemberDeclarationSyntax or VariableDeclaratorSyntax)
                    .Select(node => model.GetDeclaredSymbol(node))
                    .FirstOrDefault(s => s?.GetDocumentationCommentId() == id);
                if (symbol is null)
                    continue;

                var location = symbol.Locations.First(l => l.IsInSource);
                var start = location.GetLineSpan().StartLinePosition;
                return new SymbolLocation(symbol, location.SourceTree!.FilePath, start.Line + 1, start.Character + 1);
            }

            throw;
        }
    }

    private static string NameRange(SymbolLocation location) =>
        $"{location.Line}:{location.Column}-{location.Line}:{location.Column + location.Symbol.Name.Length}";

    private static IReadOnlyDictionary<string, string> Codes(params (string Code, string Fragment)[] codes)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (code, fragment) in codes)
            map[code] = fragment;
        return map;
    }
}
