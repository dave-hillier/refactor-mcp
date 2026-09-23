using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

[McpServerToolType]
public static class ConvertTupleToNamedTypeTool
{
    private static readonly SyntaxAnnotation ContainingType = new("TupleContainingType");

    [McpServerTool, Description("Replace a tuple in a method's return type or a parameter with a named positional record, updating the body and callers")]
    public static async Task<string> ConvertTupleToNamedType(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file declaring the method")] string filePath,
        [Description("Name of the method")] string methodName,
        [Description("Name of the new type")] string typeName,
        [Description("The parameter whose tuple type is replaced; the return type when omitted")] string? parameterName = null,
        [Description("struct for a readonly record struct (default), class for a sealed record")] string kind = "struct",
        [Description("A line of the method (1-based), to choose between overloads")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (kind is not ("struct" or "class"))
                throw new McpException($"Error: '{kind}' is not a kind; use struct or class");

            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var method = await SolutionEdits.FindMethodAsync(solution, filePath, methodName, line, cancellationToken);
            var declaration = (MethodDeclarationSyntax)await method.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken);
            var document = solution.GetDocument(declaration.SyntaxTree)!;
            var model = (await document.GetSemanticModelAsync(cancellationToken))!;

            var parameter = parameterName is null ? null : SolutionEdits.FindParameter(method, parameterName);
            var tupleSyntax = parameter is null
                ? declaration.ReturnType
                : declaration.ParameterList.Parameters[parameter.Ordinal].Type!;
            var tuple = (parameter?.Type ?? method.ReturnType) as INamedTypeSymbol;
            if (tuple is not { IsTupleType: true } || tupleSyntax is not TupleTypeSyntax tupleType)
                throw new McpException(
                    $"Error: {(parameter is null ? "The return type" : $"The parameter '{parameter.Name}'")} of '{method.Name}' is not a tuple");

            var unnamed = tuple.TupleElements.FirstOrDefault(e => !e.IsExplicitlyNamedTupleElement);
            if (unnamed is not null)
                throw new McpException($"Error: The tuple element '{unnamed.Name}' has no name to give the property");

            TypeDeclarations.EnsureLanguageVersion(
                declaration.SyntaxTree,
                kind == "struct" ? LanguageVersion.CSharp10 : LanguageVersion.CSharp9,
                kind == "struct" ? "Record structs" : "Records");

            var containing = (TypeDeclarationSyntax)declaration.Parent!;
            if (model.LookupNamespacesAndTypes(containing.SpanStart, name: typeName).Any())
                throw new McpException($"Error: A type named '{typeName}' is already visible where the type would be declared");

            var shape = new Shape(typeName, tuple, tupleType);
            var edits = new Dictionary<DocumentId, Dictionary<SyntaxNode, Func<SyntaxNode, SyntaxNode>>>();
            void Edit(DocumentId id, SyntaxNode node, Func<SyntaxNode, SyntaxNode> change)
            {
                if (!edits.TryGetValue(id, out var nodes))
                    edits[id] = nodes = new Dictionary<SyntaxNode, Func<SyntaxNode, SyntaxNode>>();
                nodes[node] = change;
            }

            Edit(document.Id, tupleSyntax, n => SyntaxFactory.ParseTypeName(shape.GenericName).WithTriviaFrom(n));
            Edit(document.Id, containing, n => n.WithAdditionalAnnotations(ContainingType));

            if (parameter is null)
            {
                foreach (var literal in ReturnedLiterals(declaration))
                    Edit(document.Id, literal, n => shape.Construction((TupleExpressionSyntax)n, shape.GenericName));

                foreach (var (id, access) in await ResultAccessesAsync(solution, method, cancellationToken))
                    Edit(id, access.Name, n => shape.Renamed((SimpleNameSyntax)n));
            }
            else
            {
                var parameterSymbol = method.Parameters[parameter.Ordinal];
                foreach (var access in declaration.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
                    .Where(a => SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(a.Expression, cancellationToken).Symbol, parameterSymbol)))
                    Edit(document.Id, access.Name, n => shape.Renamed((SimpleNameSyntax)n));

                foreach (var (id, literal, typeArguments) in await PassedLiteralsAsync(solution, method, parameter, shape, cancellationToken))
                    Edit(id, literal, n => shape.Construction((TupleExpressionSyntax)n, typeName + typeArguments));
            }

