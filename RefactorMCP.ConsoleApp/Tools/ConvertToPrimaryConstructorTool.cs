using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

[McpServerToolType]
public static class ConvertToPrimaryConstructorTool
{
    private static readonly SyntaxAnnotation ParameterUse = new("PrimaryConstructorParameterUse");

    [McpServerTool, Description("Replace a constructor that only assigns its parameters to fields and properties with a primary constructor (C# 12)")]
    public static async Task<string> ConvertToPrimaryConstructor(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file declaring the class or struct")] string filePath,
        [Description("Name of the class or struct")] string typeName,
        [Description("A line of the declaration (1-based), to choose between types of the same name")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var type = await TypeDeclarations.FindTypeAsync(solution, filePath, typeName, line, cancellationToken);
            var declaration = await TypeDeclarations.SingleDeclarationAsync(type, cancellationToken) as TypeDeclarationSyntax;

            if (type.IsRecord || declaration is not (ClassDeclarationSyntax or StructDeclarationSyntax) || declaration.ParameterList is not null)
                throw new McpException($"Error: '{typeName}' is not a class or struct without a primary constructor, so it cannot be given one");

            if (type.DeclaringSyntaxReferences.Length > 1)
                throw new McpException($"Error: '{typeName}' is not a class or struct with a single declaration; merge its parts first");

            TypeDeclarations.EnsureLanguageVersion(declaration.SyntaxTree, LanguageVersion.CSharp12, "Primary constructors on classes and structs");

            var document = solution.GetDocument(declaration.SyntaxTree)!;
            var model = (await document.GetSemanticModelAsync(cancellationToken))!;
            var constructor = SingleConstructor(type, cancellationToken);
            var assignments = Assignments(constructor, type, model);
            var captured = await CapturedFieldsAsync(solution, constructor, assignments, cancellationToken);

            var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
            var fieldUses = new Dictionary<SyntaxNode, string>();
            foreach (var (field, parameter) in captured)
            {
                foreach (var use in await UsesOutsideAsync(solution, field, constructor, cancellationToken))
                    fieldUses[use] = parameter.Name;
            }

            var initialized = assignments.Where(a => !captured.ContainsKey(a.Member)).ToList();
            var initializerTargets = initialized.ToDictionary(
                a => DeclaringNode(a.Member, cancellationToken),
                a => a.Parameter.Name);
            var removedFields = captured.Keys.Select(f => DeclaringNode(f, cancellationToken)).ToHashSet();

            root = root.ReplaceNodes(
                fieldUses.Keys.Concat(initializerTargets.Keys).Append(declaration),
                (original, rewritten) =>
                {
                    if (original == declaration)
                        return ToPrimary((TypeDeclarationSyntax)rewritten, declaration, constructor, removedFields);
                    if (initializerTargets.TryGetValue(original, out var initializer))
                        return WithInitializer(rewritten, initializer);
                    return SyntaxFactory.IdentifierName(fieldUses[original])
                        .WithTriviaFrom(rewritten)
                        .WithAdditionalAnnotations(ParameterUse);
                });

            var changed = solution.WithDocumentSyntaxRoot(document.Id, root);
            await SolutionEdits.EnsureCompilesAsync(solution, changed, cancellationToken);
            await EnsureUsesReadParametersAsync(changed.GetDocument(document.Id)!, cancellationToken);
            await SolutionEdits.WriteAsync(solution, changed, cancellationToken);

            return $"Successfully gave '{typeName}' a primary constructor";
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error converting to a primary constructor: {ex.Message}", ex);
        }
    }

    /// <summary>A parameter the constructor assigns to a field or auto-property.</summary>
    private sealed record Assignment(ISymbol Member, IParameterSymbol Parameter);

    private static ConstructorDeclarationSyntax SingleConstructor(INamedTypeSymbol type, CancellationToken cancellationToken)
    {
        var constructors = type.InstanceConstructors.Where(c => !c.IsImplicitlyDeclared).ToList();
        if (constructors.Count != 1)
            throw new McpException(
                $"Error: '{type.Name}' has {constructors.Count} constructors; a primary constructor replaces exactly one");

        var constructor = (ConstructorDeclarationSyntax)constructors[0].DeclaringSyntaxReferences[0].GetSyntax(cancellationToken);
        if (constructors[0].DeclaredAccessibility != Accessibility.Public)
            throw new McpException(
                $"Error: The constructor of '{type.Name}' is {SyntaxFacts.GetText(constructors[0].DeclaredAccessibility)}, but a primary constructor is public");

        if (constructor.AttributeLists.Count > 0 || constructor.GetLeadingTrivia().Any(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)))
            throw new McpException(
                $"Error: The constructor of '{type.Name}' has attributes or documentation, which a primary constructor has nowhere to keep");

        if (constructor.Initializer is { } initializer && initializer.IsKind(SyntaxKind.ThisConstructorInitializer))
            throw new McpException($"Error: The constructor of '{type.Name}' calls another constructor with this(...)");

