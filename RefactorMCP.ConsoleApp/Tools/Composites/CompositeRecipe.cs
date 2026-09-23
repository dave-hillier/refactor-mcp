using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using ModelContextProtocol;
using RefactorMCP.ConsoleApp.Tools.Moving;

namespace RefactorMCP.ConsoleApp.Tools.Composites;

/// <summary>
/// Runs a composite refactoring as its recipe: primitive tools called one
/// after another, each writing its change before the next looks. When a step
/// refuses, every file and the session's solution are put back as they were
/// before the first step, and the refusal names the step.
/// </summary>
internal sealed class CompositeRecipe
{
    private readonly Solution _original;
    private int _steps;

    private CompositeRecipe(string solutionPath, Solution original)
    {
        SolutionPath = solutionPath;
        _original = original;
    }

    public string SolutionPath { get; }

    public static async Task RunAsync(
        string solutionPath,
        Func<CompositeRecipe, Task> steps,
        CancellationToken cancellationToken)
    {
        var original = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var recipe = new CompositeRecipe(solutionPath, original);
        try
        {
            await steps(recipe);
        }
        catch
        {
            await recipe.RollBackAsync();
            throw;
        }
    }

    /// <summary>Runs one primitive, naming it in the error if it refuses.</summary>
    public async Task StepAsync(string refactoring, Func<Task> step)
    {
        _steps++;
        try
        {
            await step();
        }
        catch (McpException ex)
        {
            throw new McpException($"Error: step {_steps} ({refactoring}) refused: {WithoutPrefix(ex.Message)}", ex);
        }
    }

    /// <summary>The solution as the steps so far have left it.</summary>
    public Task<Solution> CurrentAsync(CancellationToken cancellationToken = default) =>
        RefactoringHelpers.GetOrLoadSolution(SolutionPath, cancellationToken);

    /// <summary>
    /// Finds a declaration again after earlier steps have moved or rewritten
    /// it, by its documentation comment id.
    /// </summary>
    public async Task<CompositeTarget> FindAsync(string documentationId, CancellationToken cancellationToken = default)
    {
        var solution = await CurrentAsync(cancellationToken);
        foreach (var project in solution.Projects)
        {
            var compilation = await project.GetCompilationAsync(cancellationToken);
            var symbol = compilation is null ? null : DocumentationCommentId.GetFirstSymbolForDeclarationId(documentationId, compilation);
            var location = symbol?.Locations.FirstOrDefault(l => l.IsInSource);
            if (symbol is not null && location is not null)
                return new CompositeTarget(symbol, location.SourceTree!.FilePath, location.GetLineSpan().StartLinePosition.Line + 1, location);
        }

        throw new McpException($"Error: '{documentationId}' is no longer declared in the solution");
    }

    private async Task RollBackAsync()
    {
        var current = await CurrentAsync(CancellationToken.None);
        await MovingSupport.ApplyAsync(current, _original, CancellationToken.None);
    }

    private static string WithoutPrefix(string message)
    {
        // Some tools wrap a refusal in their own "Error doing X: Error: ..." prefix.
        var index = message.LastIndexOf("Error: ", StringComparison.Ordinal);
        return index >= 0 ? message[(index + "Error: ".Length)..] : message;
    }
}

/// <summary>A declaration as the step tools take it: its file and the line of its name.</summary>
internal sealed record CompositeTarget(ISymbol Symbol, string FilePath, int Line, Location Location)
{
    /// <summary>The name's span as a 1-based, end-exclusive selection range.</summary>
    public string NameRange()
    {
        var span = Location.GetLineSpan();
        return $"{span.StartLinePosition.Line + 1}:{span.StartLinePosition.Character + 1}-{span.EndLinePosition.Line + 1}:{span.EndLinePosition.Character + 1}";
    }
}
