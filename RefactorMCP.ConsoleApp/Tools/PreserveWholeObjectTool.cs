using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[McpServerToolType]
public static class PreserveWholeObjectTool
{
    [McpServerTool, Description("Replace parameters that every call fills from members of one object with a parameter taking the object itself")]
    public static async Task<string> PreserveWholeObject(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file declaring the method")] string filePath,
        [Description("Name of the method, or of the type for a constructor")] string methodName,
        [Description("The parameters every call fills from members of the same object")] string[] parameters,
        [Description("Name of the parameter that takes the object")] string parameterName,
        [Description("A line of the method's declaration (1-based), to choose between overloads")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var method = (await SolutionEdits.FindMethodAsync(solution, filePath, methodName, line, cancellationToken)).OriginalDefinition;
            var replaced = parameters.Select(name => SolutionEdits.FindParameter(method, name)).ToList();
            if (method.Parameters.Any(p => p.Name == parameterName && !replaced.Contains(p, SymbolEqualityComparer.Default)))
                throw new McpException($"Error: '{method.Name}' already has a parameter named '{parameterName}'");

            var family = await MethodFamily.FindAsync(solution, method, cancellationToken);
            var sites = await SignatureChange.CallSitesAsync(solution, family, cancellationToken);
            if (sites.Count == 0)
                throw new McpException($"Error: '{method.Name}' is never called, so there is no object to take instead of its parameters");

            var (objectType, members) = SourceObject(sites, replaced);
            var location = method.Locations.First(l => l.IsInSource);
            var model = (await solution.GetDocument(location.SourceTree)!.GetSemanticModelAsync(cancellationToken))!;
            var typeName = objectType.ToMinimalDisplayString(model, location.SourceSpan.Start);

            var replacements = replaced.ToDictionary(
                p => p.Ordinal,
                p => (ExpressionSyntax)SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(parameterName),
                    SyntaxFactory.IdentifierName(members[p.Ordinal])));
            var (edited, resolved) = await ChangeSignatureTool.ReplaceParameterUsesAsync(solution, method, replacements, cancellationToken);

            // The object's parameter takes the place of the first parameter it replaces.
            var first = replaced.Min(p => p.Ordinal);
            var declaration = ParameterSlot.ParseDeclaration(typeName, parameterName, null);
            var slots = new List<ParameterSlot>();
            foreach (var parameter in resolved.Parameters)
            {
                if (parameter.Ordinal == first)
                    slots.Add(ParameterSlot.Added(declaration, site => SourceOf(site, first)));
                else if (replaced.All(r => r.Ordinal != parameter.Ordinal))
                    slots.Add(ParameterSlot.Existing(parameter.Ordinal));
            }

            var changed = await SignatureChange.ApplyAsync(edited, resolved, slots, cancellationToken);
            await SolutionEdits.EnsureCompilesAsync(solution, changed, cancellationToken);
            await SolutionEdits.WriteAsync(solution, changed, cancellationToken);

            return $"Successfully replaced ({string.Join(", ", parameters)}) of '{methodName}' with '{typeName} {parameterName}'";
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error preserving whole object: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// The type of the object every call reads the arguments from, and the member each
    /// replaced parameter is read from. At every call, each argument for a replaced
    /// parameter must read the same member of one simple expression.
    /// </summary>
    private static (ITypeSymbol Type, Dictionary<int, string> Members) SourceObject(
        IReadOnlyList<SignatureCallSite> sites,
        IReadOnlyList<IParameterSymbol> replaced)
    {
        ITypeSymbol? type = null;
        var members = new Dictionary<int, ISymbol>();
        foreach (var site in sites)
        {
            string? source = null;
            foreach (var parameter in replaced)
            {
                var argument = site.ArgumentFor(parameter.Ordinal);
                var access = argument as MemberAccessExpressionSyntax;
                var member = access is null ? null : site.Model.GetSymbolInfo(access).Symbol;
                if (access is null
                    || !access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
                    || !ExpressionFacts.IsSimple(access.Expression)
                    || member is not (IPropertySymbol or IFieldSymbol))
                {
                    throw new McpException(
                        $"Error: The call at {SolutionEdits.Describe(site.Call.GetLocation())} does not read '{parameter.Name}' from a member of an object");
                }

                var objectType = site.Model.GetTypeInfo(access.Expression).Type;
                source ??= access.Expression.ToString();
                if (access.Expression.ToString() != source)
                    throw new McpException(
                        $"Error: The call at {SolutionEdits.Describe(site.Call.GetLocation())} reads its arguments from more than one object");

                if (type is not null && !SymbolEqualityComparer.Default.Equals(type, objectType))
                    throw new McpException(
                        $"Error: Calls pass members of objects of different types, '{type.Name}' and '{objectType?.Name}'");
                type = objectType;

                if (members.TryGetValue(parameter.Ordinal, out var earlier) && !SymbolEqualityComparer.Default.Equals(earlier, member))
                    throw new McpException(
                        $"Error: Calls fill '{parameter.Name}' from different members, '{earlier.Name}' and '{member.Name}'");
                members[parameter.Ordinal] = member;
            }
        }

        return (type!, members.ToDictionary(m => m.Key, m => m.Value.Name));
    }

    /// <summary>The object a call reads the replaced arguments from.</summary>
    private static ExpressionSyntax SourceOf(SignatureCallSite site, int ordinal) =>
        ((MemberAccessExpressionSyntax)site.ArgumentFor(ordinal)!).Expression.WithoutTrivia();
}
