using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorMCP.ConsoleApp.Tools.Composites;

[McpServerToolType]
public static class IntroduceParameterObjectTool
{
    [McpServerTool, Description("Replace a group of a method's parameters with one parameter of a new record holding them, updating the body and every call")]
    public static async Task<string> IntroduceParameterObject(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the method")] string filePath,
        [Description("Name of the method")] string methodName,
        [Description("The parameters to group, in the order the record declares them")] string[] parameters,
        [Description("Name of the new record")] string typeName,
        [Description("Name of the parameter that replaces the group")] string parameterName,
        [Description("struct for a readonly record struct (default), class for a sealed record")] string kind = "struct",
        [Description("A line of the method's declaration (1-based), to choose between overloads")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var method = await SolutionEdits.FindMethodAsync(solution, filePath, methodName, line, cancellationToken);
            if (method.MethodKind != MethodKind.Ordinary)
                throw new McpException($"Error: '{methodName}' is not an ordinary method, so its parameters cannot become a record");

            var grouped = Grouped(method, parameters, parameterName);
            var location = method.Locations.First(l => l.IsInSource);
            var model = (await solution.GetDocument(location.SourceTree)!.GetSemanticModelAsync(cancellationToken))!;

            await CompositeRecipe.RunAsync(solutionPath, async recipe =>
            {
                // The group first becomes a tuple, read element by element in the body
                // and built from each call's own arguments.
                await recipe.StepAsync("change-signature", async () =>
                {
                    var tupleType = "(" + string.Join(", ", grouped.Select(p =>
                        $"{p.Type.ToMinimalDisplayString(model, location.SourceSpan.Start)} {p.Name}")) + ")";
                    var replacements = grouped.ToDictionary(
                        p => p.Ordinal,
                        p => (ExpressionSyntax)SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName(parameterName),
                            SyntaxFactory.IdentifierName(p.Name)));

                    var (replaced, resolved) = await ChangeSignatureTool.ReplaceParameterUsesAsync(solution, method, replacements, cancellationToken);
                    var declaration = ParameterSlot.ParseDeclaration(tupleType, parameterName, null);

                    // The new parameter takes the place of the first of the group.
                    var first = grouped.Min(p => p.Ordinal);
                    var slots = new List<ParameterSlot>();
                    foreach (var parameter in resolved.Parameters)
                    {
                        if (parameter.Ordinal == first)
                            slots.Add(ParameterSlot.Added(declaration, site => Tuple(site, grouped)));
                        else if (grouped.All(g => g.Ordinal != parameter.Ordinal))
                            slots.Add(ParameterSlot.Existing(parameter.Ordinal));
                    }

                    var changed = await SignatureChange.ApplyAsync(replaced, resolved, slots, cancellationToken);
                    await SolutionEdits.EnsureCompilesAsync(solution, changed, cancellationToken);
                    await SolutionEdits.WriteAsync(solution, changed, cancellationToken);
                });

                // Convert Tuple to Named Type then turns the tuple into the record. The
                // declaration still starts on the line it did.
                await recipe.StepAsync("convert-tuple-to-named-type", () => ConvertTupleToNamedTypeTool.ConvertTupleToNamedType(
                    solutionPath,
                    location.SourceTree!.FilePath,
                    method.Name,
                    typeName,
                    parameterName,
                    kind,
                    location.GetLineSpan().StartLinePosition.Line + 1,
                    cancellationToken));
            }, cancellationToken);

            return $"Successfully replaced ({string.Join(", ", parameters)}) of '{methodName}' with '{typeName} {parameterName}'";
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error introducing parameter object: {ex.Message}", ex);
        }
    }

    /// <summary>The parameters to group, in the order given, each passed by value and required.</summary>
    private static List<IParameterSymbol> Grouped(IMethodSymbol method, string[] names, string parameterName)
    {
        if (names.Length < 2)
            throw new McpException("Error: A parameter object needs at least two parameters to group");

        var grouped = names.Select(name => SolutionEdits.FindParameter(method, name)).ToList();
        if (grouped.Select(p => p.Ordinal).Distinct().Count() != grouped.Count)
            throw new McpException("Error: A parameter is listed more than once");

        foreach (var parameter in grouped)
        {
            if (parameter.RefKind != RefKind.None || parameter.IsParams || parameter.HasExplicitDefaultValue || parameter.IsThis)
                throw new McpException(
                    $"Error: Parameter '{parameter.Name}' is ref, out, params, optional or the this of an extension method, so it cannot join a parameter object");
        }

        if (method.Parameters.Any(p => p.Name == parameterName && !grouped.Contains(p, SymbolEqualityComparer.Default)))
            throw new McpException($"Error: '{method.Name}' already has a parameter named '{parameterName}'");

        return grouped;
    }

    /// <summary>The call's arguments for the grouped parameters, as a tuple.</summary>
    private static ExpressionSyntax Tuple(SignatureCallSite site, IReadOnlyList<IParameterSymbol> grouped) =>
        SyntaxFactory.TupleExpression(SyntaxFactory.SeparatedList(
            grouped.Select(p => SyntaxFactory.Argument(site.ArgumentFor(p.Ordinal)!.WithoutTrivia())),
            Enumerable.Repeat(SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space), grouped.Count - 1)));
}
