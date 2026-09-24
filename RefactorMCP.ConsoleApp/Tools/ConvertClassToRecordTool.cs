using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

[McpServerToolType]
public static class ConvertClassToRecordTool
{
    /// <summary>Methods that compare elements with their Equals, so a record's value equality changes what they find.</summary>
    private static readonly HashSet<string> ComparingMethods = new(StringComparer.Ordinal)
    {
        "Contains", "IndexOf", "LastIndexOf", "Remove", "Distinct", "Union", "Intersect", "Except", "ToHashSet", "SequenceEqual",
    };

    /// <summary>Sets whose element type is hashed.</summary>
    private static readonly HashSet<string> Sets = new(StringComparer.Ordinal)
    {
        "HashSet", "ISet", "IReadOnlySet", "SortedSet", "ImmutableHashSet", "FrozenSet",
    };

    [McpServerTool, Description("Convert a class whose state is fixed by its constructor into a record, positional where the constructor only assigns properties, refusing when record equality or ToString would change behaviour")]
    public static async Task<string> ConvertClassToRecord(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the class")] string filePath,
        [Description("Name of the class")] string typeName,
        [Description("A line of the declaration (1-based), to choose between types of the same name")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var type = await TypeDeclarations.FindTypeAsync(solution, filePath, typeName, line, cancellationToken);
            var declarations = (await TypeDeclarations.DeclarationsAsync(type, cancellationToken)).ToList();

            await EnsureConvertibleAsync(solution, type, declarations, cancellationToken);

            var model = await solution.GetDocument(declarations[0].SyntaxTree)!.GetSemanticModelAsync(cancellationToken);
            var positional = declarations.Count == 1
                ? Positional((ClassDeclarationSyntax)declarations[0], type, model!)
                : null;

            // Positional parameters are named after the properties, so named arguments follow.
            var renames = await TypeDeclarations.NamedArgumentRenamesAsync(
                solution,
                positional?.Constructor,
                positional?.Parameters.ToDictionary(p => p.Parameter.Name, p => p.Property.Identifier.ValueText) ?? new Dictionary<string, string>(),
                cancellationToken);

            var changed = solution;
            var documents = declarations.Select(d => solution.GetDocument(d.SyntaxTree)!.Id).Concat(renames.Select(g => g.Key)).Distinct();
            foreach (var documentId in documents)
            {
                var document = solution.GetDocument(documentId)!;
                var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
                var tree = root.SyntaxTree;
                var ownRenames = renames[document.Id].ToDictionary(r => (SyntaxNode)r.Name, r => r.NewName);
                var classes = declarations.Where(d => d.SyntaxTree == tree).ToList();

                root = root.ReplaceNodes(
                    ownRenames.Keys.Concat(classes),
                    (original, rewritten) => rewritten switch
                    {
                        ClassDeclarationSyntax declaration => ToRecord(declaration, positional),
                        IdentifierNameSyntax name => name.WithIdentifier(
                            SyntaxFactory.Identifier(name.Identifier.LeadingTrivia, ownRenames[original], name.Identifier.TrailingTrivia)),
                        _ => rewritten,
                    });
                changed = changed.WithDocumentSyntaxRoot(document.Id, root);
            }

            await SolutionEdits.EnsureCompilesAsync(solution, changed, cancellationToken);
            await SolutionEdits.WriteAsync(solution, changed, cancellationToken);

            return positional is null
                ? $"Successfully converted class '{typeName}' to a record"
                : $"Successfully converted class '{typeName}' to a positional record";
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error converting class to record: {ex.Message}", ex);
        }
    }

