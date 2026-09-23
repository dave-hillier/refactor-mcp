using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using System.Threading;

/// <summary>
/// Extract Method on a selection that is exactly an expression: the expression
/// becomes the body of a new private method returning its value, and the call
/// takes its place.
/// </summary>
internal static class ExpressionExtraction
{
    /// <summary>
    /// The expression the selection covers exactly, ignoring surrounding
    /// whitespace, when it is one a method could compute: not a whole
    /// expression statement, which is a statement to extract, nor a type or the
    /// name of a member being accessed.
    /// </summary>
    public static ExpressionSyntax? Selected(SyntaxNode root, SourceText text, TextSpan span)
    {
        var selected = text.ToString(span);
        var start = span.Start + (selected.Length - selected.TrimStart().Length);
        var end = span.End - (selected.Length - selected.TrimEnd().Length);
        if (end <= start)
            return null;

        var trimmed = TextSpan.FromBounds(start, end);
        var node = root.FindNode(trimmed, getInnermostNodeForTie: true);
        while (node.Parent is { } parent && parent.Span == trimmed && parent is ExpressionSyntax)
            node = parent;

        if (node is not ExpressionSyntax expression || expression.Span != trimmed)
            return null;
        if (expression.Parent is ExpressionStatementSyntax || SyntaxFacts.IsInNamespaceOrTypeContext(expression))
            return null;
        if (expression.Parent is MemberAccessExpressionSyntax access && access.Name == expression
            || expression.Parent is MemberBindingExpressionSyntax
            || expression.Parent is QualifiedNameSyntax)
            return null;

        return expression.Ancestors().OfType<MethodDeclarationSyntax>().Any() ? expression : null;
    }

