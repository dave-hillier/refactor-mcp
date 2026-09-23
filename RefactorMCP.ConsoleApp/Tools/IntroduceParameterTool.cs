using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Editing;

[McpServerToolType]
public static class IntroduceParameterTool
{
    private static async Task<string> IntroduceParameterWithSolution(Document document, string methodName, string selectionRange, string parameterName)
    {
        var sourceText = await document.GetTextAsync();
        var syntaxRoot = await document.GetSyntaxRootAsync();

        if (!syntaxRoot!.DescendantNodes().OfType<BaseMethodDeclarationSyntax>().Any(m => NameOf(m) == methodName))
            return $"Error: No method named '{methodName}' found";

        var span = RefactoringHelpers.ParseSelectionRange(sourceText, selectionRange);
        var expression = SelectedExpression(syntaxRoot, sourceText, span)
            ?? throw new McpException("Error: Selected code is not a valid expression");

        var declaration = expression.Ancestors().OfType<BaseMethodDeclarationSyntax>().FirstOrDefault()
            ?? throw new McpException("Error: Selected code is not within a method");
        if (NameOf(declaration) != methodName)
            throw new McpException($"Error: Selected code is not within method '{methodName}'");

        var model = await document.GetSemanticModelAsync();
        var method = (IMethodSymbol)model!.GetDeclaredSymbol(declaration)!;
        var uses = ParameterUses(model, expression, method);
        EnsureNameIsFree(model, declaration, expression, parameterName);

        var type = model.GetTypeInfo(expression).Type ?? model.GetTypeInfo(expression).ConvertedType;
        if (type is null || type.TypeKind == TypeKind.Error)
            throw new McpException("Error: The selected expression has no type a parameter could declare");
        if (MentionsMethodTypeParameter(type))
            throw new McpException($"Error: The expression's type '{type.ToDisplayString()}' uses the method's type parameter, which calls cannot name");

        // Mark the expression so it can be found again once the signature has changed.
        var marker = new SyntaxAnnotation();
        var solution = document.Project.Solution;
        var annotated = solution.WithDocumentSyntaxRoot(
            document.Id,
            syntaxRoot.ReplaceNode(expression, expression.WithAdditionalAnnotations(marker)));
        var annotatedMethod = await SolutionEdits.ResolveAsync(annotated, document.Project.Id, method);

        var typeName = type.ToMinimalDisplayString(model, declaration.SpanStart);
        var newParameter = ParameterSlot.Added(
            ParameterSlot.ParseDeclaration(typeName, parameterName, null),
            site => ValueAt(site, expression, uses, method));
        var position = method.Parameters.FirstOrDefault(p => p.HasExplicitDefaultValue || p.IsParams)?.Ordinal
            ?? method.Parameters.Length;
        var slots = method.Parameters.Select(p => ParameterSlot.Existing(p.Ordinal)).ToList();
        slots.Insert(position, newParameter);

        var changed = await SignatureChange.ApplyAsync(annotated, annotatedMethod, slots);
        var changedRoot = await changed.GetDocument(document.Id)!.GetSyntaxRootAsync();
        var target = changedRoot!.GetAnnotatedNodes(marker).Single();
        changed = changed.WithDocumentSyntaxRoot(
            document.Id,
            changedRoot.ReplaceNode(target, SyntaxFactory.IdentifierName(parameterName).WithTriviaFrom(target)));

        await SolutionEdits.EnsureCompilesAsync(solution, changed);
        await SolutionEdits.WriteAsync(solution, changed);

        return $"Successfully introduced parameter '{parameterName}' from {selectionRange} in method '{methodName}' in {document.FilePath} (solution mode)";
    }

    private static string NameOf(BaseMethodDeclarationSyntax method) => method switch
    {
        MethodDeclarationSyntax m => m.Identifier.ValueText,
        ConstructorDeclarationSyntax c => c.Identifier.ValueText,
        _ => "",
    };

