using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Extract Method onto a method the class already has. When the selected code is
/// that method's body, with each of its parameters standing for an expression in
/// the code, the code becomes a call of the method passing those expressions.
/// Parameterise Method uses it to point similar methods at the parameterised one.
/// </summary>
internal static class ExistingMethodCall
{
    public static async Task<SyntaxNode> ReplaceAsync(
        Document document,
        SemanticModel model,
        MethodDeclarationSyntax containingMethod,
        IReadOnlyList<SyntaxNode> selected,
        string name)
    {
        var containing = model.GetDeclaredSymbol(containingMethod)!;
        var root = (await document.GetSyntaxRootAsync())!;

        // A bare return ending the statements leaves the containing method, so it
        // stays after the call.
        // A single return of a value calls a method that returns the same value.
        var code = selected switch
        {
            [ReturnStatementSyntax { Expression: { } returned }] => new List<SyntaxNode> { returned },
            [.., ReturnStatementSyntax { Expression: null }] when selected.Count > 1 => selected.Take(selected.Count - 1).ToList(),
            _ => selected.ToList(),
        };

        foreach (var candidate in containing.ContainingType.GetMembers(name).OfType<IMethodSymbol>())
        {
            if (!CanStandFor(candidate, containing))
                continue;
            if (candidate.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax declaration)
                continue;

            var candidateDocument = document.Project.Solution.GetDocument(declaration.SyntaxTree);
            var pattern = Pattern(declaration, candidate, code, model);
            if (candidateDocument == null || pattern == null)
                continue;

            var matcher = new Matcher(candidate, (await candidateDocument.GetSemanticModelAsync())!, model);
            if (!matcher.Matches(pattern, code)
                || candidate.Parameters.Any(p => !matcher.Bindings.ContainsKey(p))
                || matcher.Bindings.Values.Any(ExpressionFacts.HasSideEffects))
            {
                continue;
            }

            var arguments = candidate.Parameters.Select(p => SyntaxFactory.Argument(matcher.Bindings[p].WithoutTrivia()));
            var call = SyntaxFactory.InvocationExpression(
                SyntaxFactory.IdentifierName(name),
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(
                    arguments,
                    Enumerable.Repeat(SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space), Math.Max(0, candidate.Parameters.Length - 1)))));
            return Replace(root, code, call);
        }

        throw new McpException(
            $"Error: '{containing.ContainingType.Name}' already has a member named '{name}', and no method of that name has the selected code as its body");
    }

    private static bool CanStandFor(IMethodSymbol candidate, IMethodSymbol containing) =>
        candidate.MethodKind == MethodKind.Ordinary &&
        !candidate.IsGenericMethod &&
        !(containing.IsStatic && !candidate.IsStatic) &&
        !SymbolEqualityComparer.Default.Equals(candidate, containing) &&
        candidate.Parameters.All(p => p.RefKind == RefKind.None && !p.IsParams);

    /// <summary>
    /// What the candidate's body must match: the expression a value-returning method
    /// returns, of the selected expression's type, or the statements of a void method.
    /// </summary>
    private static IReadOnlyList<SyntaxNode>? Pattern(
        MethodDeclarationSyntax declaration,
        IMethodSymbol candidate,
        IReadOnlyList<SyntaxNode> code,
        SemanticModel model)
    {
        if (code is [ExpressionSyntax expression])
        {
            var returned = declaration.ExpressionBody?.Expression
                ?? (declaration.Body?.Statements is [ReturnStatementSyntax { Expression: { } value }] ? value : null);
            var type = model.GetTypeInfo(expression).Type;
            return returned != null && !candidate.ReturnsVoid && SymbolEqualityComparer.Default.Equals(candidate.ReturnType, type)
                ? new[] { returned }
                : null;
        }

        if (!candidate.ReturnsVoid || declaration.Body == null)
            return null;

        var statements = declaration.Body.Statements;
        return statements.LastOrDefault() is ReturnStatementSyntax { Expression: null }
            ? statements.Take(statements.Count - 1).ToList()
            : statements.ToList();
    }

    /// <summary>The code with the call in its place: the expression, or the run of statements.</summary>
    private static SyntaxNode Replace(SyntaxNode root, IReadOnlyList<SyntaxNode> code, InvocationExpressionSyntax call)
    {
        if (code is [ExpressionSyntax expression])
            return root.ReplaceNode(expression, call.WithTriviaFrom(expression));

        var statement = SyntaxFactory.ExpressionStatement(call)
            .WithLeadingTrivia(code[0].GetLeadingTrivia())
            .WithTrailingTrivia(code[^1].GetTrailingTrivia());
        if (code[0].Parent is not BlockSyntax block)
            return root.ReplaceNode(code[0], statement);

        var first = block.Statements.IndexOf((StatementSyntax)code[0]);
        var statements = block.Statements
            .Where((_, i) => i < first || i >= first + code.Count)
            .ToList();
        statements.Insert(first, statement);
        return root.ReplaceNode(block, block.WithStatements(SyntaxFactory.List(statements)));
    }

    /// <summary>
    /// Compares the candidate's body with the code node by node. A reference to one of
    /// the candidate's parameters matches any expression, the same one each time it
    /// appears; every other name must mean the same symbol in both.
    /// </summary>
    private sealed class Matcher
    {
        private readonly IMethodSymbol _method;
        private readonly SemanticModel _patternModel;
        private readonly SemanticModel _codeModel;

        public Matcher(IMethodSymbol method, SemanticModel patternModel, SemanticModel codeModel)
        {
            _method = method;
            _patternModel = patternModel;
            _codeModel = codeModel;
        }

        public Dictionary<IParameterSymbol, ExpressionSyntax> Bindings { get; } = new(SymbolEqualityComparer.Default);

        public bool Matches(IReadOnlyList<SyntaxNode> pattern, IReadOnlyList<SyntaxNode> code) =>
            pattern.Count == code.Count && pattern.Zip(code).All(pair => Match(pair.First, pair.Second));

        private bool Match(SyntaxNode pattern, SyntaxNode code)
        {
            if (pattern is IdentifierNameSyntax name &&
                _patternModel.GetSymbolInfo(name).Symbol is IParameterSymbol parameter &&
                SymbolEqualityComparer.Default.Equals(parameter.ContainingSymbol, _method))
            {
                if (code is not ExpressionSyntax expression)
                    return false;
                if (Bindings.TryGetValue(parameter, out var bound))
                    return SyntaxFactory.AreEquivalent(bound, expression);

                Bindings[parameter] = expression;
                return true;
            }

            if (pattern.RawKind != code.RawKind)
                return false;
            if (pattern is SimpleNameSyntax && !SameSymbol(pattern, code))
                return false;

            var patternChildren = pattern.ChildNodesAndTokens();
            var codeChildren = code.ChildNodesAndTokens();
            if (patternChildren.Count != codeChildren.Count)
                return false;

            for (var i = 0; i < patternChildren.Count; i++)
            {
                var (p, c) = (patternChildren[i], codeChildren[i]);
                var same = p.IsToken
                    ? c.IsToken && p.AsToken().RawKind == c.AsToken().RawKind && p.AsToken().ValueText == c.AsToken().ValueText
                    : c.IsNode && Match(p.AsNode()!, c.AsNode()!);
                if (!same)
                    return false;
            }

            return true;
        }

        // Locals are each body's own, so they match by name.
        private bool SameSymbol(SyntaxNode pattern, SyntaxNode code)
        {
            var p = _patternModel.GetSymbolInfo(pattern).Symbol;
            var c = _codeModel.GetSymbolInfo(code).Symbol;
            if (p is ILocalSymbol && c is ILocalSymbol)
                return p.Name == c.Name;

            return SymbolEqualityComparer.Default.Equals(p?.OriginalDefinition, c?.OriginalDefinition);
        }
    }
}
