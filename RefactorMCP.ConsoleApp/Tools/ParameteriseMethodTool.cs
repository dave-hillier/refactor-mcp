using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using RefactorMCP.ConsoleApp.Tools.Composites;

[McpServerToolType]
public static class ParameteriseMethodTool
{
    [McpServerTool, Description("Replace methods that differ only in literal values with one method taking those values as parameters, and make every call pass its method's values")]
    public static async Task<string> ParameteriseMethod(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file declaring the methods")] string filePath,
        [Description("The similar methods, by name; the first becomes the parameterised method")] string[] methods,
        [Description("Name of the parameterised method")] string name,
        [Description("A name for each literal that differs between the methods, in the order the literals appear")] string[] parameterNames,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var similar = await SimilarMethodsAsync(solution, filePath, methods, cancellationToken);
            var positions = await VaryingLiteralsAsync(solution, similar, cancellationToken);
            if (positions.Count != parameterNames.Length)
                throw new McpException(
                    $"Error: {positions.Count} literal(s) differ between the methods, but {parameterNames.Length} parameter name(s) were given");

            await CompositeRecipe.RunAsync(solutionPath, async recipe =>
            {
                // Introduce Parameter on each differing literal of the first method. A
                // literal becomes a name, one token for another, so positions hold.
                var first = methods[0];
                for (var i = 0; i < positions.Count; i++)
                {
                    var literal = await TokenAsync(solutionPath, filePath, first, positions[i], cancellationToken);
                    var parameterName = parameterNames[i];
                    await recipe.StepAsync("introduce-parameter", () =>
                        IntroduceParameterTool.IntroduceParameter(solutionPath, filePath, first, Range(literal.Parent!), parameterName));
                }

                if (name != first)
                {
                    var identifier = (await DeclarationAsync(solutionPath, filePath, first, cancellationToken)).Identifier;
                    var start = identifier.GetLocation().GetLineSpan().StartLinePosition;
                    await recipe.StepAsync("rename", () =>
                        RenameSymbolTool.RenameSymbol(solutionPath, filePath, first, name, start.Line + 1, start.Character + 1, cancellationToken));
                }

                // Extract Method onto the parameterised method makes each other method
                // call it with its own literals; Inline Method then points its callers
                // there too.
                foreach (var other in methods.Skip(1))
                {
                    var body = (await DeclarationAsync(solutionPath, filePath, other, cancellationToken)).Body!;
                    var statements = TextSpan.FromBounds(body.Statements.First().SpanStart, body.Statements.Last().Span.End);
                    await recipe.StepAsync("extract-method", () =>
                        ExtractMethodTool.ExtractMethod(solutionPath, filePath, Range(body.SyntaxTree, statements), name));

                    var line = (await DeclarationAsync(solutionPath, filePath, other, cancellationToken)).Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    await recipe.StepAsync("inline-method", () => InlineMethodTool.InlineMethod(solutionPath, filePath, other, line));
                }
            }, cancellationToken);

            return $"Successfully parameterised {string.Join(", ", methods)} as '{name}({string.Join(", ", parameterNames)})'";
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error parameterising method: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// The methods, which must share a class, a signature and a block body, and be
    /// plain methods that Inline Method can remove.
    /// </summary>
    private static async Task<List<(IMethodSymbol Symbol, MethodDeclarationSyntax Declaration)>> SimilarMethodsAsync(
        Solution solution,
        string filePath,
        string[] names,
        CancellationToken cancellationToken)
    {
        if (names.Length < 2)
            throw new McpException("Error: Parameterise Method needs at least two similar methods");

        var methods = new List<(IMethodSymbol, MethodDeclarationSyntax)>();
        foreach (var name in names)
        {
            var symbol = await SolutionEdits.FindMethodAsync(solution, filePath, name, null, cancellationToken);
            if (await symbol.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken) is not MethodDeclarationSyntax { Body: not null } declaration)
                throw new McpException($"Error: '{name}' has no block body to compare");
            methods.Add((symbol, declaration));
        }

        var (first, _) = methods[0];
        foreach (var (symbol, _) in methods.Skip(1))
        {
            var sameSignature = SymbolEqualityComparer.Default.Equals(symbol.ContainingType, first.ContainingType)
                && SymbolEqualityComparer.Default.Equals(symbol.ReturnType, first.ReturnType)
                && symbol.IsStatic == first.IsStatic
                && !symbol.IsGenericMethod && !first.IsGenericMethod
                && symbol.Parameters.Length == first.Parameters.Length
                && symbol.Parameters.Zip(first.Parameters).All(p =>
                    p.First.Name == p.Second.Name &&
                    p.First.RefKind == p.Second.RefKind &&
                    SymbolEqualityComparer.Default.Equals(p.First.Type, p.Second.Type));
            if (!sameSignature)
                throw new McpException(
                    $"Error: '{symbol.Name}' and '{first.Name}' differ in their class, return type, parameters or static modifier");
        }

        return methods;
    }