    /// <summary>The expression the selection covers exactly, ignoring surrounding whitespace.</summary>
    private static ExpressionSyntax? SelectedExpression(SyntaxNode root, SourceText text, TextSpan span)
    {
        var selected = text.ToString(span);
        var start = span.Start + (selected.Length - selected.TrimStart().Length);
        var end = span.End - (selected.Length - selected.TrimEnd().Length);
        if (end <= start)
            return null;

        var trimmed = TextSpan.FromBounds(start, end);
        return root.FindNode(trimmed, getInnermostNodeForTie: true)
            .AncestorsAndSelf()
            .TakeWhile(n => n.Span == trimmed)
            .OfType<ExpressionSyntax>()
            .LastOrDefault();
    }

    /// <summary>
    /// The identifiers in the expression that read the method's own
    /// parameters, which calls replace with their arguments. Anything else
    /// callers cannot see is refused.
    /// </summary>
    private static List<(IdentifierNameSyntax Identifier, int Ordinal)> ParameterUses(
        SemanticModel model,
        ExpressionSyntax expression,
        IMethodSymbol method)
    {
        var uses = new List<(IdentifierNameSyntax, int)>();
        foreach (var node in expression.DescendantNodesAndSelf())
        {
            if (node is ThisExpressionSyntax or BaseExpressionSyntax)
                throw new McpException($"Error: The expression uses the instance member '{node}', which callers cannot see");

            if (node is not IdentifierNameSyntax identifier)
                continue;

            var name = identifier.Identifier.ValueText;
            switch (model.GetSymbolInfo(identifier).Symbol)
            {
                case IParameterSymbol parameter when SymbolEqualityComparer.Default.Equals(parameter.ContainingSymbol, method):
                    uses.Add((identifier, parameter.Ordinal));
                    break;
                case ILocalSymbol or IParameterSymbol or IRangeVariableSymbol or IMethodSymbol { MethodKind: MethodKind.LocalFunction }:
                    throw new McpException($"Error: The expression uses the local '{name}', which callers cannot see");
                case ITypeParameterSymbol { DeclaringMethod: not null }:
                    throw new McpException($"Error: The expression uses the method's type parameter '{name}', which calls cannot name");
                case IFieldSymbol or IPropertySymbol or IMethodSymbol or IEventSymbol when
                    !model.GetSymbolInfo(identifier).Symbol!.IsStatic && !IsAccessedThroughAnotherObject(identifier):
                    throw new McpException($"Error: The expression uses the instance member '{name}', which callers cannot see");
            }
        }

        return uses;
    }

    private static bool IsAccessedThroughAnotherObject(IdentifierNameSyntax identifier) => identifier.Parent switch
    {
        MemberAccessExpressionSyntax access when access.Name == identifier =>
            access.Expression is not (ThisExpressionSyntax or BaseExpressionSyntax),
        MemberBindingExpressionSyntax => true,
        _ => false,
    };

    private static bool MentionsMethodTypeParameter(ITypeSymbol type) => type switch
    {
        ITypeParameterSymbol parameter => parameter.DeclaringMethod is not null,
        IArrayTypeSymbol array => MentionsMethodTypeParameter(array.ElementType),
        INamedTypeSymbol named => named.TypeArguments.Any(MentionsMethodTypeParameter),
        _ => false,
    };

    /// <summary>
    /// The new parameter must not collide with a parameter or local, nor
    /// shadow a member the body already uses by that name.
    /// </summary>
    private static void EnsureNameIsFree(
        SemanticModel model,
        BaseMethodDeclarationSyntax declaration,
        ExpressionSyntax expression,
        string name)
    {
        var inScope = model.LookupSymbols(expression.SpanStart, name: name)
            .Any(s => s is not INamespaceOrTypeSymbol);
        var declaredInside = declaration.DescendantNodes()
            .Select(n => model.GetDeclaredSymbol(n))
            .Any(s => s is not null && s.Name == name);
        if (inScope || declaredInside)
            throw new McpException($"Error: The name '{name}' is already in use in '{NameOf(declaration)}'");
    }

