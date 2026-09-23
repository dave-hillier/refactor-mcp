using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
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
    };

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
