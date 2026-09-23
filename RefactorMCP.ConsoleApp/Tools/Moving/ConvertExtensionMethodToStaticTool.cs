using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Server;

namespace RefactorMCP.ConsoleApp.Tools.Moving;

[McpServerToolType]
public static class ConvertExtensionMethodToStaticTool
{
    [McpServerTool, Description("Convert an extension method to an ordinary static method by removing 'this' " +
        "from its first parameter; extension-style calls across the solution become static calls")]
    public static async Task<string> ConvertExtensionMethodToStatic(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file declaring the method")] string filePath,
        [Description("Name of the extension method")] string methodName,
        [Description("Line of the method's declaration (1-based, optional), to choose between overloads")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var document = MovingSupport.DocumentOrThrow(solution, filePath);
        var method = (IMethodSymbol)await MovingSupport.FindDeclaredSymbolAsync(
            document, methodName, line, s => s is IMethodSymbol { MethodKind: MethodKind.Ordinary }, "method", cancellationToken);

        return await ExtensionMethodConversions.ToStaticAsync(solution, method, cancellationToken);
    }
}