    /// <summary>
    /// The positions, among the first method's body tokens, of the literals that
    /// differ between the methods. Everything else must be the same, token for token,
    /// and each differing literal must have the same type in every method.
    /// </summary>
    private static async Task<List<int>> VaryingLiteralsAsync(
        Solution solution,
        List<(IMethodSymbol Symbol, MethodDeclarationSyntax Declaration)> methods,
        CancellationToken cancellationToken)
    {
        var model = (await solution.GetDocument(methods[0].Declaration.SyntaxTree)!.GetSemanticModelAsync(cancellationToken))!;
        var firstTokens = methods[0].Declaration.Body!.DescendantTokens().ToList();
        var positions = new SortedSet<int>();

        foreach (var (symbol, declaration) in methods.Skip(1))
        {
            var tokens = declaration.Body!.DescendantTokens().ToList();
            var differs = tokens.Count != firstTokens.Count;
            for (var i = 0; !differs && i < tokens.Count; i++)
            {
                if (tokens[i].ValueText == firstTokens[i].ValueText && tokens[i].RawKind == firstTokens[i].RawKind)
                    continue;

                if (firstTokens[i].Parent is not LiteralExpressionSyntax firstLiteral
                    || tokens[i].Parent is not LiteralExpressionSyntax literal
                    || !SymbolEqualityComparer.Default.Equals(model.GetTypeInfo(firstLiteral).Type, model.GetTypeInfo(literal).Type))
                {
                    differs = true;
                    break;
                }

                positions.Add(i);
            }

            if (differs)
                throw new McpException(
                    $"Error: '{symbol.Name}' differs from '{methods[0].Symbol.Name}' in more than literals of the same type");
        }

        if (positions.Count == 0)
            throw new McpException("Error: The methods have the same body, so there is no value to make a parameter");

        return positions.ToList();
    }

    private static async Task<MethodDeclarationSyntax> DeclarationAsync(string solutionPath, string filePath, string name, CancellationToken cancellationToken)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var method = await SolutionEdits.FindMethodAsync(solution, filePath, name, null, cancellationToken);
        return (MethodDeclarationSyntax)await method.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken);
    }

    private static async Task<SyntaxToken> TokenAsync(string solutionPath, string filePath, string name, int position, CancellationToken cancellationToken) =>
        (await DeclarationAsync(solutionPath, filePath, name, cancellationToken)).Body!.DescendantTokens().ElementAt(position);

    private static string Range(SyntaxNode node) => Range(node.SyntaxTree, node.Span);

    /// <summary>A span as the tools' <c>startLine:startColumn-endLine:endColumn</c>, 1-based, end exclusive.</summary>
    private static string Range(SyntaxTree tree, TextSpan span)
    {
        var lines = tree.GetLineSpan(span);
        return $"{lines.StartLinePosition.Line + 1}:{lines.StartLinePosition.Character + 1}-{lines.EndLinePosition.Line + 1}:{lines.EndLinePosition.Character + 1}";
    }
}
