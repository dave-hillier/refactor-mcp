using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static RefactorMCP.Tests.Catalog.CatalogMapping;

namespace RefactorMCP.Tests.Catalog.Mappings;

/// <summary>Composites from the catalog that work on methods and their signatures.</summary>
internal sealed class MethodCompositesMappings : ICatalogMappings
{
    public IEnumerable<CatalogMapping> Mappings => new[]
    {
        new CatalogMapping(
            "replace-temp-with-query",
            "replace-temp-with-query",
            async context =>
            {
                var (filePath, line, column) = await LocalPosition(context);
                return new Dictionary<string, JsonElement>
                {
                    ["solutionPath"] = Json(context.SolutionPath),
                    ["filePath"] = Json(filePath),
                    ["line"] = Json(line),
                    ["column"] = Json(column),
                    ["queryName"] = context.RequiredArgument("name"),
                };
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["no-initializer"] = "has no initializer to become the query",
                ["assigned-after-declaration"] = "is assigned after its declaration",
                ["initializer-has-side-effects"] = "has side effects",
                ["initializer-inputs-change"] = "which the initializer of",
                ["never-used"] = "is never used",
                ["name-conflict"] = "already has a member named",
                ["not-in-method"] = "is not declared in the body of a class's method",
                ["not-a-local"] = "There is no local variable",
            }),
        new CatalogMapping(
            "decompose-conditional",
            "decompose-conditional",
            context =>
            {
                var (line, column) = context.Caret();
                var arguments = new Dictionary<string, JsonElement>
                {
                    ["solutionPath"] = Json(context.SolutionPath),
                    ["filePath"] = Json(context.TargetFilePath()),
                    ["line"] = Json(line),
                    ["column"] = Json(column),
                    ["conditionName"] = context.RequiredArgument("conditionName"),
                    ["thenName"] = context.RequiredArgument("thenName"),
                };
                if (context.HasArgument("elseName"))
                    arguments["elseName"] = context.RequiredArgument("elseName");
                return arguments;
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["not-an-if"] = "There is no if statement at",
                ["no-else-branch"] = "has no else branch to extract",
                ["else-if-branch"] = "The else branch is another if statement",
                ["empty-branch"] = "is empty, so there is nothing to extract",
                ["assigned-local-used-after"] = "The extracted block assigns",
                ["declared-local-used-after"] = "The extracted block declares",
                ["returns-early"] = "which the new method cannot do for it",
                ["name-conflict"] = "already has a member named",
                ["not-in-method"] = "Selected code is not within a method",
            }),
        new CatalogMapping(
            "introduce-parameter-object",
            "introduce-parameter-object",
            async context =>
            {
                var arguments = await MethodArguments(context);
                arguments["parameters"] = context.RequiredArgument("parameters");
                arguments["typeName"] = context.RequiredArgument("typeName");
                arguments["parameterName"] = context.RequiredArgument("parameterName");
                if (context.HasArgument("kind"))
                    arguments["kind"] = context.RequiredArgument("kind");
                return arguments;
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["too-few-parameters"] = "needs at least two parameters to group",
                ["unknown-parameter"] = "has no parameter named",
                ["unsupported-parameter"] = "so it cannot join a parameter object",
                ["duplicate-parameter"] = "already has a parameter named",
                ["not-a-method"] = "is not an ordinary method",
                ["type-name-conflict"] = "is already visible where the type would be declared",
                ["method-group-reference"] = "is used as a method group",
                ["external-member"] = "which is declared outside the solution",
            }),
        new CatalogMapping(
            "preserve-whole-object",
            "preserve-whole-object",
            async context =>
            {
                var arguments = await MethodArguments(context);
                arguments["parameters"] = context.RequiredArgument("parameters");
                arguments["parameterName"] = context.RequiredArgument("parameterName");
                return arguments;
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["not-from-one-object"] = "from a member of an object",
                ["several-objects"] = "reads its arguments from more than one object",
                ["members-differ"] = "from different members",
                ["types-differ"] = "members of objects of different types",
                ["no-calls"] = "is never called",
                ["unknown-parameter"] = "has no parameter named",
                ["duplicate-parameter"] = "already has a parameter named",
                ["replaced-parameter-assigned"] = "so its uses cannot be replaced",
                ["method-group-reference"] = "is used as a method group",
                ["external-member"] = "which is declared outside the solution",
            }),
        new CatalogMapping(
            "separate-query-from-modifier",
            "separate-query-from-modifier",
            async context =>
            {
                var arguments = await MethodArguments(context);
                arguments["queryName"] = context.RequiredArgument("queryName");
                arguments["modifierName"] = context.RequiredArgument("modifierName");
                return arguments;
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["returns-nothing"] = "returns nothing, so it has no query to separate",
                ["no-modifier"] = "has no statements that change state",
                ["query-has-side-effects"] = "has side effects of its own",
                ["modifier-reads-result"] = "the value the query returns",
                ["returns-early"] = "returns before its last statement",
                ["polymorphic-method"] = "is virtual, an override or an interface implementation",
                ["expression-bodied-member"] = "is expression-bodied",
                ["call-in-expression"] = "is part of a larger expression",
                ["returned-before-modifier"] = "which the query must compute before the modifier runs",
                ["receiver-has-side-effects"] = "is made on an expression that would be evaluated twice",
                ["argument-has-side-effects"] = "passes an argument with side effects",
                ["method-group-reference"] = "is used as a method group",
                ["name-conflict"] = "already has a member named",
            }),
        new CatalogMapping(
            "parameterise-method",
            "parameterise-method",
            context => new Dictionary<string, JsonElement>
            {
                ["solutionPath"] = Json(context.SolutionPath),
                ["filePath"] = Json(context.TargetFilePath()),
                ["methods"] = context.RequiredArgument("methods"),
                ["name"] = context.RequiredArgument("name"),
                ["parameterNames"] = context.RequiredArgument("parameterNames"),
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["too-few-methods"] = "needs at least two similar methods",
                ["signatures-differ"] = "differ in their class, return type, parameters or static modifier",
                ["bodies-differ"] = "in more than literals of the same type",
                ["same-body"] = "have the same body, so there is no value to make a parameter",
                ["parameter-count"] = "parameter name(s) were given",
                ["no-block-body"] = "has no block body to compare",
                ["name-conflict"] = "is already in use in",
                ["polymorphic-method"] = "is virtual, an override or an interface implementation",
                ["method-group-reference"] = "is used without being called",
            }),
        new CatalogMapping(
            "replace-parameter-with-explicit-methods",
            "replace-parameter-with-explicit-methods",
            async context =>
            {
                var arguments = await MethodArguments(context);
                arguments["parameterName"] = context.RequiredArgument("parameter");
                arguments["methods"] = context.RequiredArgument("methods");
                return arguments;
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["not-a-dispatch"] = "does not start by choosing what to do from",
                ["unknown-value"] = "The method does not dispatch on the value",
                ["falls-through"] = "runs on into the rest of the method",
                ["empty-branch"] = "is empty, so there is nothing to give a method",
                ["no-values"] = "Name at least one value",
                ["polymorphic-method"] = "is virtual, an override or an interface implementation",
                ["not-in-class"] = "is not a block-bodied method of a class",
                ["unknown-parameter"] = "has no parameter named",
                ["assigned-local-used-after"] = "The extracted block assigns",
                ["name-conflict"] = "already has a member named",
            }),
        new CatalogMapping(
            "constructor-injection",
            "inject-constructor-dependency",
            async context =>
            {
                var (filePath, line, column) = await LocalPosition(context);
                var arguments = new Dictionary<string, JsonElement>
                {
                    ["solutionPath"] = Json(context.SolutionPath),
                    ["filePath"] = Json(filePath),
                    ["line"] = Json(line),
                    ["column"] = Json(column),
                };
                if (context.HasArgument("parameter"))
                    arguments["parameterName"] = context.RequiredArgument("parameter");
                if (context.HasArgument("field"))
                    arguments["fieldName"] = context.RequiredArgument("field");
                return arguments;
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["not-a-construction"] = "is not initialised by constructing an object",
                ["not-in-instance-method"] = "is not in an instance method of a class",
                ["not-declared-alone"] = "is not declared on its own in a block",
                ["assigned-after-declaration"] = "is assigned after its declaration",
                ["argument-depends-on-method"] = "depends on the method's state",
                ["object-initializer"] = "is constructed with an initializer",
                ["several-constructors"] = "has several constructors",
                ["chained-constructor"] = "calls another with this(...)",
                ["name-conflict"] = "already has a member named",
                ["duplicate-parameter"] = "The constructor already has a parameter named",
                ["not-a-local"] = "There is no local variable",
            }),
    };

    /// <summary>
    /// The file, name and line of the method <c>target.symbol</c> names. A constructor
    /// is named by its type.
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

    /// <summary>
    /// A local is targeted by a caret on it, or in a later step of a composite by the
    /// containing member's symbol with <c>arguments.local</c> naming it.
    /// </summary>
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