    private static async Task EnsureConvertibleAsync(
        Solution solution,
        INamedTypeSymbol type,
        IReadOnlyList<MemberDeclarationSyntax> declarations,
        CancellationToken cancellationToken)
    {
        if (type.IsRecord)
            throw new McpException($"Error: '{type.Name}' is already a record");

        if (type.TypeKind != TypeKind.Class || type.IsStatic)
            throw new McpException(
                $"Error: '{type.Name}' is {(type.IsStatic ? "a static class" : $"a {type.TypeKind.ToString().ToLowerInvariant()}")}; only a class with instances can become a record");

        TypeDeclarations.EnsureLanguageVersion(declarations[0].SyntaxTree, LanguageVersion.CSharp9, "Records");

        if (type.BaseType is { SpecialType: not SpecialType.System_Object } baseType)
            throw new McpException($"Error: '{type.Name}' is part of a class hierarchy: it derives from '{baseType.Name}'");

        var derived = await SymbolFinder.FindDerivedClassesAsync(type, solution, transitive: false, cancellationToken: cancellationToken);
        if (derived.FirstOrDefault() is { } first)
            throw new McpException($"Error: '{type.Name}' is part of a class hierarchy: '{first.Name}' derives from it");

        foreach (var member in type.GetMembers().Where(m => !m.IsStatic))
        {
            var mutable = member switch
            {
                IFieldSymbol { IsImplicitlyDeclared: false, IsReadOnly: false, IsConst: false } => $"field '{member.Name}' is not readonly",
                IPropertySymbol { SetMethod: { IsInitOnly: false } } => $"property '{member.Name}' has a setter",
                _ => null,
            };
            if (mutable is not null)
                throw new McpException($"Error: '{type.Name}' has mutable state: {mutable}");
        }

        if (type.GetMembers("Equals").OfType<IMethodSymbol>().Any(m => m.IsOverride && m.Parameters.Length == 1))
            throw new McpException($"Error: '{type.Name}' overrides Equals(object), which a record generates itself");

        var overridesToString = type.GetMembers("ToString").OfType<IMethodSymbol>().Any(m => m.IsOverride && m.Parameters.Length == 0);
        foreach (var document in solution.Projects.SelectMany(p => p.Documents))
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var model = (await document.GetSemanticModelAsync(cancellationToken))!;
            foreach (var node in root!.DescendantNodes())
            {
                if (EqualityUse(node, model, type) is { } equality)
                    throw new McpException(
                        $"Error: '{type.Name}' cannot become a record because a record's value equality would change behaviour: it is {equality} at {SolutionEdits.Describe(node.GetLocation())}");

                if (!overridesToString && IsFormatted(node, model, type))
                    throw new McpException(
                        $"Error: '{type.Name}' cannot become a record because a record's ToString would change behaviour: it is formatted as a string at {SolutionEdits.Describe(node.GetLocation())}");
            }
        }
    }

    /// <summary>How a node observes the equality of instances of the type, or null.</summary>
    private static string? EqualityUse(SyntaxNode node, SemanticModel model, INamedTypeSymbol type)
    {
        switch (node)
        {
            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.EqualsExpression) || binary.IsKind(SyntaxKind.NotEqualsExpression):
                return !IsNull(binary.Left) && !IsNull(binary.Right) && (Is(binary.Left, model, type) || Is(binary.Right, model, type))
                    ? $"compared with {binary.OperatorToken.Text}"
                    : null;

            case InvocationExpressionSyntax invocation when model.GetSymbolInfo(invocation).Symbol is IMethodSymbol method:
                if (method.Name is "Equals" or "GetHashCode"
                    && (Receiver(invocation, model, type) || invocation.ArgumentList.Arguments.Any(a => Is(a.Expression, model, type))))
                    return method.Name == "Equals" ? "compared with Equals" : "hashed with GetHashCode";

                for (var i = 0; i < method.TypeArguments.Length; i++)
                {
                    if (IsType(method.TypeArguments[i], type) && method.TypeParameters[i].Name == "TKey")
                        return "used as a dictionary key or set element";
                }

                if (ComparingMethods.Contains(method.Name)
                    && method.TypeArguments.Concat(method.ContainingType.TypeArguments).Any(t => IsType(t, type)))
                    return $"searched or compared with {method.Name}";
                return null;

            case GenericNameSyntax generic when model.GetSymbolInfo(generic).Symbol is INamedTypeSymbol constructed:
                for (var i = 0; i < constructed.TypeArguments.Length; i++)
                {
                    if (IsType(constructed.TypeArguments[i], type)
                        && (constructed.TypeParameters[i].Name == "TKey" || Sets.Contains(constructed.Name)))
                        return "used as a dictionary key or set element";
                }
                return null;

            default:
                return null;
        }
    }

    /// <summary>Whether a node turns an instance into a string through its ToString.</summary>
    private static bool IsFormatted(SyntaxNode node, SemanticModel model, INamedTypeSymbol type) => node switch
    {
        InterpolationSyntax interpolation => Is(interpolation.Expression, model, type),
        BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression) =>
            model.GetTypeInfo(binary).Type?.SpecialType == SpecialType.System_String
            && (Is(binary.Left, model, type) || Is(binary.Right, model, type)),
        InvocationExpressionSyntax invocation =>
            model.GetSymbolInfo(invocation).Symbol is IMethodSymbol { Name: "ToString", Parameters.Length: 0 }
            && Receiver(invocation, model, type),
        _ => false,
    };

    /// <summary>Whether the call is made on an instance of the type, explicitly or through an implicit <c>this</c>.</summary>
    private static bool Receiver(InvocationExpressionSyntax invocation, SemanticModel model, INamedTypeSymbol type) =>
        invocation.Expression switch
        {
            MemberAccessExpressionSyntax access => Is(access.Expression, model, type),
            IdentifierNameSyntax => model.GetSymbolInfo(invocation).Symbol is { IsStatic: false }
                && IsType(model.GetEnclosingSymbol(invocation.SpanStart)?.ContainingType, type),
            _ => false,
        };

    private static bool Is(ExpressionSyntax expression, SemanticModel model, INamedTypeSymbol type) =>
        IsType(model.GetTypeInfo(expression).Type, type);

    private static bool IsType(ITypeSymbol? candidate, INamedTypeSymbol type) =>
        SymbolEqualityComparer.Default.Equals(candidate?.OriginalDefinition, type);

    private static bool IsNull(ExpressionSyntax expression) =>
        expression.IsKind(SyntaxKind.NullLiteralExpression) || expression.IsKind(SyntaxKind.DefaultLiteralExpression);

    /// <summary>A constructor parameter and the property it becomes.</summary>
    private sealed record PositionalParameter(ParameterSyntax Syntax, IParameterSymbol Parameter, PropertyDeclarationSyntax Property);

    /// <summary>The constructor and the properties a positional parameter list replaces.</summary>
    private sealed record PositionalShape(
        ConstructorDeclarationSyntax Syntax,
        IMethodSymbol Constructor,
        IReadOnlyList<PositionalParameter> Parameters);

    /// <summary>
    /// The positional shape when the class's only constructor is public and
    /// just assigns each parameter to a public get-only auto-property of the
    /// same type, and nothing a parameter list cannot carry, such as
    /// attributes or documentation, would be lost. Otherwise null.
    /// </summary>
    private static PositionalShape? Positional(ClassDeclarationSyntax declaration, INamedTypeSymbol type, SemanticModel model)
    {
        var constructors = declaration.Members.OfType<ConstructorDeclarationSyntax>()
            .Where(c => !c.Modifiers.Any(SyntaxKind.StaticKeyword))
            .ToList();
        if (constructors.Count != 1)
            return null;

        var constructor = constructors[0];
        var symbol = model.GetDeclaredSymbol(constructor)!;
        if (symbol.DeclaredAccessibility != Accessibility.Public
            || constructor.Initializer is not null
            || constructor.AttributeLists.Count > 0
            || HasDocumentation(constructor)
            || constructor.Body is null
            || constructor.Body.Statements.Count != symbol.Parameters.Length
            || constructor.ParameterList.Parameters.Any(p => p.Modifiers.Count > 0 || p.AttributeLists.Count > 0))
            return null;

        var assigned = new Dictionary<IParameterSymbol, PropertyDeclarationSyntax>(SymbolEqualityComparer.Default);
        foreach (var statement in constructor.Body.Statements)
        {
            if (statement is not ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment }
                || !assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                || model.GetSymbolInfo(assignment.Right).Symbol is not IParameterSymbol parameter
                || model.GetSymbolInfo(assignment.Left).Symbol is not IPropertySymbol property
                || assignment.Left is not (IdentifierNameSyntax or MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax })
                || !SymbolEqualityComparer.Default.Equals(property.ContainingType, type)
                || !SymbolEqualityComparer.IncludeNullability.Equals(property.Type, parameter.Type)
                || assigned.ContainsKey(parameter)
                || assigned.Values.Any(p => p.Identifier.ValueText == property.Name))
                return null;

            var propertySyntax = declaration.Members.OfType<PropertyDeclarationSyntax>()
                .FirstOrDefault(p => p.Identifier.ValueText == property.Name);
            if (propertySyntax is null || !IsPlainGetOnlyAutoProperty(propertySyntax, property))
                return null;

            assigned[parameter] = propertySyntax;
        }

        var parameters = constructor.ParameterList.Parameters
            .Select(p => model.GetDeclaredSymbol(p)!)
            .Select((p, i) => new PositionalParameter(constructor.ParameterList.Parameters[i], p, assigned[p]))
            .ToList();
        return new PositionalShape(constructor, symbol, parameters);
    }

    private static bool IsPlainGetOnlyAutoProperty(PropertyDeclarationSyntax syntax, IPropertySymbol property) =>
        property.DeclaredAccessibility == Accessibility.Public
        && !property.IsStatic
        && (property.SetMethod is null || property.SetMethod.IsInitOnly)
        && syntax.Initializer is null
        && syntax.AttributeLists.Count == 0
        && !HasDocumentation(syntax)
        && syntax.AccessorList is { } accessors
        && accessors.Accessors.All(a => a.Body is null && a.ExpressionBody is null && a.Modifiers.Count == 0);

    private static bool HasDocumentation(SyntaxNode node) =>
        node.GetLeadingTrivia().Any(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));

    /// <summary>
    /// The record declaration: <c>class</c> becomes <c>record</c>, and for a
    /// positional shape the parameter list replaces the constructor and
    /// properties, the record ending with <c>;</c> when nothing else remains.
    /// </summary>
    private static RecordDeclarationSyntax ToRecord(ClassDeclarationSyntax declaration, PositionalShape? positional)
    {
        var keyword = SyntaxFactory.Token(declaration.Keyword.LeadingTrivia, SyntaxKind.RecordKeyword, declaration.Keyword.TrailingTrivia);
        var members = declaration.Members;
        ParameterListSyntax? parameterList = null;

        if (positional is not null)
        {
            var removed = positional.Parameters.Select(p => p.Property.Identifier.ValueText).ToHashSet();
            var remaining = declaration.Members
                .Where(m => m is not ConstructorDeclarationSyntax c || c.Modifiers.Any(SyntaxKind.StaticKeyword))
                .Where(m => m is not PropertyDeclarationSyntax p || !removed.Contains(p.Identifier.ValueText))
                .ToList();
            if (remaining.Count > 0 && remaining[0] != declaration.Members[0])
                remaining[0] = remaining[0].WithLeadingTrivia(WithoutBlankLines(remaining[0].GetLeadingTrivia()));
            members = SyntaxFactory.List(remaining);

            var nameEnd = declaration.TypeParameterList?.GreaterThanToken ?? declaration.Identifier;
            var parameters = positional.Parameters.Select(p => p.Syntax.WithIdentifier(SyntaxFactory.Identifier(
                p.Syntax.Identifier.LeadingTrivia,
                p.Property.Identifier.ValueText,
                p.Syntax.Identifier.TrailingTrivia)));
            parameterList = SyntaxFactory.ParameterList(
                    SyntaxFactory.SeparatedList(parameters, positional.Syntax.ParameterList.Parameters.GetSeparators()))
                .WithTrailingTrivia(nameEnd.TrailingTrivia);
            declaration = declaration.ReplaceToken(
                declaration.TypeParameterList?.GreaterThanToken ?? declaration.Identifier,
                nameEnd.WithTrailingTrivia());
        }

        var record = SyntaxFactory.RecordDeclaration(
            SyntaxKind.RecordDeclaration,
            declaration.AttributeLists,
            declaration.Modifiers,
            keyword,
            default,
            declaration.Identifier,
            declaration.TypeParameterList,
            parameterList,
            declaration.BaseList,
            declaration.ConstraintClauses,
            declaration.OpenBraceToken,
            members,
            declaration.CloseBraceToken,
            default);

        if (positional is null || members.Count > 0)
            return record;

        // Nothing but the positional state is left: `record Name(...);`.
        var headerEnd = record.OpenBraceToken.GetPreviousToken();
        record = record.ReplaceToken(headerEnd, headerEnd.WithTrailingTrivia());
        return record
            .WithOpenBraceToken(default)
            .WithCloseBraceToken(default)
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken).WithTrailingTrivia(declaration.CloseBraceToken.TrailingTrivia));
    }

    /// <summary>Leading trivia without the blank lines before its first comment or code.</summary>
    private static SyntaxTriviaList WithoutBlankLines(SyntaxTriviaList trivia)
    {
        var list = trivia.ToList();
        while (true)
        {
            if (list.Count > 0 && list[0].IsKind(SyntaxKind.EndOfLineTrivia))
                list.RemoveAt(0);
            else if (list.Count > 1 && list[0].IsKind(SyntaxKind.WhitespaceTrivia) && list[1].IsKind(SyntaxKind.EndOfLineTrivia))
                list.RemoveRange(0, 2);
            else
                return SyntaxFactory.TriviaList(list);
        }
    }
}