    public static async Task<string> ExtractAsync(Document document, ExpressionSyntax expression, string methodName, CancellationToken cancellationToken)
    {
        var model = (await document.GetSemanticModelAsync(cancellationToken))!;
        var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
        var method = expression.Ancestors().OfType<MethodDeclarationSyntax>().First();

        var resultType = ResultType(expression, model);
        EnsureNoWrites(expression, method, model);

        var parameters = ExtractMethodRewriter.FindParameters(new[] { expression }, model);
        var typeParameters = ExtractMethodRewriter.FindTypeParameters(method, new[] { expression }, parameters, resultType, model);

        var modifiers = SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));
        if (method.Modifiers.Any(SyntaxKind.StaticKeyword))
            modifiers = modifiers.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));

        var extracted = SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName(resultType.ToMinimalDisplayString(model, expression.SpanStart)), methodName)
            .WithModifiers(modifiers)
            .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters.Select(p => p.Syntax))))
            .WithBody(SyntaxFactory.Block(SyntaxFactory.ReturnStatement(expression.WithoutTrivia().WithAdditionalAnnotations(Moved))));
        if (typeParameters.Count > 0)
        {
            var names = typeParameters.Select(t => t.Name).ToHashSet();
            extracted = extracted
                .WithTypeParameterList(SyntaxFactory.TypeParameterList(SyntaxFactory.SeparatedList(
                    typeParameters.Select(t => SyntaxFactory.TypeParameter(t.Name)))))
                .WithConstraintClauses(SyntaxFactory.List(method.ConstraintClauses.Where(c => names.Contains(c.Name.Identifier.ValueText))));
        }

        // Type arguments are spelled out only when the arguments cannot infer them.
        SimpleNameSyntax name = typeParameters.Any(t => !parameters.Any(p => ExtractMethodRewriter.Mentions(p.Type, t)))
            ? SyntaxFactory.GenericName(methodName).WithTypeArgumentList(SyntaxFactory.TypeArgumentList(
                SyntaxFactory.SeparatedList<TypeSyntax>(typeParameters.Select(t => SyntaxFactory.IdentifierName(t.Name)))))
            : SyntaxFactory.IdentifierName(methodName);
        var call = SyntaxFactory.InvocationExpression(name, SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(
            parameters.Select(p => SyntaxFactory.Argument(SyntaxFactory.IdentifierName(p.Syntax.Identifier))))));

        var tracked = root.TrackNodes(expression, method);
        tracked = tracked.ReplaceNode(tracked.GetCurrentNode(expression)!, call.WithTriviaFrom(expression).WithAdditionalAnnotations(Formatter.Annotation));
        var currentMethod = tracked.GetCurrentNode(method)!;
        var currentType = (TypeDeclarationSyntax)currentMethod.Parent!;
        var endOfLine = TypeDeclarations.NewLine(currentType);
        var placed = extracted.WithLeadingTrivia(endOfLine).WithTrailingTrivia(endOfLine).WithAdditionalAnnotations(Formatter.Annotation);
        tracked = tracked.ReplaceNode(currentType, currentType.WithMembers(currentType.Members.Insert(currentType.Members.IndexOf(currentMethod) + 1, placed)));

        var changed = await Formatter.FormatAsync(document.WithSyntaxRoot(tracked), Formatter.Annotation, cancellationToken: cancellationToken);
        changed = await KeepContinuationIndentsAsync(changed, expression, cancellationToken);
        await SolutionEdits.EnsureCompilesAsync(document.Project.Solution, changed.Project.Solution, cancellationToken);
        await SolutionEdits.WriteAsync(document.Project.Solution, changed.Project.Solution, cancellationToken);
        return $"Successfully extracted method '{methodName}' from the expression in {document.FilePath}";
    }

    /// <summary>
    /// The type the method returns: the expression's own type, or the type it
    /// converts to where it has none, such as a <c>null</c> literal. In a
    /// nullable context a reference type is nullable only where the value may be null.
    /// </summary>
    private static ITypeSymbol ResultType(ExpressionSyntax expression, SemanticModel model)
    {
        if (IsAssignedTo(expression))
            throw new McpException("Error: The selected expression is not a value a method could return: it is assigned to");

        var info = model.GetTypeInfo(expression);
        var type = info.Type ?? info.ConvertedType;
        if (type is null or IErrorTypeSymbol || type.SpecialType == SpecialType.System_Void)
            throw new McpException("Error: The selected expression is not a value a method could return: it has no type");

        if (type.IsReferenceType && type.NullableAnnotation != NullableAnnotation.None)
        {
            type = type.WithNullableAnnotation(info.Nullability.FlowState == NullableFlowState.MaybeNull
                ? NullableAnnotation.Annotated
                : NullableAnnotation.NotAnnotated);
        }

        return type;
    }

    private static bool IsAssignedTo(ExpressionSyntax expression) => expression.Parent switch
    {
        AssignmentExpressionSyntax assignment => assignment.Left == expression,
        PrefixUnaryExpressionSyntax prefix => prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression),
        PostfixUnaryExpressionSyntax unary => unary.IsKind(SyntaxKind.PostIncrementExpression) || unary.IsKind(SyntaxKind.PostDecrementExpression),
        ArgumentSyntax argument => !argument.RefKindKeyword.IsKind(SyntaxKind.None),
        _ => false,
    };

    /// <summary>
    /// The new method gets its own copies of the locals it reads, so the
    /// expression may not assign one, and a variable it declares, such as a
    /// pattern variable, may not be used outside it. An await would make the
    /// new method async and the call an await, which is not supported.
    /// </summary>
    private static void EnsureNoWrites(ExpressionSyntax expression, MethodDeclarationSyntax method, SemanticModel model)
    {
        if (expression.DescendantNodesAndSelf(n => n is not AnonymousFunctionExpressionSyntax).OfType<AwaitExpressionSyntax>().Any())
            throw new McpException("Error: The selected expression awaits, which an extracted method would have to do too");

        var flow = model.AnalyzeDataFlow(expression);
        if (flow is not { Succeeded: true })
            return;

        var assigned = flow.WrittenInside.FirstOrDefault(s => s is ILocalSymbol or IParameterSymbol
            && !flow.VariablesDeclared.Contains(s, SymbolEqualityComparer.Default));
        if (assigned is not null)
            throw new McpException($"Error: The selected expression assigns '{assigned.Name}', which the extracted method would only change a copy of");

        var declared = flow.VariablesDeclared.FirstOrDefault(s => flow.ReadOutside.Contains(s, SymbolEqualityComparer.Default)
            || flow.WrittenOutside.Contains(s, SymbolEqualityComparer.Default));
        if (declared is not null)
        {
            var use = method.DescendantNodes().OfType<IdentifierNameSyntax>()
                .First(n => !expression.Span.Contains(n.Span) && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(n).Symbol, declared));
            var line = use.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            throw new McpException(
                $"Error: The extracted block declares '{declared.Name}', which is used at line {line}. Include that code in the extraction, or narrow the selection.");
        }
    }

    private static readonly SyntaxAnnotation Moved = new(nameof(ExpressionExtraction) + "." + nameof(Moved));

    /// <summary>
    /// An expression written over several lines keeps the indentation of its
    /// later lines relative to the statement it is in, as it had where it came
    /// from; the formatter would otherwise indent them from where it moved.
    /// </summary>
    private static async Task<Document> KeepContinuationIndentsAsync(Document document, ExpressionSyntax original, CancellationToken cancellationToken)
    {
        var originalIndents = LineIndents(original).ToList();
        if (originalIndents.Count == 0)
            return document;

        var text = original.SyntaxTree.GetText(cancellationToken);
        var line = text.Lines.GetLineFromPosition(original.SpanStart).ToString();
        var originalBase = line[..(line.Length - line.TrimStart().Length)];

        var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
        var moved = root.GetAnnotatedNodes(Moved).Single();
        var statement = moved.Ancestors().OfType<StatementSyntax>().First();
        var newBase = statement.GetLeadingTrivia().LastOrDefault(t => t.IsKind(SyntaxKind.WhitespaceTrivia)).ToString();

        var indents = LineIndents(moved).ToList();
        var replacements = indents.Zip(originalIndents).ToDictionary(
            pair => pair.First,
            pair => SyntaxFactory.Whitespace(newBase + (pair.Second.ToString().StartsWith(originalBase, StringComparison.Ordinal)
                ? pair.Second.ToString()[originalBase.Length..]
                : "")));
        return document.WithSyntaxRoot(root.ReplaceTrivia(replacements.Keys, (trivia, _) => replacements[trivia]));
    }

    /// <summary>The whitespace that starts each line after the first, within a node.</summary>
    private static IEnumerable<SyntaxTrivia> LineIndents(SyntaxNode node)
    {
        var previous = default(SyntaxTrivia);
        foreach (var trivia in node.DescendantTrivia())
        {
            if (trivia.IsKind(SyntaxKind.WhitespaceTrivia) && previous.IsKind(SyntaxKind.EndOfLineTrivia))
                yield return trivia;
            previous = trivia;
        }
    }
}
