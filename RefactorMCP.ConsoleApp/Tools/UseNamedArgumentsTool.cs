using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[McpServerToolType]
public static class UseNamedArgumentsTool
{
    [McpServerTool, Description("Name the arguments of the call at a position, leaving arguments already named and those passed to a params array")]
    public static async Task<string> UseNamedArguments(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file containing the call")] string filePath,
        [Description("Line inside the call (1-based)")] int line,
        [Description("Column inside the call (1-based)")] int column,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var document = RefactoringHelpers.GetDocumentByPath(solution, filePath)
                ?? throw new McpException($"Error: File {filePath} not found in solution");
            var text = await document.GetTextAsync(cancellationToken);
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var model = await document.GetSemanticModelAsync(cancellationToken);

            if (line < 1 || line > text.Lines.Count)
                throw new McpException($"Error: Line {line} is outside {filePath}");
            var position = text.Lines[line - 1].Start + column - 1;
            var call = root!.FindToken(position).Parent?.AncestorsAndSelf()
                .FirstOrDefault(n => SignatureChange.ArgumentListOf(n) is not null)
                ?? throw new McpException($"Error: There is no call at {line}:{column}");

            var method = model!.GetSymbolInfo(call, cancellationToken).Symbol as IMethodSymbol
                ?? throw new McpException($"Error: The call at {line}:{column} does not bind to a single method");
            var arguments = SignatureChange.ArgumentListOf(call)!;
            if (arguments.Arguments.Count == 0)
                throw new McpException($"Error: The call to '{method.Name}' has no arguments to name");

            var named = arguments.Arguments.Select((argument, index) => NameIfPositional(argument, index, method)).ToList();
            if (named.SequenceEqual(arguments.Arguments))
                throw new McpException($"Error: Every argument of the call to '{method.Name}' that can be named already is");

            var marker = new SyntaxAnnotation();
            var newArguments = arguments.WithArguments(SyntaxFactory.SeparatedList(named, arguments.Arguments.GetSeparators()));
            var newRoot = root.ReplaceNode(call, call.ReplaceNode(arguments, newArguments).WithAdditionalAnnotations(marker));
            var changed = solution.WithDocumentSyntaxRoot(document.Id, newRoot);

            await EnsureSameMethodAsync(changed.GetDocument(document.Id)!, marker, method, cancellationToken);
            await SolutionEdits.EnsureCompilesAsync(solution, changed, cancellationToken);
            await SolutionEdits.WriteAsync(solution, changed, cancellationToken);

            return $"Successfully named the arguments of the call to '{method.Name}' at {line}:{column}";
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error naming arguments: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// A positional argument is named after its parameter. Arguments passed
    /// to a params array in expanded form cannot be named, so they stay.
    /// </summary>
    private static ArgumentSyntax NameIfPositional(ArgumentSyntax argument, int index, IMethodSymbol method)
    {
        if (argument.NameColon is not null || index >= method.Parameters.Length)
            return argument;

        var parameter = method.Parameters[index];
        return parameter.IsParams ? argument : SignatureChange.Named(argument, parameter.Name);
    }

    /// <summary>Named arguments can make another overload applicable; the call must still bind as it did.</summary>
    private static async Task EnsureSameMethodAsync(
        Document document,
        SyntaxAnnotation marker,
        IMethodSymbol method,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var model = await document.GetSemanticModelAsync(cancellationToken);
        var call = root!.GetAnnotatedNodes(marker).Single();
        var bound = model!.GetSymbolInfo(call, cancellationToken).Symbol as IMethodSymbol;
        if (bound is null || MethodFamily.Key(bound) != MethodFamily.Key(method))
            throw new McpException(
                $"Error: With named arguments the call would no longer bind to '{method.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)}'");
    }
}
