using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static RefactorMCP.Tests.Catalog.CatalogMapping;

namespace RefactorMCP.Tests.Catalog.Mappings;

/// <summary>Primitives from the catalog's "Method conversions" group.</summary>
internal sealed class MethodConversionsMappings : ICatalogMappings
{
    public IEnumerable<CatalogMapping> Mappings => new[]
    {
        new CatalogMapping(
            "convert-to-expression-body",
            "convert-to-expression-body",
            PositionArguments,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["not-single-statement"] = "is not a single return, expression or throw statement",
                ["several-accessors"] = "has more than a plain get accessor",
                ["already-expression-bodied"] = "is already expression-bodied",
                ["directive-in-body"] = "contains a preprocessor directive",
                ["no-body"] = "has no body",
                ["not-a-member-with-body"] = "There is no method, property, accessor, constructor, operator or local function",
            }),
        new CatalogMapping(
            "convert-to-block-body",
            "convert-to-block-body",
            PositionArguments,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["already-block-body"] = "already has a block body",
                ["no-body"] = "has no body",
                ["not-a-member-with-body"] = "There is no method, property, accessor, constructor, operator or local function",
            }),
        new CatalogMapping(
            "convert-lambda-to-method-group",
            "convert-lambda-to-method-group",
            PositionArguments,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["not-a-lambda"] = "There is no lambda",
                ["not-a-forwarding-call"] = "is not a single call of a method passing the lambda's parameters",
                ["unstable-receiver"] = "where a method group would evaluate it once",
                ["resolution-changes"] = "would change which method, delegate type or overload is chosen",
            }),
        new CatalogMapping(
            "convert-method-group-to-lambda",
            "convert-method-group-to-lambda",
            PositionArguments,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["not-a-method-group"] = "is not a method group",
                ["unstable-receiver"] = "where a lambda would evaluate it each time it runs",
                ["resolution-changes"] = "would change which method, delegate type or overload is chosen",
            }),
        new CatalogMapping(
            "convert-method-to-local-function",
            "convert-method-to-local-function",
            MethodArguments,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["not-private"] = "is not private",
                ["never-called"] = "is never called",
                ["called-from-several-members"] = "a local function can only belong to one",
                ["caller-expression-bodied"] = "outside a block body",
                ["called-on-another-instance"] = "on another instance",
                ["name-conflict"] = "is already declared in the caller",
                ["overloaded-method"] = "also uses another overload",
                ["unsupported-method"] = "is an extension, extern or partial method",
            }),
        new CatalogMapping(
            "convert-local-function-to-method",
            "convert-local-function-to-method",
            LocalFunctionArguments,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["not-a-local-function"] = "There is no local function",
                ["name-conflict"] = "already has a member named",
                ["used-as-delegate"] = "is used as a delegate and captures variables",
                ["calls-local-function"] = "which a method could not reach",
                ["ref-in-async"] = "which it would need to take by ref",
            }),
    };

    private static async Task<Dictionary<string, JsonElement>> MethodArguments(StepContext context)
    {
        var location = await context.SymbolLocationAsync();
        return new Dictionary<string, JsonElement>
        {
            ["solutionPath"] = Json(context.SolutionPath),
            ["filePath"] = Json(location.FilePath),
            ["methodName"] = Json(location.Symbol.Name),
            ["line"] = Json(location.Line),
        };
    }

    /// <summary>
    /// A local function has no symbol id of its own. A caret marks it, on its name or a
    /// call; in a later step of a composite, the containing member's symbol with
    /// <c>arguments.function</c> naming it.
    /// </summary>
    private static async Task<Dictionary<string, JsonElement>> LocalFunctionArguments(StepContext context)
    {
        var (filePath, line, column) = context.Step.Target?.Caret is not null
            ? (context.TargetFilePath(), context.Caret().Line, context.Caret().Column)
            : await LocalFunctionPosition(context);
        var arguments = new Dictionary<string, JsonElement>
        {
            ["solutionPath"] = Json(context.SolutionPath),
            ["filePath"] = Json(filePath),
            ["line"] = Json(line),
            ["column"] = Json(column),
        };
        if (context.HasArgument("name"))
            arguments["name"] = context.RequiredArgument("name");
        return arguments;
    }

    private static async Task<(string FilePath, int Line, int Column)> LocalFunctionPosition(StepContext context)
    {
        var member = await context.SymbolAsync();
        var name = context.RequiredString("function");
        foreach (var reference in member.DeclaringSyntaxReferences)
        {
            var function = (await reference.GetSyntaxAsync())
                .DescendantNodes()
                .OfType<LocalFunctionStatementSyntax>()
                .FirstOrDefault(f => f.Identifier.ValueText == name);
            if (function is null)
                continue;

            var start = function.Identifier.GetLocation().GetLineSpan().StartLinePosition;
            return (function.SyntaxTree.FilePath, start.Line + 1, start.Character + 1);
        }

        throw new InvalidOperationException($"'{member.Name}' declares no local function named '{name}'");
    }

    /// <summary>
    /// Refactorings of a declaration or an expression take a position: the caret, or
    /// the name of the declaration <c>target.symbol</c> identifies.
    /// </summary>
    private static async Task<Dictionary<string, JsonElement>> PositionArguments(StepContext context)
    {
        var (filePath, line, column) = await Position(context);
        return new Dictionary<string, JsonElement>
        {
            ["solutionPath"] = Json(context.SolutionPath),
            ["filePath"] = Json(filePath),
            ["line"] = Json(line),
            ["column"] = Json(column),
        };
    }

    private static async Task<(string FilePath, int Line, int Column)> Position(StepContext context)
    {
        if (context.Step.Target?.Caret is not null)
        {
            var (line, column) = context.Caret();
            return (context.TargetFilePath(), line, column);
        }

        var location = await context.SymbolLocationAsync();
        return (location.FilePath, location.Line, location.Column);
    }
}
