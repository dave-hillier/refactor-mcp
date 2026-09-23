using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using static RefactorMCP.Tests.Catalog.CatalogMapping;

namespace RefactorMCP.Tests.Catalog.Mappings;

/// <summary>
/// Primitives that the method composites' recipes need: redirecting calls that
/// pass a constant, initialising a field from a constructor parameter,
/// replacing an expression with a field, and making a method async.
/// </summary>
internal sealed class MethodRecipePrimitivesMappings : ICatalogMappings
{
    public IEnumerable<CatalogMapping> Mappings => new[]
    {
        new CatalogMapping(
            "redirect-calls-with-constant-argument",
            "redirect-calls-with-constant-argument",
            async context =>
            {
                var arguments = await MethodArguments(context);
                arguments["parameterName"] = context.RequiredArgument("parameter");
                arguments["value"] = context.RequiredArgument("value");
                arguments["targetMethodName"] = context.RequiredArgument("method");
                return arguments;
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["not-a-method"] = "is not an ordinary method with a block body",
                ["polymorphic-method"] = "is virtual, an override or an interface implementation",
                ["generic-method"] = "is generic",
                ["unknown-parameter"] = "has no parameter named",
                ["not-a-constant"] = "is not a constant of type",
                ["unknown-method"] = "has no method named",
                ["does-more-than-call"] = "does more than call",
                ["does-not-compile"] = "The change would not compile",
            }),
        new CatalogMapping(
            "initialize-field-from-constructor-parameter",
            "initialize-field-from-constructor-parameter",
            async context =>
            {
                var arguments = await MethodArguments(context);
                arguments["className"] = arguments["methodName"];
                arguments.Remove("methodName");
                arguments["fieldName"] = context.RequiredArgument("field");
                arguments["parameterName"] = context.RequiredArgument("parameter");
                return arguments;
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["not-a-constructor"] = "is not an instance constructor with a body",
                ["unknown-parameter"] = "has no parameter named",
                ["unknown-field"] = "has no instance field named",
                ["type-mismatch"] = "cannot hold",
                ["field-in-use"] = "is already used",
                ["returns-early"] = "returns before its end",
            }),
        new CatalogMapping(
            "replace-expression-with-field",
            "replace-expression-with-field",
            async context =>
            {
                if (context.Step.Target?.Symbol is null)
                {
                    return new Dictionary<string, JsonElement>
                    {
                        ["solutionPath"] = Json(context.SolutionPath),
                        ["filePath"] = Json(context.TargetFilePath()),
                        ["selectionRange"] = Json(context.SelectionRange()),
                        ["fieldName"] = context.RequiredArgument("field"),
                    };
                }

                var arguments = await MethodArguments(context);
                arguments["memberName"] = arguments["methodName"];
                arguments.Remove("methodName");
                arguments["expression"] = context.RequiredArgument("expression");
                arguments["fieldName"] = context.RequiredArgument("field");
                return arguments;
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["not-an-expression"] = "The selection is not an expression",
                ["expression-not-found"] = "does not contain the expression",
                ["unknown-field"] = "has no instance field named",
                ["field-not-readonly"] = "is not readonly",
                ["type-mismatch"] = "is not of the expression's type",
                ["not-in-instance-member"] = "is not in an instance method, property or accessor of",
                ["not-a-fixed-value"] = "is not built only from constants and constructions",
                ["field-not-from-parameter"] = "is not assigned from a parameter in every constructor",
                ["used-during-construction"] = "could run before",
                ["different-value-passed"] = "passes a different value",
            }),
        new CatalogMapping(
            "make-method-async",
            "make-method-async",
            MethodArguments,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["nothing-to-await"] = "does not block on a task, so it has nothing to await",
                ["already-async"] = "already returns a task",
                ["not-a-method"] = "is not an ordinary method",
                ["polymorphic-method"] = "is virtual, an override or an interface implementation",
                ["ref-parameters"] = "has ref, out or in parameters",
                ["iterator"] = "is an iterator",
                ["method-group-reference"] = "is used as a method group",
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
}
