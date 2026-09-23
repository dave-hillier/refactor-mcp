using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[McpServerToolType]
public static class InlineParameterTool
{
    [McpServerTool, Description("Remove a parameter every call passes the same constant for, using the constant in the method body instead")]
    public static async Task<string> InlineParameter(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file declaring the method")] string filePath,
        [Description("Name of the method, or of the type for a constructor")] string methodName,
        [Description("Name of the parameter to inline")] string parameterName,
        [Description("A line of the method's declaration (1-based), to choose between overloads")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var method = await SolutionEdits.FindMethodAsync(solution, filePath, methodName, line, cancellationToken);
            var parameter = SolutionEdits.FindParameter(method, parameterName);

            var family = await MethodFamily.FindAsync(solution, method, cancellationToken);
            if (family.Count > 1 || method.IsVirtual || method.IsAbstract || method.IsOverride)
                throw new McpException(
                    $"Error: '{method.Name}' is part of an inheritance or interface hierarchy, whose other members have bodies of their own");

            if (parameter.RefKind != RefKind.None || parameter.IsParams)
                throw new McpException(
                    $"Error: Parameter '{parameter.Name}' is passed by reference or as a params array, so it has no single value to inline");

            var uses = await ParameterUsage.UsesAsync(solution, method, parameter, cancellationToken);
            if (uses.Any(IsWritten))
                throw new McpException(
                    $"Error: Parameter '{parameter.Name}' is assigned in the body, so it has no single value to inline");

            var sites = await SignatureChange.CallSitesAsync(solution, family, cancellationToken);
            if (sites.Count == 0)
                throw new McpException($"Error: Nothing calls '{method.Name}', so there is no value to inline for '{parameter.Name}'");

            var value = await SingleValueAsync(solution, sites, parameter, cancellationToken);

            var inlined = solution;
            foreach (var group in uses.GroupBy(u => u.SyntaxTree))
            {
                var document = solution.GetDocument(group.Key)!;
                var root = await group.Key.GetRootAsync(cancellationToken);
                inlined = inlined.WithDocumentSyntaxRoot(
                    document.Id,
                    root.ReplaceNodes(group, (original, _) => ExpressionPlacement.Fit(value, original)));
            }

            var project = SolutionEdits.ProjectOf(solution, method);
            var inlinedMethod = await SolutionEdits.ResolveAsync(inlined, project, method, cancellationToken);
            var slots = method.Parameters
                .Where(p => p.Ordinal != parameter.Ordinal)
                .Select(p => ParameterSlot.Existing(p.Ordinal))
                .ToList();
            var changed = await SignatureChange.ApplyAsync(inlined, inlinedMethod, slots, cancellationToken);
            await SolutionEdits.EnsureCompilesAsync(solution, changed, cancellationToken);
            await SolutionEdits.WriteAsync(solution, changed, cancellationToken);

            return $"Successfully inlined parameter '{parameterName}' of '{methodName}' as {value}";
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error inlining parameter: {ex.Message}", ex);
        }
    }

    private static bool IsWritten(IdentifierNameSyntax identifier) => identifier.Parent switch
    {
        AssignmentExpressionSyntax assignment => assignment.Left == identifier,
        PrefixUnaryExpressionSyntax unary => unary.IsKind(SyntaxKind.PreIncrementExpression) || unary.IsKind(SyntaxKind.PreDecrementExpression),
        PostfixUnaryExpressionSyntax => true,
        ArgumentSyntax argument => !argument.RefKindKeyword.IsKind(SyntaxKind.None),
        RefExpressionSyntax => true,
        _ => false,
    };

    /// <summary>
    /// The constant every call passes, as the body should write it: the
    /// expression a call wrote, or the parameter's default for calls that
    /// leave it out, cast when its type is not the parameter's own.
    /// </summary>
    private static async Task<ExpressionSyntax> SingleValueAsync(
        Solution solution,
        IReadOnlyList<SignatureCallSite> sites,
        IParameterSymbol parameter,
        CancellationToken cancellationToken)
    {
        ExpressionSyntax? chosen = null;
        SemanticModel? chosenModel = null;
        object? chosenValue = null;

        foreach (var site in sites)
        {
            var (expression, model) = site.ArgumentsFor(parameter.Ordinal).Count == 0
                ? await DefaultValueAsync(solution, parameter, cancellationToken)
                : (site.ArgumentFor(parameter.Ordinal)!, site.Model);

            var constant = model.GetConstantValue(expression, cancellationToken);
            if (!constant.HasValue)
                throw new McpException(
                    $"Error: The call at {SolutionEdits.Describe(site.Call.GetLocation())} passes '{expression}' for '{parameter.Name}', which is not a constant");

            if (chosen is null)
            {
                (chosen, chosenModel, chosenValue) = (expression, model, constant.Value);
            }
            else if (!ConstantValues.Same(chosenValue, constant.Value))
            {
                throw new McpException(
                    $"Error: Calls pass different values for '{parameter.Name}': '{chosen}' and '{expression}'");
            }
        }

        var type = chosenModel!.GetTypeInfo(chosen!, cancellationToken).Type;
        if (type is null || SymbolEqualityComparer.Default.Equals(type, parameter.Type))
            return chosen!.WithoutTrivia();

        var declaration = parameter.Locations.First(l => l.IsInSource);
        var declarationModel = await solution.GetDocument(declaration.SourceTree)!.GetSemanticModelAsync(cancellationToken);
        var typeName = parameter.Type.ToMinimalDisplayString(declarationModel!, declaration.SourceSpan.Start);
        return ExpressionPlacement.Cast(SyntaxFactory.ParseTypeName(typeName), chosen!);
    }

    private static async Task<(ExpressionSyntax Expression, SemanticModel Model)> DefaultValueAsync(
        Solution solution,
        IParameterSymbol parameter,
        CancellationToken cancellationToken)
    {
        var reference = parameter.DeclaringSyntaxReferences.First();
        var syntax = (ParameterSyntax)await reference.GetSyntaxAsync(cancellationToken);
        var model = await solution.GetDocument(reference.SyntaxTree)!.GetSemanticModelAsync(cancellationToken);
        return syntax.Default is { } clause
            ? (clause.Value, model!)
            : throw new McpException($"Error: A call leaves out '{parameter.Name}', which has no default to inline");
    }
}
