using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

[McpServerToolType]
public static class ReplaceConstructorWithFactoryMethodTool
{
    private static readonly Dictionary<string, SyntaxKind[]> Accessibilities = new(StringComparer.Ordinal)
    {
        ["public"] = new[] { SyntaxKind.PublicKeyword },
        ["internal"] = new[] { SyntaxKind.InternalKeyword },
        ["protected"] = new[] { SyntaxKind.ProtectedKeyword },
        ["private"] = new[] { SyntaxKind.PrivateKeyword },
        ["protected internal"] = new[] { SyntaxKind.ProtectedKeyword, SyntaxKind.InternalKeyword },
        ["private protected"] = new[] { SyntaxKind.PrivateKeyword, SyntaxKind.ProtectedKeyword },
    };

    /// <summary>Compiler errors that mean something still calls the constructor directly.</summary>
    private static readonly HashSet<string> StillNeeded = new(StringComparer.Ordinal)
    {
        "CS0122", // inaccessible due to its protection level
        "CS0310", // must have a public parameterless constructor for a new() constraint
    };

    [McpServerTool, Description("Add a static factory method for a constructor, narrow the constructor, and call the factory wherever the constructor was called")]
    public static async Task<string> ReplaceConstructorWithFactoryMethod(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the constructor")] string filePath,
        [Description("Name of the type whose constructor is replaced")] string typeName,
        [Description("A line of the constructor (1-based), to choose between overloads")] int? line = null,
        [Description("Name of the factory method (default Create)")] string methodName = "Create",
        [Description("New accessibility of the constructor (default private)")] string accessibility = "private",
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Accessibilities.TryGetValue(accessibility.Trim(), out var keywords))
                throw new McpException(
                    $"Error: '{accessibility}' is not an accessibility; use {string.Join(", ", Accessibilities.Keys)}");

            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var constructor = await SolutionEdits.FindMethodAsync(solution, filePath, typeName, line, cancellationToken);
            if (constructor.MethodKind != MethodKind.Constructor)
                throw new McpException($"Error: '{typeName}' at the given line is not a constructor");

            var type = constructor.ContainingType;
            if (type.IsAbstract || type.IsStatic || type.TypeKind != TypeKind.Class)
                throw new McpException(
                    $"Error: '{type.Name}' is not a concrete class, so a factory method cannot create it");

            EnsureNameIsFree(type, constructor, methodName);

