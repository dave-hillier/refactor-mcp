using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[McpServerToolType]
public static class RemoveUnusedParameterTool
{
    [McpServerTool, Description("Remove a parameter no body reads from a method, its overrides and implementations, and the argument every call passes for it")]
    public static async Task<string> RemoveUnusedParameter(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the method")] string filePath,
        [Description("Name of the method, or of the type for a constructor")] string methodName,
        [Description("Name of the parameter to remove")] string parameterName,
        [Description("A line of the method's declaration (1-based), to choose between overloads")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var method = await SolutionEdits.FindMethodAsync(solution, filePath, methodName, line, cancellationToken);
            var parameter = SolutionEdits.FindParameter(method, parameterName);

            var family = await MethodFamily.FindAsync(solution, method, cancellationToken);
            foreach (var site in await SignatureChange.CallSitesAsync(solution, family, cancellationToken))
            {
                foreach (var argument in site.ArgumentsFor(parameter.Ordinal))
                {
                    if (MayHaveSideEffects(argument.Expression))
                        throw new McpException(
                            $"Error: The argument '{argument.Expression}' at {SolutionEdits.Describe(argument.GetLocation())} may have side effects, which removing it would lose");
                }
            }

            var slots = method.Parameters
                .Where(p => p.Ordinal != parameter.Ordinal)
                .Select(p => ParameterSlot.Existing(p.Ordinal))
                .ToList();
            var changed = await SignatureChange.ApplyAsync(solution, method, slots, cancellationToken);
            await SolutionEdits.EnsureCompilesAsync(solution, changed, cancellationToken);
            await SolutionEdits.WriteAsync(solution, changed, cancellationToken);

            return $"Successfully removed parameter '{parameterName}' from '{methodName}'";
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error removing parameter: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Calls, object creation, assignments, increments and awaits may do
    /// work beyond producing a value. Property reads are assumed not to.
    /// </summary>
    private static bool MayHaveSideEffects(ExpressionSyntax expression) =>
        expression.DescendantNodesAndSelf().Any(node => node switch
        {
            InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" } } => false,
            InvocationExpressionSyntax or BaseObjectCreationExpressionSyntax or AssignmentExpressionSyntax
                or AwaitExpressionSyntax => true,
            PrefixUnaryExpressionSyntax unary => unary.IsKind(SyntaxKind.PreIncrementExpression)
                || unary.IsKind(SyntaxKind.PreDecrementExpression),
            PostfixUnaryExpressionSyntax postfix => postfix.IsKind(SyntaxKind.PostIncrementExpression)
                || postfix.IsKind(SyntaxKind.PostDecrementExpression),
            _ => false,
        });
}