        return constructor;
    }

    /// <summary>
    /// Each statement of the body as a parameter assigned to a field or
    /// auto-property of the type, refusing anything else.
    /// </summary>
    private static List<Assignment> Assignments(ConstructorDeclarationSyntax constructor, INamedTypeSymbol type, SemanticModel model)
    {
        McpException Logic(string detail) =>
            new($"Error: The constructor of '{type.Name}' does more than assign its parameters to fields and auto-properties: {detail}");

        if (constructor.Body is null)
            throw Logic("it has an expression body");

        var assignments = new List<Assignment>();
        foreach (var statement in constructor.Body.Statements)
        {
            if (statement is not ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment }
                || !assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                || assignment.Left is not (IdentifierNameSyntax or MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax })
                || model.GetSymbolInfo(assignment.Right).Symbol is not IParameterSymbol parameter
                || model.GetSymbolInfo(assignment.Left).Symbol is not { IsStatic: false } member
                || !SymbolEqualityComparer.Default.Equals(member.ContainingType, type)
                || member is not (IFieldSymbol or IPropertySymbol)
                || member is IPropertySymbol property && !IsAutoProperty(property))
            {
                throw Logic($"'{statement.ToString().Trim()}' is not a parameter assigned to a field or auto-property");
            }

            if (assignments.Any(a => SymbolEqualityComparer.Default.Equals(a.Member, member)))
                throw Logic($"'{member.Name}' is assigned twice");

            assignments.Add(new Assignment(member, parameter));
        }

        var passedToBase = constructor.Initializer?.ArgumentList.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Select(n => model.GetSymbolInfo(n).Symbol)
            .OfType<IParameterSymbol>()
            .ToList() ?? new List<IParameterSymbol>();
        foreach (var parameter in model.GetDeclaredSymbol(constructor)!.Parameters)
        {
            if (!assignments.Any(a => SymbolEqualityComparer.Default.Equals(a.Parameter, parameter))
                && !passedToBase.Contains(parameter, SymbolEqualityComparer.Default))
                throw Logic($"parameter '{parameter.Name}' is not used");
        }

        return assignments;
    }

    private static bool IsAutoProperty(IPropertySymbol property) =>
        property.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is PropertyDeclarationSyntax
        {
            ExpressionBody: null,
            AccessorList: { } accessors,
        }
        && accessors.Accessors.All(a => a.Body is null && a.ExpressionBody is null);

    /// <summary>
    /// The fields that can give way to the parameter they hold: private, with
    /// no attributes or comments, assigned only by the constructor, read only
    /// through <c>this</c>, from a parameter that has no other job.
    /// </summary>
    private static async Task<Dictionary<ISymbol, IParameterSymbol>> CapturedFieldsAsync(
        Solution solution,
        ConstructorDeclarationSyntax constructor,
        IReadOnlyList<Assignment> assignments,
        CancellationToken cancellationToken)
    {
        var passedToBase = constructor.Initializer?.ArgumentList.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Select(n => n.Identifier.ValueText)
            .ToHashSet() ?? new HashSet<string>();
        var captured = new Dictionary<ISymbol, IParameterSymbol>(SymbolEqualityComparer.Default);

        foreach (var assignment in assignments)
        {
            if (assignment.Member is not IFieldSymbol { DeclaredAccessibility: Accessibility.Private } field
                || assignments.Count(a => SymbolEqualityComparer.Default.Equals(a.Parameter, assignment.Parameter)) > 1
                || passedToBase.Contains(assignment.Parameter.Name))
                continue;

            var variable = (VariableDeclaratorSyntax)DeclaringNode(field, cancellationToken);
            var fieldDeclaration = (FieldDeclarationSyntax)variable.Parent!.Parent!;
            if (fieldDeclaration.AttributeLists.Count > 0
                || fieldDeclaration.GetLeadingTrivia().Any(t => !t.IsKind(SyntaxKind.WhitespaceTrivia) && !t.IsKind(SyntaxKind.EndOfLineTrivia))
                || variable.Initializer is not null)
                continue;

            var uses = await UsesOutsideAsync(solution, field, constructor, cancellationToken);
            if (uses.All(u => u is IdentifierNameSyntax or MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } && !IsWritten(u)))
                captured[field] = assignment.Parameter;
        }

        return captured;
    }

    /// <summary>
    /// Every use of a field outside the constructor: the simple name, or the
    /// whole <c>this.field</c> or <c>other.field</c> access.
    /// </summary>
    private static async Task<List<ExpressionSyntax>> UsesOutsideAsync(
        Solution solution,
        ISymbol field,
        ConstructorDeclarationSyntax constructor,
        CancellationToken cancellationToken)
    {
        var uses = new List<ExpressionSyntax>();
        foreach (var reference in await SymbolFinder.FindReferencesAsync(field, solution, cancellationToken))
        {
            foreach (var location in reference.Locations)
            {
                if (location.Location.SourceTree == constructor.SyntaxTree && constructor.Span.Contains(location.Location.SourceSpan))
                    continue;

                var root = await location.Location.SourceTree!.GetRootAsync(cancellationToken);
                var name = (SimpleNameSyntax)root.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true);
                uses.Add(name.Parent is MemberAccessExpressionSyntax access && access.Name == name ? access : name);
            }
        }

        return uses;
    }

    private static bool IsWritten(ExpressionSyntax use) => use.Parent switch
    {
        AssignmentExpressionSyntax assignment => assignment.Left == use,
        PrefixUnaryExpressionSyntax or PostfixUnaryExpressionSyntax => true,
        ArgumentSyntax argument => !argument.RefKindKeyword.IsKind(SyntaxKind.None),
        _ => false,
    };

    private static SyntaxNode DeclaringNode(ISymbol member, CancellationToken cancellationToken) =>
        member.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken);

    /// <summary>A field declarator or property initialized from a parameter.</summary>
    private static SyntaxNode WithInitializer(SyntaxNode member, string parameter)
    {
        var value = SyntaxFactory.EqualsValueClause(
            SyntaxFactory.Token(SyntaxKind.EqualsToken).WithLeadingTrivia(SyntaxFactory.Space).WithTrailingTrivia(SyntaxFactory.Space),
            SyntaxFactory.IdentifierName(parameter));

        return member switch
        {
            VariableDeclaratorSyntax variable => variable.WithInitializer(value),
            PropertyDeclarationSyntax property => property
                .WithAccessorList(property.AccessorList!.WithTrailingTrivia())
                .WithInitializer(value)
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken).WithTrailingTrivia(property.AccessorList!.GetTrailingTrivia())),
            _ => member,
        };
    }

    /// <summary>
    /// The type with the constructor's parameters after its name, the base
    /// call on its base type, and the constructor and captured fields gone.
    /// </summary>
    private static TypeDeclarationSyntax ToPrimary(
        TypeDeclarationSyntax type,
        TypeDeclarationSyntax original,
        ConstructorDeclarationSyntax constructor,
        IReadOnlySet<SyntaxNode> removedFields)
    {
        // Members are matched by position, since the rewritten type holds new nodes.
        var members = new List<MemberDeclarationSyntax>();
        for (var i = 0; i < original.Members.Count; i++)
        {
            var member = type.Members[i];
            if (original.Members[i] == constructor)
                continue;

            if (original.Members[i] is FieldDeclarationSyntax originalField && member is FieldDeclarationSyntax field)
            {
                var kept = field.Declaration.Variables
                    .Where((_, index) => !removedFields.Contains(originalField.Declaration.Variables[index]))
                    .ToList();
                if (kept.Count == 0)
                    continue;
                if (kept.Count < field.Declaration.Variables.Count)
                    member = field.WithDeclaration(field.Declaration.WithVariables(SyntaxFactory.SeparatedList(kept)));
            }

            members.Add(member);
        }

        if (members.Count > 0 && type.Members.Count > 0 && members[0] != type.Members[0])
            members[0] = members[0].WithLeadingTrivia(WithoutBlankLines(members[0].GetLeadingTrivia()));

        var nameEnd = type.TypeParameterList?.GreaterThanToken ?? type.Identifier;
        var parameters = constructor.ParameterList
            .WithoutTrivia()
            .WithTrailingTrivia(nameEnd.TrailingTrivia);

        var result = type
            .ReplaceToken(nameEnd, nameEnd.WithTrailingTrivia())
            .WithMembers(SyntaxFactory.List(members));
        result = result.WithParameterList(parameters);

        if (constructor.Initializer is { } initializer && result.BaseList is { } baseList)
        {
            var first = baseList.Types[0];
            var primary = SyntaxFactory.PrimaryConstructorBaseType(first.Type.WithoutTrailingTrivia(), initializer.ArgumentList.WithoutTrivia())
                .WithTrailingTrivia(first.GetTrailingTrivia());
            result = result.WithBaseList(baseList.WithTypes(baseList.Types.Replace(first, primary)));
        }

        return result;
    }

    /// <summary>Leading trivia without the blank lines before its first comment or code.</summary>
    internal static SyntaxTriviaList WithoutBlankLines(SyntaxTriviaList trivia)
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

    /// <summary>Every former field use must now read the primary constructor's parameter.</summary>
    private static async Task EnsureUsesReadParametersAsync(Document document, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var model = await document.GetSemanticModelAsync(cancellationToken);
        foreach (var use in root!.GetAnnotatedNodes(ParameterUse))
        {
            if (model!.GetSymbolInfo(use, cancellationToken).Symbol is not IParameterSymbol { ContainingSymbol: IMethodSymbol { MethodKind: MethodKind.Constructor } } parameter
                || parameter.ContainingSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(cancellationToken) is not TypeDeclarationSyntax)
                throw new McpException(
                    $"Error: '{use}' at {SolutionEdits.Describe(use.GetLocation())} would not read the primary constructor's parameter, because another symbol of that name is in scope");
        }
    }
}
