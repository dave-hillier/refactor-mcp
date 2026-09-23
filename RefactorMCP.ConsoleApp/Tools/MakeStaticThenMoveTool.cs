using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using RefactorMCP.ConsoleApp.Tools;
using RefactorMCP.ConsoleApp.Tools.Composites;
using RefactorMCP.ConsoleApp.Tools.Moving;

[McpServerToolType]
public static class MakeStaticThenMoveTool
{
    [McpServerTool, Description("Convert an instance method to static and move it to another class (preferred for large C# file refactoring). " +
        "The method takes the instance as its first parameter, every call passes it, and the target class is created as a static class if it does not exist. " +
        "Refuses, changing nothing, if either step would.")]
    public static async Task<string> MakeStaticThenMove(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file containing the method")] string filePath,
        [Description("Name of the method to convert and move")] string methodName,
        [Description("Name of the target class")] string targetClass,
        [Description("Name for the instance parameter (optional, defaults to the class name in camel case)")] string? instanceParameterName = null,
        [Description("Path to the target file (optional, used when the target class has to be created)")] string? targetFilePath = null,
        [Description("Leave a static stub in the old class that delegates to the moved method (default true) instead of updating callers")] bool keepStub = true,
        [Description("Line of the method's declaration (1-based, optional), to choose between overloads")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var document = MovingSupport.DocumentOrThrow(solution, filePath);
        var method = (IMethodSymbol)await MovingSupport.FindDeclaredSymbolAsync(
            document, methodName, line, s => s is IMethodSymbol { MethodKind: MethodKind.Ordinary }, "method", cancellationToken);
        var typeId = method.ContainingType.GetDocumentationCommentId()!;
        var parameterTypes = method.Parameters.Select(p => p.Type).ToList();

        await CompositeRecipe.RunAsync(solutionPath, async recipe =>
        {
            await recipe.StepAsync("make-method-static", () => MakeMethodStaticTool.MakeMethodStatic(
                solutionPath, filePath, methodName, parameterName: instanceParameterName, line: line, cancellationToken: cancellationToken));

            var moved = await StaticVersionAsync(recipe, typeId, methodName, parameterTypes, cancellationToken);
            await recipe.StepAsync("move-static-method", () => MoveMemberTool.MoveMember(
                solutionPath,
                moved.FilePath,
                methodName,
                targetType: targetClass,
                keepStub: keepStub,
                targetFilePath: targetFilePath,
                line: moved.Line,
                kind: "static-method",
                cancellationToken: cancellationToken));
        }, cancellationToken);

        return $"Successfully made {methodName} static and moved it to {targetClass}";
    }

    /// <summary>
    /// The method once it is static: the overload whose parameters are the old
    /// ones, preceded by the instance when the method used it.
    /// </summary>
    private static async Task<CompositeTarget> StaticVersionAsync(
        CompositeRecipe recipe,
        string typeId,
        string methodName,
        System.Collections.Generic.IReadOnlyList<ITypeSymbol> oldParameters,
        CancellationToken cancellationToken)
    {
        var type = (INamedTypeSymbol)(await recipe.FindAsync(typeId, cancellationToken)).Symbol;
        var method = type.GetMembers(methodName).OfType<IMethodSymbol>().First(m =>
            m.IsStatic
            && m.Parameters.Length - oldParameters.Count is 0 or 1
            && m.Parameters.Skip(m.Parameters.Length - oldParameters.Count).Select(p => p.Type.ToDisplayString())
                .SequenceEqual(oldParameters.Select(p => p.ToDisplayString())));
        var location = method.Locations.First(l => l.IsInSource);
        return new CompositeTarget(method, location.SourceTree!.FilePath, location.GetLineSpan().StartLinePosition.Line + 1, location);
    }
}
