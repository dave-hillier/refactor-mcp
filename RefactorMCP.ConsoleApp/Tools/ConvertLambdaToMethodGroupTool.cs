using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Threading;

[McpServerToolType]
public static class ConvertLambdaToMethodGroupTool
{
    [McpServerTool, Description("Replace a lambda that only passes its parameters to a method, such as x => Format(x), with the method group Format")]
    public static async Task<string> ConvertLambdaToMethodGroup(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line of a position inside the lambda (1-based)")] int line,
        [Description("Column of that position (1-based)")] int column,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var target = await PositionTarget.FindAsync(solutionPath, filePath, line, column, cancellationToken);
            var lambda = target.Enclosing<AnonymousFunctionExpressionSyntax>()
                ?? throw new McpException($"Error: There is no lambda at {target.Describe()}");

            var call = ForwardingCall(target.Model, lambda, cancellationToken)
                ?? throw new McpException("Error: The lambda's body is not a single call of a method passing the lambda's parameters in order");

            var receiver = (call.Expression as MemberAccessExpressionSyntax)?.Expression;
            if (!DelegateConversion.IsStableReceiver(receiver, target.Model, cancellationToken))
            {
                throw new McpException(
                    $"Error: The lambda evaluates '{receiver}' each time it runs, where a method group would evaluate it once");
            }

            var group = call.Expression.WithoutTrivia().WithTriviaFrom(lambda);
            var newRoot = await DelegateConversion.ReplaceCheckedAsync(
                target,
                lambda,
                group,
                DelegateConversion.Bind(target.Model, lambda, call.Expression, cancellationToken),
                replaced => replaced,
                $"Error: Using '{call.Expression}' as a method group would change which method, delegate type or overload is chosen",
                cancellationToken);

            await target.WriteAsync(newRoot);
            return $"Successfully converted the lambda to the method group '{call.Expression}' in {filePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error converting lambda to method group: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// The call the lambda consists of, when it calls a method (not a delegate) with exactly
    /// its parameters, in order and with their ref kinds, and the method expression does
    /// not use them.
    /// </summary>
    private static InvocationExpressionSyntax? ForwardingCall(
        SemanticModel model,
        AnonymousFunctionExpressionSyntax lambda,
        CancellationToken cancellationToken)
    {
        if (lambda.AsyncKeyword != default)
            return null;

        var parameters = lambda switch
        {
            SimpleLambdaExpressionSyntax simple => new[] { simple.Parameter },
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ParameterList.Parameters.ToArray(),
            AnonymousMethodExpressionSyntax { ParameterList: { } list } => list.Parameters.ToArray(),
            _ => null,
        };
        if (parameters == null)
            return null;

        var body = lambda.Body switch
        {
            BlockSyntax { Statements: [ExpressionStatementSyntax statement] } => statement.Expression,
            BlockSyntax { Statements: [ReturnStatementSyntax { Expression: { } returned }] } => returned,
            ExpressionSyntax expression => expression,
            _ => null,
        };
        if (body is not InvocationExpressionSyntax call || call.ArgumentList.Arguments.Count != parameters.Length)
            return null;

        if (model.GetSymbolInfo(call, cancellationToken).Symbol is not IMethodSymbol { MethodKind: not MethodKind.DelegateInvoke })
            return null;

        var parameterSymbols = parameters.Select(p => model.GetDeclaredSymbol(p, cancellationToken)).ToList();
        for (var i = 0; i < parameters.Length; i++)
        {
            var argument = call.ArgumentList.Arguments[i];
            if (argument.NameColon != null ||
                argument.Expression is not IdentifierNameSyntax name ||
                !SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(name, cancellationToken).Symbol, parameterSymbols[i]) ||
                argument.RefKindKeyword.Kind() != RefKindKeyword(parameterSymbols[i]))
            {
                return null;
            }
        }

        var usesParameter = call.Expression.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Any(n => parameterSymbols.Contains(model.GetSymbolInfo(n, cancellationToken).Symbol, SymbolEqualityComparer.Default));
        return usesParameter ? null : call;
    }

    private static SyntaxKind RefKindKeyword(IParameterSymbol? parameter) => parameter?.RefKind switch
    {
        RefKind.Ref => SyntaxKind.RefKeyword,
        RefKind.Out => SyntaxKind.OutKeyword,
        RefKind.In => SyntaxKind.InKeyword,
        _ => SyntaxKind.None,
    };
}