    /// <summary>The expression a call passes for the new parameter: the selected one, with its arguments in place of the parameters.</summary>
    private static ExpressionSyntax ValueAt(
        SignatureCallSite site,
        ExpressionSyntax expression,
        IReadOnlyList<(IdentifierNameSyntax Identifier, int Ordinal)> uses,
        IMethodSymbol method)
    {
        var value = expression.ReplaceNodes(
            uses.Select(u => u.Identifier),
            (original, _) =>
            {
                var argument = ArgumentAt(site, uses.First(u => u.Identifier == original).Ordinal, method);
                var substituted = original == expression || !NeedsParentheses(argument)
                    ? argument
                    : SyntaxFactory.ParenthesizedExpression(argument);
                return substituted.WithTriviaFrom(original);
            });
        return value.WithoutTrivia();
    }

    private static ExpressionSyntax ArgumentAt(SignatureCallSite site, int ordinal, IMethodSymbol method)
    {
        var passed = ordinal == 0 && site.Receiver is { } receiver ? receiver : site.ArgumentFor(ordinal);
        if (passed is not null)
            return passed.WithoutTrivia();

        var parameter = method.Parameters[ordinal];
        var defaultValue = parameter.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax())
            .OfType<ParameterSyntax>()
            .Select(p => p.Default?.Value)
            .FirstOrDefault(v => v is not null);
        return defaultValue?.WithoutTrivia()
            ?? throw new McpException($"Error: A call passes no single value for '{parameter.Name}', so the expression cannot be computed there");
    }

    private static bool NeedsParentheses(ExpressionSyntax expression) => expression is not (
        IdentifierNameSyntax or LiteralExpressionSyntax or MemberAccessExpressionSyntax or InvocationExpressionSyntax
        or ElementAccessExpressionSyntax or ParenthesizedExpressionSyntax or ThisExpressionSyntax
        or BaseObjectCreationExpressionSyntax or InterpolatedStringExpressionSyntax or GenericNameSyntax);

    private static Task<string> IntroduceParameterSingleFile(string filePath, string methodName, string selectionRange, string parameterName)
    {
        return RefactoringHelpers.ApplySingleFileEdit(
            filePath,
            text => IntroduceParameterInSource(text, methodName, selectionRange, parameterName),
            $"Successfully introduced parameter '{parameterName}' from {selectionRange} in method '{methodName}' in {filePath} (single file mode)");
    }

    public static string IntroduceParameterInSource(string sourceText, string methodName, string selectionRange, string parameterName)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = syntaxTree.GetRoot();
        var text = SourceText.From(sourceText);

        var method = syntaxRoot.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            return $"Error: No method named '{methodName}' found";

        var span = RefactoringHelpers.ParseSelectionRange(text, selectionRange);

        var selectedExpression = syntaxRoot.DescendantNodes()
            .OfType<ExpressionSyntax>()
            .Where(e => span.Contains(e.Span) || e.Span.Contains(span))
            .OrderBy(e => Math.Abs(e.Span.Length - span.Length))
            .ThenBy(e => e.Span.Length)
            .FirstOrDefault();
        if (selectedExpression == null)
            throw new McpException("Error: Selected code is not a valid expression");

        var parameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameterName))
            .WithType(SyntaxFactory.ParseTypeName("object"));

        var parameterReference = SyntaxFactory.IdentifierName(parameterName);
        var generator = SyntaxGenerator.GetGenerator(RefactoringHelpers.SharedWorkspace, LanguageNames.CSharp);
        var rewriter = new ParameterIntroductionRewriter(selectedExpression, methodName, parameter, parameterReference, generator);
        var newRoot = rewriter.Visit(syntaxRoot);

        var formattedRoot = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
        return formattedRoot.ToFullString();
    }
    [McpServerTool, Description("Create a new parameter from selected code (preferred for large C# file refactoring)")]
    public static async Task<string> IntroduceParameter(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Name of the method to add parameter to")] string methodName,
        [Description("Range in format 'startLine:startColumn-endLine:endColumn'")] string selectionRange,
        [Description("Name for the new parameter")] string parameterName)
    {
        try
        {
            return await RefactoringHelpers.RunWithSolutionOrFile(
                solutionPath,
                filePath,
                doc => IntroduceParameterWithSolution(doc, methodName, selectionRange, parameterName),
                path => IntroduceParameterSingleFile(path, methodName, selectionRange, parameterName));
        }
        catch (Exception ex)
        {
            throw new McpException($"Error introducing parameter: {ex.Message}", ex);
        }
    }
}
