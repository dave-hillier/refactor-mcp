using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static RefactorMCP.Tests.Catalog.CatalogMapping;

namespace RefactorMCP.Tests.Catalog.Mappings;

/// <summary>Primitives from the catalog's "Methods and locals" group.</summary>
internal sealed class MethodsAndLocalsMappings : ICatalogMappings
{
    public IEnumerable<CatalogMapping> Mappings => new[]
    {
        new CatalogMapping(
            "extract-method",
            "extract-method",
            context => new Dictionary<string, JsonElement>
            {
                ["solutionPath"] = Json(context.SolutionPath),
                ["filePath"] = Json(context.TargetFilePath()),
                ["selectionRange"] = Json(context.SelectionRange()),
                ["methodName"] = context.RequiredArgument("name"),
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["expression-bodied-member"] = "Extraction from expression-bodied methods is not supported",
                ["declared-local-used-after"] = "The extracted block declares",
                ["assigned-local-used-after"] = "The extracted block assigns",
                ["not-in-method"] = "Selected code is not within a method",
                ["no-statements-selected"] = "does not contain extractable statements",
                ["not-a-value"] = "is not a value a method could return",
                ["expression-assigns-local"] = "The selected expression assigns",
                ["expression-awaits"] = "The selected expression awaits",
            }),
        new CatalogMapping(
            "inline-method",
            "inline-method",
            InlineMethodArguments,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["recursive-method"] = "calls itself",
                ["polymorphic-method"] = "is virtual, an override or an interface implementation",
                ["method-group-reference"] = "is used without being called",
                ["inaccessible-member"] = "which is not accessible at",
                ["multiple-statements"] = "computes its result in several statements",
                ["early-return"] = "returns before its last statement",
                ["name-conflict"] = "which is already used there",
                ["unsupported-method"] = "is async or an iterator",
                ["unsupported-call"] = "is not a statement of its own",
            }),
        new CatalogMapping(
            "extract-local-variable",
            "introduce-variable",
            context => new Dictionary<string, JsonElement>
            {
                ["solutionPath"] = Json(context.SolutionPath),
                ["filePath"] = Json(context.TargetFilePath()),
                ["selectionRange"] = Json(context.SelectionRange()),
                ["variableName"] = context.RequiredArgument("name"),
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["expression-bodied-member"] = "in an expression-bodied member is not supported",
                ["not-in-statement"] = "is not inside a statement",
                ["in-loop-condition"] = "is part of a loop condition",
                ["conditionally-evaluated"] = "is only evaluated on some paths",
                ["declared-in-statement"] = "which is declared inside the statement",
                ["name-conflict"] = "is already declared or used",
                ["void-expression"] = "has no value",
                ["not-an-expression"] = "is not a valid expression",
            }),
        new CatalogMapping(
            "split-declaration-and-assignment",
            "split-declaration-and-assignment",
            LocalArguments,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["no-initializer"] = "has no initializer to split off",
                ["multiple-declarators"] = "declares several locals",
                ["const-local"] = "is a constant",
                ["using-declaration"] = "is a using declaration",
                ["anonymous-type"] = "has an anonymous type",
                ["not-a-local"] = "There is no local variable",
            }),
        new CatalogMapping(
            "join-declaration-and-assignment",
            "join-declaration-and-assignment",
            LocalArguments,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["has-initializer"] = "already has an initializer",
                ["first-use-not-assignment"] = "is not an assignment to it in the same block",
                ["never-assigned"] = "is never assigned",
                ["multiple-declarators"] = "declares several locals",
                ["not-a-local"] = "There is no local variable",
            }),
        new CatalogMapping(
            "inline-local-variable",
            "inline-local-variable",
            LocalArguments,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["no-initializer"] = "has no initializer to inline",
                ["assigned-after-declaration"] = "is assigned after its declaration",
                ["initializer-has-side-effects"] = "has side effects",
                ["initializer-inputs-change"] = "which the initializer of",
                ["never-used"] = "is never used",
                ["using-declaration"] = "is a using declaration",
                ["not-a-local"] = "There is no local variable",
            }),
        new CatalogMapping(
            "split-temporary-variable",
            "split-temporary-variable",
            LocalArguments,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["not-an-assignment"] = "is not on an assignment to",
                ["assignment-reads-variable"] = "The assignment reads",
                ["assignment-in-nested-block"] = "is in a block nested inside",
                ["captured-variable"] = "is captured by a lambda or local function",
                ["name-conflict"] = "is already declared in the scope",
                ["not-a-local"] = "There is no local variable",
            }),
        new CatalogMapping(
            "convert-local-to-field",
            "convert-local-to-field",
            LocalArguments,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["name-conflict"] = "already has a member named",
                ["hidden-by-local"] = "is already declared in the member",
                ["uses-method-type-parameter"] = "uses a type parameter of the method",
                ["using-declaration"] = "is a using declaration",
                ["multiple-declarators"] = "declares several locals",
                ["anonymous-type"] = "has an anonymous type",
                ["not-a-local"] = "There is no local variable",
            }),
    };

    private static async Task<Dictionary<string, JsonElement>> InlineMethodArguments(StepContext context)
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
    /// Refactorings of a single local take its position. A caret marks it directly;
    /// in a later step of a composite, where markers no longer apply, the target is
    /// the containing member's symbol and <c>arguments.local</c> names the local.
    /// </summary>
    private static async Task<Dictionary<string, JsonElement>> LocalArguments(StepContext context)
    {
        var (filePath, line, column) = await LocalPosition(context);
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

    private static async Task<(string FilePath, int Line, int Column)> LocalPosition(StepContext context)
    {
        if (context.Step.Target?.Caret is not null)
        {
            var (line, column) = context.Caret();
            return (context.TargetFilePath(), line, column);
        }

        var member = await context.SymbolAsync();
        var local = context.RequiredString("local");
        foreach (var reference in member.DeclaringSyntaxReferences)
        {
            var declarator = (await reference.GetSyntaxAsync())
                .DescendantNodes()
                .OfType<VariableDeclaratorSyntax>()
                .FirstOrDefault(d => d.Identifier.ValueText == local);
            if (declarator is null)
                continue;

            var start = declarator.Identifier.GetLocation().GetLineSpan().StartLinePosition;
            return (declarator.SyntaxTree.FilePath, start.Line + 1, start.Character + 1);
        }

        throw new InvalidOperationException($"'{member.Name}' declares no local named '{local}'");
    }
}
