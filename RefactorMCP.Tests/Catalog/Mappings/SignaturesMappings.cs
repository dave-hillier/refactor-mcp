using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static RefactorMCP.Tests.Catalog.CatalogMapping;

namespace RefactorMCP.Tests.Catalog.Mappings;

/// <summary>Primitives from the catalog's "Signatures" group.</summary>
internal sealed class SignaturesMappings : ICatalogMappings
{
    public IEnumerable<CatalogMapping> Mappings => new[]
    {
        new CatalogMapping(
            "change-signature",
            "change-signature",
            async context => With(await MemberArguments(context, "methodName"), ("parameters", context.RequiredArgument("parameters"))),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["removed-parameter-in-use"] = "so it cannot be removed",
                ["unknown-parameter"] = "give a type to add it as a new parameter",
                ["duplicate-parameter"] = "already has a parameter named",
                ["missing-value"] = "needs a value for existing calls or a default",
                ["optional-before-required"] = "would follow optional parameter",
                ["params-not-last"] = "must stay last",
                ["method-group-reference"] = "is used as a method group",
                ["external-member"] = "which is declared outside the solution",
                ["extension-this-moved"] = "of an extension method must stay first",
            }),
        new CatalogMapping(
            "introduce-parameter",
            "introduce-parameter",
            context => new Dictionary<string, JsonElement>
            {
                ["solutionPath"] = Json(context.SolutionPath),
                ["filePath"] = Json(context.TargetFilePath()),
                ["methodName"] = Json(MethodAroundSelection(context)),
                ["selectionRange"] = Json(context.SelectionRange()),
                ["parameterName"] = context.RequiredArgument("name"),
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["not-an-expression"] = "Selected code is not a valid expression",
                ["references-local"] = "uses the local",
                ["references-instance-member"] = "uses the instance member",
                ["references-type-parameter"] = "type parameter",
                ["name-conflict"] = "is already in use in",
            }),
        new CatalogMapping(
            "inline-parameter",
            "inline-parameter",
            async context => With(await MemberArguments(context, "methodName"), ("parameterName", context.RequiredArgument("parameter"))),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["values-differ"] = "Calls pass different values for",
                ["not-constant"] = "which is not a constant",
                ["parameter-assigned"] = "is assigned in the body",
                ["no-calls"] = "so there is no value to inline",
                ["part-of-hierarchy"] = "is part of an inheritance or interface hierarchy",
                ["method-group-reference"] = "is used as a method group",
            }),
        new CatalogMapping(
            "remove-unused-parameter",
            "remove-unused-parameter",
            async context => With(await MemberArguments(context, "methodName"), ("parameterName", context.RequiredArgument("parameter"))),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["parameter-in-use"] = "so it cannot be removed",
                ["argument-has-side-effects"] = "may have side effects",
                ["method-group-reference"] = "is used as a method group",
                ["external-member"] = "which is declared outside the solution",
            }),
        new CatalogMapping(
            "add-parameter-default-value",
            "add-parameter-default-value",
            async context => With(
                await MemberArguments(context, "methodName"),
                ("parameterName", context.RequiredArgument("parameter")),
                ("value", context.RequiredArgument("value")),
                ("removeFromCallSites", context.HasArgument("removeFromCallSites")
                    ? context.RequiredArgument("removeFromCallSites")
                    : Json(false))),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["already-optional"] = "already has a default value",
                ["ref-or-out-parameter"] = "which cannot have a default value",
                ["later-parameter-required"] = "has no default value, so",
                ["not-constant"] = "is not a compile-time constant",
                ["incompatible-value"] = "cannot be converted to the type of",
            }),
    };

    /// <summary>
    /// The name of the method or constructor containing the selection, which
    /// the tool takes as well as the selection.
    /// </summary>
    private static string MethodAroundSelection(StepContext context)
    {
        var text = SourceText.From(File.ReadAllText(context.TargetFilePath()));
        var root = CSharpSyntaxTree.ParseText(text).GetRoot();
        var range = context.SelectionRange().Split('-', ':');
        var start = text.Lines[int.Parse(range[0]) - 1].Start + int.Parse(range[1]) - 1;

        return root.FindToken(start).Parent!.AncestorsAndSelf().OfType<BaseMethodDeclarationSyntax>().FirstOrDefault() switch
        {
            MethodDeclarationSyntax method => method.Identifier.ValueText,
            ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
            _ => throw new InvalidOperationException("The selection is not inside a method"),
        };
    }

    /// <summary>
    /// The file, name and line of the declaration <c>target.symbol</c> names.
    /// A constructor is named by its type.
    /// </summary>
    private static async Task<Dictionary<string, JsonElement>> MemberArguments(StepContext context, string nameParameter)
    {
        var location = await context.SymbolLocationAsync();
        var name = location.Symbol is IMethodSymbol { MethodKind: MethodKind.Constructor } constructor
            ? constructor.ContainingType.Name
            : location.Symbol.Name;

        return new Dictionary<string, JsonElement>
        {
            ["solutionPath"] = Json(context.SolutionPath),
            ["filePath"] = Json(location.FilePath),
            [nameParameter] = Json(name),
            ["line"] = Json(location.Line),
        };
    }

    private static Dictionary<string, JsonElement> With(
        Dictionary<string, JsonElement> arguments,
        params (string Name, JsonElement Value)[] more)
    {
        foreach (var (name, value) in more)
            arguments[name] = value;

        return arguments;
    }
}
