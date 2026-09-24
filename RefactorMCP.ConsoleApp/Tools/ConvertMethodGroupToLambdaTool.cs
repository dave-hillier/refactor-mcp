using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Threading;

[McpServerToolType]
public static class ConvertMethodGroupToLambdaTool
{
    [McpServerTool, Description("Replace a method group used as a delegate, such as Format, with a lambda calling it, such as x => Format(x)")]
    public static async Task<string> ConvertMethodGroupToLambda(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line of the method group's name (1-based)")] int line,
        [Description("Column of the method group's name (1-based)")] int column,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var target = await PositionTarget.FindAsync(solutionPath, filePath, line, column, cancellationToken);
            var group = MethodGroupAt(target);
            var method = target.Model.GetSymbolInfo(group, cancellationToken).Symbol as IMethodSymbol;
            var delegateType = target.Model.GetTypeInfo(group, cancellationToken).ConvertedType as INamedTypeSymbol;
            if (method == null || delegateType?.DelegateInvokeMethod is not { } invoke || IsCalled(group))
                throw new McpException($"Error: '{group}' at {target.Describe()} is not a method group converted to a delegate");

            var receiver = (group as MemberAccessExpressionSyntax)?.Expression;
            if (!DelegateConversion.IsStableReceiver(receiver, target.Model, cancellationToken))
            {
                throw new McpException(
                    $"Error: The method group evaluates '{receiver}' once, where a lambda would evaluate it each time it runs");
            }

            var names = ParameterNames(target.Model, group, method);
            var lambda = Lambda(target.Model, group, method, invoke, names).WithTriviaFrom(group);
            var newRoot = await DelegateConversion.ReplaceCheckedAsync(
                target,
                group,
                lambda,
                DelegateConversion.Bind(target.Model, group, group, cancellationToken),
                replaced => ((InvocationExpressionSyntax)((LambdaExpressionSyntax)replaced).ExpressionBody!).Expression,
                $"Error: Replacing '{group}' with a lambda would change which method, delegate type or overload is chosen",
                cancellationToken);

            await target.WriteAsync(newRoot);
            return $"Successfully converted the method group '{group}' to a lambda in {filePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error converting method group to lambda: {ex.Message}", ex);
        }
    }

    /// <summary>The name at the position, with the receiver it is accessed on.</summary>
    private static ExpressionSyntax MethodGroupAt(PositionTarget target)
    {
        if (target.Token.Parent is not SimpleNameSyntax name)
            throw new McpException($"Error: The code at {target.Describe()} is not a method group");

        return name.Parent is MemberAccessExpressionSyntax access && access.Name == name ? access : name;
    }

    private static bool IsCalled(ExpressionSyntax group) =>
        group.Parent is InvocationExpressionSyntax invocation && invocation.Expression == group;

    /// <summary>
    /// The method's parameter names, numbered where a local, a parameter, the method or
    /// the receiver already uses the name at the method group.
    /// </summary>
    private static List<string> ParameterNames(SemanticModel model, ExpressionSyntax group, IMethodSymbol method)
    {
        var taken = new HashSet<string>(group.DescendantNodesAndSelf().OfType<SimpleNameSyntax>().Select(n => n.Identifier.ValueText));
        var names = new List<string>();
        foreach (var parameter in method.Parameters)
        {
            var name = parameter.Name;
            for (var suffix = 1; taken.Contains(name) || IsLocalInScope(model, group, name); suffix++)
                name = parameter.Name + suffix;

            taken.Add(name);
            names.Add(name);
        }

        return names;
    }

    private static bool IsLocalInScope(SemanticModel model, ExpressionSyntax group, string name) =>
        model.LookupSymbols(group.SpanStart, name: name)
            .Any(s => s is ILocalSymbol or IParameterSymbol or IRangeVariableSymbol ||
                      s is IMethodSymbol { MethodKind: MethodKind.LocalFunction });

    /// <summary>
    /// A lambda with the delegate's parameters calling the method. Parameters passed by
    /// reference need their types written, so then every parameter has its type.
    /// </summary>
    private static LambdaExpressionSyntax Lambda(
        SemanticModel model,
        ExpressionSyntax group,
        IMethodSymbol method,
        IMethodSymbol invoke,
        List<string> names)
    {
        var typed = invoke.Parameters.Any(p => p.RefKind != RefKind.None);
        var parameters = invoke.Parameters.Select((p, i) =>
        {
            var parameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier(names[i]));
            if (!typed)
                return parameter;

            return parameter
                .WithModifiers(RefModifiers(p.RefKind))
                .WithType(SyntaxFactory.ParseTypeName(p.Type.ToMinimalDisplayString(model, group.SpanStart)).WithTrailingTrivia(SyntaxFactory.Space));
        }).ToList();

        var arguments = method.Parameters.Select((p, i) =>
        {
            var argument = SyntaxFactory.Argument(SyntaxFactory.IdentifierName(names[i]));
            var modifier = RefModifiers(p.RefKind);
            return modifier.Count == 0 ? argument : argument.WithRefKindKeyword(modifier[0]);
        });

        var call = SyntaxFactory.InvocationExpression(
            group.WithoutTrivia(),
            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments, Commas(names.Count))));
        var arrow = SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.Space), SyntaxKind.EqualsGreaterThanToken, SyntaxFactory.TriviaList(SyntaxFactory.Space));

        if (parameters.Count == 1 && !typed)
            return SyntaxFactory.SimpleLambdaExpression(parameters[0]).WithArrowToken(arrow).WithExpressionBody(call);

        return SyntaxFactory.ParenthesizedLambdaExpression(
                SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters, Commas(parameters.Count))),
                call)
            .WithArrowToken(arrow);
    }

    private static IEnumerable<SyntaxToken> Commas(int count) =>
        Enumerable.Range(0, Math.Max(0, count - 1))
            .Select(_ => SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space));

    private static SyntaxTokenList RefModifiers(RefKind refKind) => refKind switch
    {
        RefKind.Ref => SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.RefKeyword).WithTrailingTrivia(SyntaxFactory.Space)),
        RefKind.Out => SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.OutKeyword).WithTrailingTrivia(SyntaxFactory.Space)),
        RefKind.In => SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.InKeyword).WithTrailingTrivia(SyntaxFactory.Space)),
        _ => SyntaxFactory.TokenList(),
    };
}
