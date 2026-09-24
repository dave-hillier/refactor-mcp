using ModelContextProtocol.Server;
using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using System.ComponentModel;

[McpServerToolType]
public static class ChangeTypeTool
{
    [McpServerTool, Description("Change the declared type of a local, parameter, field, property or method return value, typically to a base type or interface")]
    public static async Task<string> ChangeType(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file with the declaration")] string filePath,
        [Description("Name of the local, field, property or method; for a parameter, the method's name")] string name,
        [Description("The new type, as it would be written in the file")] string newType,
        [Description("Name of the parameter of method 'name' to change, instead of its return type (optional)")] string? parameterName = null,
        [Description("Line of the declaration, to tell same-named declarations apart (optional)")] int? line = null,
        [Description("Column of the declaration (optional)")] int? column = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (solution, document) = await TypeRefactoringHelpers.LoadDocumentAsync(solutionPath, filePath, cancellationToken);
            var model = (await document.GetSemanticModelAsync(cancellationToken))!;
            var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
            var symbol = FindDeclared(root, model, await document.GetTextAsync(cancellationToken), name, line, column);

            if (parameterName is not null)
            {
                symbol = (symbol as IMethodSymbol)?.Parameters.FirstOrDefault(p => p.Name == parameterName)
                    ?? throw new McpException($"Error: {name} has no parameter named {parameterName}");
            }

            var (declaringDocument, declaration) = await DeclarationAsync(solution, symbol, cancellationToken);
            var description = parameterName ?? name;

            // Mark each place that uses the value, to compare what it binds to afterwards.
            var consumers = await ConsumersAsync(solution, symbol, cancellationToken);
            var declarationMark = new SyntaxAnnotation();
            var annotated = await AnnotateAsync(solution, consumers, declaringDocument, declaration, declarationMark, cancellationToken);

            var typeMark = new SyntaxAnnotation();
            var changedDocument = await WithNewTypeAsync(annotated.GetDocument(declaringDocument)!, declarationMark, typeMark, newType, cancellationToken);
            (changedDocument, _) = await TypeRefactoringHelpers.ResolveTypeAsync(changedDocument, typeMark, newType, cancellationToken);
            var changed = changedDocument.Project.Solution;

            var errors = await TypeRefactoringHelpers.NewErrorsAsync(solution, changed, cancellationToken);
            if (errors.Count > 0)
                throw new McpException($"Error: Changing {description} to {newType} breaks code that uses it: {TypeRefactoringHelpers.Describe(errors)}");

            await EnsureSameBindingsAsync(changed, consumers, symbol, description, newType, cancellationToken);
            await TypeRefactoringHelpers.ApplyAsync(solution, changed, cancellationToken);
            return $"Successfully changed the type of {description} to {newType}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error changing type: {ex.Message}", ex);
        }
    }

    private static ISymbol FindDeclared(SyntaxNode root, SemanticModel model, Microsoft.CodeAnalysis.Text.SourceText text, string name, int? line, int? column)
    {
        if (line is > 0 && column is > 0 && line <= text.Lines.Count)
        {
            var token = root.FindToken(text.Lines[line.Value - 1].Start + column.Value - 1);
            var declared = token.Parent?.AncestorsAndSelf()
                .Select(n => model.GetDeclaredSymbol(n))
                .FirstOrDefault(s => s?.Name == name);
            if (declared is not null)
                return declared;
        }

        var candidates = root.DescendantNodes()
            .Where(n => n is VariableDeclaratorSyntax or ParameterSyntax or PropertyDeclarationSyntax or MethodDeclarationSyntax or SingleVariableDesignationSyntax)
            .Select(n => model.GetDeclaredSymbol(n))
            .OfType<ISymbol>()
            .Where(s => s.Name == name)
            .Distinct(SymbolEqualityComparer.Default)
            .ToList();

        return candidates.Count switch
        {
            0 => throw new McpException($"Error: No declaration named {name} found"),
            1 => candidates[0],
            _ => throw new McpException($"Error: Several declarations are named {name}; pass the line and column of the one to change"),
        };
    }

    /// <summary>The syntax whose type is changed: a variable, parameter, property, method or foreach loop.</summary>
    private static async Task<(DocumentId Document, SyntaxNode Node)> DeclarationAsync(
        Solution solution,
        ISymbol symbol,
        CancellationToken cancellationToken)
    {
        var reference = symbol.DeclaringSyntaxReferences.FirstOrDefault()
            ?? throw new McpException($"Error: {symbol.Name} is not declared in source");
        var node = await reference.GetSyntaxAsync(cancellationToken);
        return node switch
        {
            VariableDeclaratorSyntax or ParameterSyntax or PropertyDeclarationSyntax or MethodDeclarationSyntax or ForEachStatementSyntax
                or SingleVariableDesignationSyntax { Parent: DeclarationExpressionSyntax } =>
                (solution.GetDocument(reference.SyntaxTree)!.Id, node),
            _ => throw new McpException($"Error: {symbol.Name} has no declared type to change"),
        };
    }

    /// <summary>
    /// The uses whose meaning depends on the value's type: the member reached
    /// through it, and the method it is passed to.
    /// </summary>
    private static async Task<IReadOnlyList<TypeRefactoringHelpers.Binding>> ConsumersAsync(Solution solution, ISymbol symbol, CancellationToken cancellationToken)
    {
        var consumers = new List<TypeRefactoringHelpers.Binding>();
        var references = await SymbolFinder.FindReferencesAsync(symbol, solution, cancellationToken);
        foreach (var location in references.SelectMany(r => r.Locations))
        {
            var root = await location.Document.GetSyntaxRootAsync(cancellationToken);
            var model = (await location.Document.GetSemanticModelAsync(cancellationToken))!;
            SyntaxNode value = root!.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true);
            if (value.Parent is MemberAccessExpressionSyntax access && access.Name == value)
                value = access;
            if (symbol is IMethodSymbol && value.Parent is InvocationExpressionSyntax invocation && invocation.Expression == value)
                value = invocation;

            SyntaxNode? consumer = value.Parent switch
            {
                MemberAccessExpressionSyntax outer when outer.Expression == value => outer,
                ConditionalAccessExpressionSyntax conditional when conditional.Expression == value =>
                    conditional.WhenNotNull.DescendantNodesAndSelf().OfType<MemberBindingExpressionSyntax>().FirstOrDefault(),
                ArgumentSyntax { Parent.Parent: { } call } => call,
                _ => null,
            };

            if (consumer is not null && model.GetSymbolInfo(consumer, cancellationToken).Symbol is { } bound)
                consumers.Add(new TypeRefactoringHelpers.Binding(location.Document.Id, consumer, new SyntaxAnnotation(), bound));
        }

        return consumers;
    }

    private static async Task<Solution> AnnotateAsync(
        Solution solution,
        IReadOnlyList<TypeRefactoringHelpers.Binding> consumers,
        DocumentId declaringDocument,
        SyntaxNode declaration,
        SyntaxAnnotation declarationMark,
        CancellationToken cancellationToken)
    {
        var marks = consumers.ToLookup(c => c.Document);
        foreach (var id in marks.Select(g => g.Key).Append(declaringDocument).Distinct())
        {
            var root = (await solution.GetDocument(id)!.GetSyntaxRootAsync(cancellationToken))!;
            var byNode = marks[id].ToDictionary(c => c.Node, c => c.Mark);
            if (id == declaringDocument)
                byNode[declaration] = declarationMark;

            root = root.ReplaceNodes(byNode.Keys, (original, rewritten) => rewritten.WithAdditionalAnnotations(byNode[original]));
            solution = solution.WithDocumentSyntaxRoot(id, root);
        }

        return solution;
    }

    /// <summary>
    /// Writes the new type in place of the declared one. A variable declared
    /// alongside others gets a declaration of its own, and a nullable type
    /// stays nullable.
    /// </summary>
    private static async Task<Document> WithNewTypeAsync(
        Document document,
        SyntaxAnnotation declarationMark,
        SyntaxAnnotation typeMark,
        string newType,
        CancellationToken cancellationToken)
    {
        var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
        var declaration = root.GetAnnotatedNodes(declarationMark).Single();

        TypeSyntax NewType(TypeSyntax old)
        {
            var parsed = SyntaxFactory.ParseTypeName(newType);
            if (old is NullableTypeSyntax && parsed is not NullableTypeSyntax)
                parsed = SyntaxFactory.NullableType(parsed);
            return parsed.WithTriviaFrom(old).WithAdditionalAnnotations(typeMark);
        }

        SyntaxNode updated = declaration switch
        {
            VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax { Variables.Count: > 1 } variables } variable =>
                SplitDeclaration(root, variables, variable, NewType),
            VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax variables } =>
                root.ReplaceNode(variables.Type, NewType(variables.Type)),
            ParameterSyntax parameter => root.ReplaceNode(parameter.Type!, NewType(parameter.Type!)),
            PropertyDeclarationSyntax property => root.ReplaceNode(property.Type, NewType(property.Type)),
            MethodDeclarationSyntax method => root.ReplaceNode(method.ReturnType, NewType(method.ReturnType)),
            SingleVariableDesignationSyntax { Parent: DeclarationExpressionSyntax expression } =>
                root.ReplaceNode(expression.Type, NewType(expression.Type)),
            ForEachStatementSyntax loop => root.ReplaceNode(loop.Type, NewType(loop.Type)),
            _ => throw new McpException("Error: The declaration has no type to change"),
        };

        return document.WithSyntaxRoot(updated);
    }

    /// <summary>
    /// Moves one variable of a field or local declaration into a declaration
    /// of its own, right after the original, with the new type.
    /// </summary>
    private static SyntaxNode SplitDeclaration(
        SyntaxNode root,
        VariableDeclarationSyntax variables,
        VariableDeclaratorSyntax variable,
        Func<TypeSyntax, TypeSyntax> newType)
    {
        var remaining = variables.WithVariables(variables.Variables.Remove(variable));
        var own = variables
            .WithType(newType(variables.Type))
            .WithVariables(SyntaxFactory.SingletonSeparatedList(variable.WithoutLeadingTrivia()));

        var statement = variables.Parent!;
        var indentation = statement.GetLeadingTrivia().LastOrDefault(t => t.IsKind(SyntaxKind.WhitespaceTrivia));
        SyntaxNode separate = statement switch
        {
            FieldDeclarationSyntax field => field.WithDeclaration(own),
            LocalDeclarationStatementSyntax local => local.WithDeclaration(own),
            _ => throw new McpException("Error: The declaration cannot be split"),
        };
        separate = separate.WithLeadingTrivia(indentation == default ? SyntaxFactory.TriviaList() : SyntaxFactory.TriviaList(indentation));
        SyntaxNode kept = statement switch
        {
            FieldDeclarationSyntax field => field.WithDeclaration(remaining),
            LocalDeclarationStatementSyntax local => local.WithDeclaration(remaining),
            _ => statement,
        };

        return root.ReplaceNode(statement, new[] { kept, separate });
    }

    /// <summary>
    /// Refuses when a use now reaches a different member: another overload,
    /// or an interface member the old one does not implement. A call to the
    /// method whose parameter changed is the same call.
    /// </summary>
    private static async Task EnsureSameBindingsAsync(
        Solution changed,
        IReadOnlyList<TypeRefactoringHelpers.Binding> consumers,
        ISymbol target,
        string description,
        string newType,
        CancellationToken cancellationToken)
    {
        var relevant = target is IParameterSymbol
            ? consumers.Where(c => !SymbolEqualityComparer.Default.Equals(TypeRefactoringHelpers.Definition(c.Bound), target.ContainingSymbol.OriginalDefinition))
            : consumers;
        var changedBinding = await TypeRefactoringHelpers.FirstChangedBindingAsync(changed, relevant, cancellationToken);
        if (changedBinding is not { } found)
            return;

        var at = found.Node.GetLocation().GetLineSpan();
        throw new McpException(
            $"Error: Changing {description} to {newType} would change which member {Path.GetFileName(at.Path)}({at.StartLinePosition.Line + 1}) uses: " +
            $"{found.Now?.ToDisplayString() ?? "nothing"} instead of {found.Binding.Bound.ToDisplayString()}");
    }
}