            var declaration = (ConstructorDeclarationSyntax)await constructor.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken);
            var creations = await CreationsAsync(solution, constructor, cancellationToken);

            var containing = (TypeDeclarationSyntax)declaration.Parent!;
            var constructorIndex = containing.Members.IndexOf(declaration);
            var declaringId = solution.GetDocument(declaration.SyntaxTree)!.Id;

            var changed = solution;
            foreach (var documentId in creations.Select(c => c.Document.Id).Append(declaringId).Distinct())
            {
                var document = solution.GetDocument(documentId)!;
                var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
                var model = (await document.GetSemanticModelAsync(cancellationToken))!;
                var calls = creations
                    .Where(c => c.Document.Id == documentId)
                    .ToDictionary(c => (SyntaxNode)c.Expression, c => FactoryCall(c.Expression, model, methodName));
                var nodes = documentId == declaringId ? calls.Keys.Append(containing) : calls.Keys;

                root = root.ReplaceNodes(nodes, (original, rewritten) => original == containing
                    ? WithFactory((TypeDeclarationSyntax)rewritten, constructorIndex, methodName, keywords)
                    : calls[original]);
                changed = changed.WithDocumentSyntaxRoot(documentId, root);
            }

            await EnsureCompilesAsync(solution, changed, type, cancellationToken);
            await SolutionEdits.WriteAsync(solution, changed, cancellationToken);

            return $"Successfully replaced {creations.Count} construction(s) of '{type.Name}' with '{type.Name}.{methodName}'";
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error replacing constructor with factory method: {ex.Message}", ex);
        }
    }

    private static void EnsureNameIsFree(INamedTypeSymbol type, IMethodSymbol constructor, string methodName)
    {
        foreach (var member in type.GetMembers(methodName))
        {
            var clashes = member is not IMethodSymbol method
                || method.Parameters.Select(p => p.Type).SequenceEqual(constructor.Parameters.Select(p => p.Type), SymbolEqualityComparer.Default);
            if (clashes)
                throw new McpException(
                    $"Error: '{type.Name}' already has a member named '{methodName}' that the factory method would clash with");
        }
    }

    private sealed record Creation(Document Document, BaseObjectCreationExpressionSyntax Expression);

    /// <summary>Every <c>new</c> expression in the solution that calls the constructor.</summary>
    private static async Task<List<Creation>> CreationsAsync(Solution solution, IMethodSymbol constructor, CancellationToken cancellationToken)
    {
        var creations = new List<Creation>();
        foreach (var reference in await SymbolFinder.FindReferencesAsync(constructor, solution, cancellationToken))
        {
            if (!SymbolEqualityComparer.Default.Equals(reference.Definition.OriginalDefinition, constructor.OriginalDefinition))
                continue;

            foreach (var location in reference.Locations)
            {
                var root = await location.Document.GetSyntaxRootAsync(cancellationToken);
                var creation = root!.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true)
                    .AncestorsAndSelf()
                    .TakeWhile(n => n is not StatementSyntax and not MemberDeclarationSyntax)
                    .OfType<BaseObjectCreationExpressionSyntax>()
                    .FirstOrDefault();
                if (creation is null)
                    continue;

                if (creation.Initializer is not null)
                    throw new McpException(
                        $"Error: The creation at {SolutionEdits.Describe(creation.GetLocation())} uses an object initializer, which a factory method call cannot carry");

                creations.Add(new Creation(location.Document, creation));
            }
        }

        return creations;
    }

    /// <summary><c>Type.Factory(arguments)</c>, naming the type a target-typed <c>new</c> creates.</summary>
    private static ExpressionSyntax FactoryCall(BaseObjectCreationExpressionSyntax creation, SemanticModel model, string methodName)
    {
        var type = creation is ObjectCreationExpressionSyntax explicitType
            ? explicitType.Type.WithoutTrivia()
            : SyntaxFactory.ParseTypeName(model.GetTypeInfo(creation).Type!.ToMinimalDisplayString(model, creation.SpanStart));

        return SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, type, SyntaxFactory.IdentifierName(methodName)),
                creation.ArgumentList!.WithoutTrailingTrivia())
            .WithTriviaFrom(creation);
    }

    /// <summary>The type with the constructor narrowed and the factory method after it.</summary>
    private static TypeDeclarationSyntax WithFactory(
        TypeDeclarationSyntax type,
        int constructorIndex,
        string methodName,
        SyntaxKind[] keywords)
    {
        var constructor = (ConstructorDeclarationSyntax)type.Members[constructorIndex];
        var accessibility = string.Join(" ", constructor.Modifiers.Where(IsAccessibility).Select(m => m.Text));
        var typeName = type.Identifier.ValueText + type.TypeParameterList;
        var arguments = string.Join(", ", constructor.ParameterList.Parameters.Select(p =>
            string.Concat(p.Modifiers.Where(m => m.IsKind(SyntaxKind.RefKeyword) || m.IsKind(SyntaxKind.OutKeyword) || m.IsKind(SyntaxKind.InKeyword)).Select(m => m.Text + " "))
            + p.Identifier.Text));
        var factory = GeneratedMembers.Parse(
            $"{(accessibility.Length == 0 ? "private" : accessibility)} static {typeName} {methodName}{constructor.ParameterList.WithoutTrivia()} => new {typeName}({arguments});",
            GeneratedMembers.Indentation(constructor.GetFirstToken()),
            TypeDeclarations.NewLine(type));

        var members = type.Members
            .Replace(constructor, WithAccessibility(constructor, keywords))
            .Insert(constructorIndex + 1, factory);
        return type.WithMembers(members);
    }

    private static bool IsAccessibility(SyntaxToken token) =>
        token.IsKind(SyntaxKind.PublicKeyword) || token.IsKind(SyntaxKind.InternalKeyword)
        || token.IsKind(SyntaxKind.ProtectedKeyword) || token.IsKind(SyntaxKind.PrivateKeyword);

    /// <summary>
    /// Replaces the accessibility keywords where they stood, or puts the new
    /// ones first, taking the declaration's leading trivia.
    /// </summary>
    private static ConstructorDeclarationSyntax WithAccessibility(ConstructorDeclarationSyntax constructor, SyntaxKind[] keywords)
    {
        var modifiers = constructor.Modifiers;
        var tokens = keywords.Select(k => SyntaxFactory.Token(k).WithTrailingTrivia(SyntaxFactory.Space)).ToList();

        var first = modifiers.IndexOf(modifiers.FirstOrDefault(IsAccessibility));
        if (first >= 0)
        {
            var last = modifiers.IndexOf(modifiers.Last(IsAccessibility));
            tokens[0] = tokens[0].WithLeadingTrivia(modifiers[first].LeadingTrivia);
            tokens[^1] = tokens[^1].WithTrailingTrivia(modifiers[last].TrailingTrivia);
            var kept = modifiers.Where(m => !IsAccessibility(m)).ToList();
            kept.InsertRange(modifiers.Take(first).Count(m => !IsAccessibility(m)), tokens);
            return constructor.WithModifiers(SyntaxFactory.TokenList(kept));
        }

        var start = constructor.AttributeLists.Count > 0
            ? constructor.AttributeLists.Last().GetLastToken().GetNextToken()
            : constructor.GetFirstToken();
        tokens[0] = tokens[0].WithLeadingTrivia(start.LeadingTrivia);
        var stripped = constructor.ReplaceToken(start, start.WithLeadingTrivia());
        return stripped.WithModifiers(SyntaxFactory.TokenList(tokens.Concat(stripped.Modifiers)));
    }

    private static async Task EnsureCompilesAsync(Solution solution, Solution changed, INamedTypeSymbol type, CancellationToken cancellationToken)
    {
        var errors = await SolutionEdits.NewDiagnosticsAsync(solution, changed, d => d.Severity == DiagnosticSeverity.Error, cancellationToken);
        if (errors.Count == 0)
            return;

        var needed = errors.FirstOrDefault(e => StillNeeded.Contains(e.Id));
        if (needed is not null)
            throw new McpException(
                $"Error: The constructor of '{type.Name}' is still needed at its old accessibility: {SolutionEdits.Describe(needed)}");

        throw new McpException($"Error: The change would not compile: {SolutionEdits.Describe(errors[0])}");
    }
}