            var changed = solution;
            foreach (var (id, nodes) in edits)
            {
                var root = (await solution.GetDocument(id)!.GetSyntaxRootAsync(cancellationToken))!;
                root = root.ReplaceNodes(nodes.Keys, (original, rewritten) => nodes[original](rewritten));

                if (id == document.Id)
                {
                    var type = root.GetAnnotatedNodes(ContainingType).First();
                    var accessibility = Accessibility(containing);
                    var record = GeneratedMembers.Parse(
                        shape.DeclarationText(accessibility, kind),
                        GeneratedMembers.Indentation(containing.GetFirstToken()),
                        TypeDeclarations.NewLine(root));
                    root = root.InsertNodesAfter(type, new[] { record });
                }

                changed = changed.WithDocumentSyntaxRoot(id, root);
            }

            var errors = await SolutionEdits.NewDiagnosticsAsync(solution, changed, d => d.Severity == DiagnosticSeverity.Error, cancellationToken);
            if (errors.Count > 0)
                throw new McpException(
                    $"Error: A use of the tuple would not compile with '{typeName}': {SolutionEdits.Describe(errors[0])}");

            await SolutionEdits.WriteAsync(solution, changed, cancellationToken);
            return $"Successfully replaced the tuple in '{method.Name}' with '{typeName}'";
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error converting tuple to named type: {ex.Message}", ex);
        }
    }

    /// <summary>The tuple's elements as the record's properties, and the type parameters they use.</summary>
    private sealed class Shape
    {
        private readonly Dictionary<string, string> _properties = new(StringComparer.Ordinal);

        public Shape(string typeName, INamedTypeSymbol tuple, TupleTypeSyntax syntax)
        {
            TypeName = typeName;
            Properties = tuple.TupleElements
                .Select((e, i) => new GeneratedValue(Pascal(e.Name), syntax.Elements[i].Type.ToString()))
                .ToList();

            for (var i = 0; i < tuple.TupleElements.Length; i++)
            {
                _properties[tuple.TupleElements[i].Name] = Properties[i].Name;
                _properties[$"Item{i + 1}"] = Properties[i].Name;
            }

            TypeParameters = tuple.TupleElements
                .SelectMany(e => TypeParametersIn(e.Type))
                .Distinct<ITypeParameterSymbol>(SymbolEqualityComparer.Default)
                .ToList();
        }

        public string TypeName { get; }

        public IReadOnlyList<GeneratedValue> Properties { get; }

        public IReadOnlyList<ITypeParameterSymbol> TypeParameters { get; }

        /// <summary>The type as written inside the method, over the method's own type parameters.</summary>
        public string GenericName =>
            TypeParameters.Count == 0 ? TypeName : $"{TypeName}<{string.Join(", ", TypeParameters.Select(t => t.Name))}>";

        public string DeclarationText(string accessibility, string kind)
        {
            var parameters = string.Join(", ", Properties.Select(p => $"{p.Type} {p.Name}"));
            var keywords = kind == "struct" ? "readonly record struct" : "sealed record";
            return $"{accessibility} {keywords} {GenericName}({parameters});";
        }

        /// <summary><c>new Name(values)</c> from a tuple literal, dropping element names.</summary>
        public SyntaxNode Construction(TupleExpressionSyntax literal, string constructedName) =>
            SyntaxFactory.ObjectCreationExpression(
                    SyntaxFactory.Token(SyntaxKind.NewKeyword).WithTrailingTrivia(SyntaxFactory.Space),
                    SyntaxFactory.ParseTypeName(constructedName),
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SeparatedList(literal.Arguments.Select(a => a.WithNameColon(null)), literal.Arguments.GetSeparators())),
                    null)
                .WithTriviaFrom(literal);

        /// <summary>An element name, or ItemN, as the property it becomes.</summary>
        public SyntaxNode Renamed(SimpleNameSyntax name) =>
            _properties.TryGetValue(name.Identifier.ValueText, out var property)
                ? name.WithIdentifier(SyntaxFactory.Identifier(name.Identifier.LeadingTrivia, property, name.Identifier.TrailingTrivia))
                : name;

        private static string Pascal(string name) => char.ToUpperInvariant(name[0]) + name[1..];

        private static IEnumerable<ITypeParameterSymbol> TypeParametersIn(ITypeSymbol type) => type switch
        {
            ITypeParameterSymbol parameter => new[] { parameter },
            IArrayTypeSymbol array => TypeParametersIn(array.ElementType),
            INamedTypeSymbol named => named.TypeArguments.SelectMany(TypeParametersIn),
            _ => Array.Empty<ITypeParameterSymbol>(),
        };
    }

    /// <summary>Tuple literals the method itself returns, not those of lambdas or local functions inside it.</summary>
    private static IEnumerable<TupleExpressionSyntax> ReturnedLiterals(MethodDeclarationSyntax method)
    {
        if (method.ExpressionBody?.Expression is TupleExpressionSyntax arrow)
            yield return arrow;

        foreach (var statement in method.Body?.DescendantNodes().OfType<ReturnStatementSyntax>() ?? Enumerable.Empty<ReturnStatementSyntax>())
        {
            var owner = statement.Ancestors().First(a => a is MethodDeclarationSyntax or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax);
            if (owner == method && statement.Expression is TupleExpressionSyntax literal)
                yield return literal;
        }
    }

    /// <summary>
    /// Element accesses on the method's result: directly on a call, or on a
    /// <c>var</c> local initialized from one.
    /// </summary>
    private static async Task<List<(DocumentId Document, MemberAccessExpressionSyntax Access)>> ResultAccessesAsync(
        Solution solution,
        IMethodSymbol method,
        CancellationToken cancellationToken)
    {
        var accesses = new List<(DocumentId, MemberAccessExpressionSyntax)>();
        foreach (var location in (await SymbolFinder.FindReferencesAsync(method, solution, cancellationToken)).SelectMany(r => r.Locations))
        {
            var root = await location.Document.GetSyntaxRootAsync(cancellationToken);
            var node = root!.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true);
            var invocation = node.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault(i => i.Expression.Span.Contains(node.Span));
            if (invocation is null)
                continue;

            if (invocation.Parent is MemberAccessExpressionSyntax direct && direct.Expression == invocation)
            {
                accesses.Add((location.Document.Id, direct));
                continue;
            }

            if (invocation.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax variable }
                && variable.Parent is VariableDeclarationSyntax { Type.IsVar: true })
            {
                var model = await location.Document.GetSemanticModelAsync(cancellationToken);
                var local = model!.GetDeclaredSymbol(variable, cancellationToken);
                var scope = variable.Ancestors().OfType<BlockSyntax>().FirstOrDefault() ?? variable.Parent.Parent!;
                accesses.AddRange(scope.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
                    .Where(a => SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(a.Expression, cancellationToken).Symbol, local))
                    .Select(a => (location.Document.Id, a)));
            }
        }

        return accesses;
    }

    /// <summary>
    /// Tuple literals callers pass for the parameter, with the type arguments
    /// the named type needs at each call.
    /// </summary>
    private static async Task<List<(DocumentId Document, TupleExpressionSyntax Literal, string TypeArguments)>> PassedLiteralsAsync(
        Solution solution,
        IMethodSymbol method,
        IParameterSymbol parameter,
        Shape shape,
        CancellationToken cancellationToken)
    {
        var literals = new List<(DocumentId, TupleExpressionSyntax, string)>();
        foreach (var location in (await SymbolFinder.FindReferencesAsync(method, solution, cancellationToken)).SelectMany(r => r.Locations))
        {
            var root = await location.Document.GetSyntaxRootAsync(cancellationToken);
            var node = root!.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true);
            var invocation = node.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault(i => i.Expression.Span.Contains(node.Span));
            if (invocation is null)
                continue;

            var arguments = invocation.ArgumentList.Arguments;
            var argument = arguments.FirstOrDefault(a => a.NameColon?.Name.Identifier.ValueText == parameter.Name)
                ?? (parameter.Ordinal < arguments.Count && arguments[parameter.Ordinal].NameColon is null ? arguments[parameter.Ordinal] : null);
            if (argument?.Expression is not TupleExpressionSyntax literal)
                continue;

            var model = await location.Document.GetSemanticModelAsync(cancellationToken);
            var invoked = (IMethodSymbol)model!.GetSymbolInfo(invocation, cancellationToken).Symbol!;
            var typeArguments = shape.TypeParameters.Count == 0
                ? ""
                : $"<{string.Join(", ", shape.TypeParameters.Select(t => (t.DeclaringMethod is not null ? invoked.TypeArguments[t.Ordinal] : invoked.ContainingType.TypeArguments[t.Ordinal]).ToMinimalDisplayString(model, invocation.SpanStart)))}>";
            literals.Add((location.Document.Id, literal, typeArguments));
        }

        return literals;
    }

    /// <summary>The accessibility of the type containing the method, which the new type shares.</summary>
    private static string Accessibility(TypeDeclarationSyntax type)
    {
        var stated = string.Join(" ", type.Modifiers
            .Where(m => m.IsKind(SyntaxKind.PublicKeyword) || m.IsKind(SyntaxKind.InternalKeyword) || m.IsKind(SyntaxKind.ProtectedKeyword) || m.IsKind(SyntaxKind.PrivateKeyword))
            .Select(m => m.Text));
        return stated.Length > 0 ? stated : type.Parent is TypeDeclarationSyntax ? "private" : "internal";
    }
}
