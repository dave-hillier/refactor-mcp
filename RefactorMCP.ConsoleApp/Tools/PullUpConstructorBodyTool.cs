using ModelContextProtocol.Server;
using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using System.ComponentModel;
using static HierarchyMemberHelpers;

[McpServerToolType]
public static class PullUpConstructorBodyTool
{
    [McpServerTool, Description("Move the leading statements of a constructor that only set up the base class into a base constructor, and chain to it")]
    public static async Task<string> PullUpConstructorBody(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the constructor")] string filePath,
        [Description("Name of the class declaring the constructor")] string className,
        [Description("Line of the constructor's declaration, to choose between several (optional)")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (solution, document) = await TypeRefactoringHelpers.LoadDocumentAsync(solutionPath, filePath, cancellationToken);
            var (type, declaration) = await TypeRefactoringHelpers.FindTypeAsync(document, className, cancellationToken);
            var constructors = declaration.Members.OfType<ConstructorDeclarationSyntax>()
                .Where(c => !c.Modifiers.Any(SyntaxKind.StaticKeyword))
                .ToList();
            var constructor = TypeRefactoringHelpers.Choose(constructors, c => c.Identifier, $"Constructor of {className}", line);
            var model = (await document.GetSemanticModelAsync(cancellationToken))!;
            var symbol = model.GetDeclaredSymbol(constructor, cancellationToken)!;
            var baseClass = await SourceBaseAsync(solution, type, cancellationToken);

            if (constructor.Initializer is { } chained
                && (chained.IsKind(SyntaxKind.ThisConstructorInitializer) || chained.ArgumentList.Arguments.Count > 0))
            {
                throw new McpException($"Error: The constructor already calls {chained.ThisOrBaseKeyword.Text}(...), so its statements cannot move to another base constructor");
            }

            var map = TowardsBase(type);
            var moved = constructor.Body?.Statements
                .TakeWhile(s => Movable(s, model, type, symbol, map))
                .ToList() ?? new List<StatementSyntax>();
            if (moved.Count == 0)
                throw new McpException($"Error: The constructor's first statement needs {className}, so nothing can move to {baseClass.Symbol.Name}");

            // The base constructor takes the parameters the moved statements read, in the constructor's order.
            var parameters = symbol.Parameters
                .Where(p => moved.Any(s => s.DescendantNodes().OfType<IdentifierNameSyntax>()
                    .Any(n => SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(n, cancellationToken).Symbol, p))))
                .ToList();

            // The base constructor the subclass runs today must still run first.
            var runsBaseCode = await RunsCodeAsync(CurrentBaseConstructor(constructor, type, model), cancellationToken);

            var edits = new TrackedEdits(solution);
            var eol = TypeRefactoringHelpers.EndOfLine(constructor.SyntaxTree.GetRoot());
            var existing = type.BaseType!.InstanceConstructors.FirstOrDefault(c => !c.IsImplicitlyDeclared
                && c.Parameters.Length == parameters.Count
                && c.Parameters.Zip(parameters).All(p => SymbolEqualityComparer.Default.Equals(p.First.Type, p.Second.Type)));

            if (existing is not null)
            {
                var existingSyntax = (ConstructorDeclarationSyntax)await existing.OriginalDefinition.DeclaringSyntaxReferences.First().GetSyntaxAsync(cancellationToken);
                if (!SameStatements(existingSyntax, existing, moved, model, parameters, runsBaseCode))
                    throw new McpException($"Error: {baseClass.Symbol.Name} already has a constructor taking these parameters that does something else");
            }
            else
            {
                var baseConstructor = BaseConstructor(baseClass.Symbol, constructor, parameters, moved, model, map, runsBaseCode, eol);
                var abstractBase = baseClass.Symbol.IsAbstract;
                var needsParameterless = baseClass.Symbol.InstanceConstructors.All(c => c.IsImplicitlyDeclared)
                    && await OthersNeedParameterlessAsync(solution, baseClass.Symbol, symbol, cancellationToken);

                edits.Replace(baseClass.Document, baseClass.Declaration, t =>
                {
                    if (needsParameterless)
                        t = InsertMember(t, Parameterless(baseClass.Symbol.Name, abstractBase, eol), eol);
                    return InsertMember(t, baseConstructor, eol);
                });
                edits.Import(baseClass.Document, moved.SelectMany(s => TypeRefactoringHelpers.NamespacesUsedBy(s, model))
                    .Concat(parameters.SelectMany(p => TypeRefactoringHelpers.NamespacesUsedBy(constructor.ParameterList.Parameters[p.Ordinal], model))));
            }

            edits.Replace(document, constructor, c => Chained(c, moved.Count, parameters, eol));

            await TypeRefactoringHelpers.ApplyIfCompilesAsync(
                solution,
                await edits.ApplyAsync(cancellationToken),
                errors => $"Error: Pulling up the constructor body would break the build: {TypeRefactoringHelpers.Describe(errors)}",
                cancellationToken);

            return $"Successfully moved {moved.Count} statement(s) from the {className} constructor into a {baseClass.Symbol.Name} constructor";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error pulling up constructor body: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// A statement can run in the base constructor when it assigns a field or
    /// non-virtual property the base class has, from the constructor's
    /// parameters and what the base class can see. Anything that calls an
    /// instance method could reach an override before the subclass is ready,
    /// so it stays.
    /// </summary>
    private static bool Movable(
        StatementSyntax statement,
        SemanticModel model,
        INamedTypeSymbol subclass,
        IMethodSymbol constructor,
        IReadOnlyDictionary<ITypeParameterSymbol, TypeSyntax> map)
    {
        if (statement is not ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment })
            return false;

        var assigned = model.GetSymbolInfo(assignment.Left).Symbol;
        if (assigned is not (IFieldSymbol or IPropertySymbol) || assigned.IsStatic
            || assigned is IPropertySymbol { IsVirtual: true } or IPropertySymbol { IsAbstract: true } or IPropertySymbol { IsOverride: true })
        {
            return false;
        }

        if (SubclassOnlyUse(statement, model, subclass, constructor, map) is not null)
            return false;

        foreach (var node in statement.DescendantNodesAndSelf())
        {
            if (node is ThisExpressionSyntax && node.Parent is not MemberAccessExpressionSyntax)
                return false;

            if (node is not SimpleNameSyntax name)
                continue;

            var used = model.GetSymbolInfo(name).Symbol;
            var ok = used switch
            {
                IParameterSymbol parameter => !SymbolEqualityComparer.Default.Equals(parameter.ContainingSymbol, constructor)
                    || constructor.Parameters.Contains(parameter, SymbolEqualityComparer.Default),
                ILocalSymbol local => local.DeclaringSyntaxReferences.All(r => statement.Span.Contains(r.Span)),
                IMethodSymbol { IsStatic: false, MethodKind: MethodKind.Ordinary } method =>
                    !IsInstanceMemberOf(method, subclass) || name.Parent is MemberAccessExpressionSyntax access && access.Name == name && access.Expression is not ThisExpressionSyntax and not BaseExpressionSyntax,
                IPropertySymbol { IsStatic: false } property => !(property.IsVirtual || property.IsAbstract || property.IsOverride) || !IsInstanceMemberOf(property, subclass),
                _ => true,
            };

            if (!ok)
                return false;
        }

        return true;
    }

    /// <summary>Whether a member belongs to the subclass or a class above it, so is reached through <c>this</c>.</summary>
    private static bool IsInstanceMemberOf(ISymbol member, INamedTypeSymbol subclass)
    {
        for (var type = subclass; type is not null; type = type.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, member.ContainingType.OriginalDefinition))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Whether an existing base constructor already does what the moved
    /// statements do, once its parameter names are read as the subclass's.
    /// </summary>
    private static bool SameStatements(
        ConstructorDeclarationSyntax existing,
        IMethodSymbol existingSymbol,
        IReadOnlyList<StatementSyntax> moved,
        SemanticModel model,
        IReadOnlyList<IParameterSymbol> parameters,
        bool mustChainToParameterless)
    {
        var chainsToParameterless = existing.Initializer is { ArgumentList.Arguments.Count: 0 } initializer
            && initializer.IsKind(SyntaxKind.ThisConstructorInitializer);
        if (existing.Body is null
            || existing.Initializer is { ArgumentList.Arguments.Count: > 0 }
            || mustChainToParameterless && !chainsToParameterless)
        {
            return false;
        }

        var renamed = moved.Select(s => Shape(RenameParameters(s, model, parameters, existingSymbol)));

        return string.Join(" ", renamed) == string.Join(" ", existing.Body.Statements.Select(Shape));
    }

    private static StatementSyntax RenameParameters(
        StatementSyntax statement,
        SemanticModel model,
        IReadOnlyList<IParameterSymbol> parameters,
        IMethodSymbol target)
    {
        var uses = statement.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(n => model.GetSymbolInfo(n).Symbol is IParameterSymbol p && parameters.Contains(p, SymbolEqualityComparer.Default))
            .ToList();

        return statement.ReplaceNodes(uses, (original, _) =>
        {
            var index = parameters.ToList().FindIndex(p => SymbolEqualityComparer.Default.Equals(p, model.GetSymbolInfo(original).Symbol));
            return SyntaxFactory.IdentifierName(target.Parameters[index].Name).WithTriviaFrom(original);
        });
    }

    private static ConstructorDeclarationSyntax BaseConstructor(
        INamedTypeSymbol baseClass,
        ConstructorDeclarationSyntax constructor,
        IReadOnlyList<IParameterSymbol> parameters,
        IReadOnlyList<StatementSyntax> moved,
        SemanticModel model,
        IReadOnlyDictionary<ITypeParameterSymbol, TypeSyntax> map,
        bool chainToParameterless,
        SyntaxTrivia eol)
    {
        var parameterList = SyntaxFactory.SeparatedList(
            parameters.Select(p => Substitute(constructor.ParameterList.Parameters[p.Ordinal], model, map)
                .WithAttributeLists(default)
                .WithDefault(null)
                .WithoutTrivia()),
            parameters.Skip(1).Select(_ => SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space)));

        var statements = moved.Select(s => Substitute(s, model, map)).ToList();
        statements[0] = statements[0].WithLeadingTrivia(WithoutLeadingBlankLines(statements[0].GetLeadingTrivia()));

        var declaration = SyntaxFactory.ConstructorDeclaration(baseClass.Name)
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.ProtectedKeyword).WithTrailingTrivia(SyntaxFactory.Space)))
            .WithParameterList(SyntaxFactory.ParameterList(parameterList))
            .WithBody(SyntaxFactory.Block(statements))
            .WithAdditionalAnnotations(Formatter.Annotation);

        if (!chainToParameterless)
            return declaration;

        return declaration
            .WithParameterList(declaration.ParameterList.WithTrailingTrivia(eol))
            .WithInitializer(SyntaxFactory.ConstructorInitializer(SyntaxKind.ThisConstructorInitializer)
                .WithColonToken(SyntaxFactory.Token(SyntaxKind.ColonToken)
                    .WithLeadingTrivia(SyntaxFactory.Whitespace("    "))
                    .WithTrailingTrivia(SyntaxFactory.Space))
                .WithTrailingTrivia(eol));
    }

    /// <summary>The base constructor the subclass constructor runs before its own statements.</summary>
    private static IMethodSymbol? CurrentBaseConstructor(ConstructorDeclarationSyntax constructor, INamedTypeSymbol type, SemanticModel model) =>
        constructor.Initializer is { } initializer
            ? model.GetSymbolInfo(initializer).Symbol as IMethodSymbol
            : type.BaseType!.InstanceConstructors.FirstOrDefault(c => c.Parameters.Length == 0);

    /// <summary>Whether a constructor does anything: an empty, unchained or implicit one does not.</summary>
    private static async Task<bool> RunsCodeAsync(IMethodSymbol? constructor, CancellationToken cancellationToken)
    {
        if (constructor is null || constructor.IsImplicitlyDeclared)
            return false;

        var reference = constructor.OriginalDefinition.DeclaringSyntaxReferences.FirstOrDefault();
        if (reference is null)
            return true;

        var syntax = (ConstructorDeclarationSyntax)await reference.GetSyntaxAsync(cancellationToken);
        return syntax.Initializer is not null || syntax.Body is not { Statements.Count: 0 };
    }

    /// <summary>The parameterless constructor the implicit one provided, with the accessibility it had in effect.</summary>
    private static ConstructorDeclarationSyntax Parameterless(string name, bool isAbstract, SyntaxTrivia eol) =>
        SyntaxFactory.ConstructorDeclaration(name)
            .WithModifiers(SyntaxFactory.TokenList(
                SyntaxFactory.Token(isAbstract ? SyntaxKind.ProtectedKeyword : SyntaxKind.PublicKeyword).WithTrailingTrivia(SyntaxFactory.Space)))
            .WithParameterList(SyntaxFactory.ParameterList().WithTrailingTrivia(eol))
            .WithBody(SyntaxFactory.Block()
                .WithOpenBraceToken(SyntaxFactory.Token(SyntaxKind.OpenBraceToken).WithTrailingTrivia(eol))
                .WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken).WithTrailingTrivia(eol)))
            .WithAdditionalAnnotations(Formatter.Annotation);

    /// <summary>
    /// Whether anything besides the constructor being changed relies on the
    /// base class's implicit parameterless constructor, which declaring
    /// another constructor takes away: a subclass constructor that chains to
    /// it, a subclass with no constructor of its own, or code creating the
    /// base class directly.
    /// </summary>
    private static async Task<bool> OthersNeedParameterlessAsync(
        Solution solution,
        INamedTypeSymbol baseClass,
        IMethodSymbol changing,
        CancellationToken cancellationToken)
    {
        foreach (var subclass in await SubclassesAsync(solution, baseClass, transitive: false, cancellationToken))
        {
            foreach (var other in subclass.Symbol.InstanceConstructors)
            {
                if (SymbolEqualityComparer.Default.Equals(other.OriginalDefinition, changing.OriginalDefinition))
                    continue;

                if (other.IsImplicitlyDeclared)
                    return true;

                var syntax = (ConstructorDeclarationSyntax)await other.DeclaringSyntaxReferences.First().GetSyntaxAsync(cancellationToken);
                if (syntax.Initializer is null || syntax.Initializer.IsKind(SyntaxKind.BaseConstructorInitializer) && syntax.Initializer.ArgumentList.Arguments.Count == 0)
                    return true;
            }
        }

        var implicitConstructor = baseClass.InstanceConstructors.First();
        var references = await SymbolFinder.FindReferencesAsync(implicitConstructor, solution, cancellationToken);
        return references.SelectMany(r => r.Locations).Any();
    }

    /// <summary>The constructor without the moved statements, chaining to the base constructor that now runs them.</summary>
    private static ConstructorDeclarationSyntax Chained(
        ConstructorDeclarationSyntax constructor,
        int movedCount,
        IReadOnlyList<IParameterSymbol> parameters,
        SyntaxTrivia eol)
    {
        var remaining = constructor.Body!.Statements.Skip(movedCount).ToList();
        if (remaining.Count > 0)
            remaining[0] = remaining[0].WithLeadingTrivia(WithoutLeadingBlankLines(remaining[0].GetLeadingTrivia()));

        var arguments = SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(
            parameters.Select(p => SyntaxFactory.Argument(SyntaxFactory.IdentifierName(p.Name))),
            parameters.Skip(1).Select(_ => SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space))));

        var body = constructor.Body.WithStatements(SyntaxFactory.List(remaining));
        if (constructor.Initializer is { } empty)
            return constructor.WithInitializer(empty.WithArgumentList(arguments.WithTriviaFrom(empty.ArgumentList))).WithBody(body);

        // The initializer goes on its own line when the body's brace does.
        var closeParen = constructor.ParameterList.CloseParenToken;
        var ownLine = closeParen.TrailingTrivia.Any(t => t.IsKind(SyntaxKind.EndOfLineTrivia));
        var indentation = constructor.GetLeadingTrivia().LastOrDefault(t => t.IsKind(SyntaxKind.WhitespaceTrivia)).ToString() + "    ";

        var initializer = SyntaxFactory.ConstructorInitializer(SyntaxKind.BaseConstructorInitializer, arguments)
            .WithColonToken(SyntaxFactory.Token(SyntaxKind.ColonToken)
                .WithLeadingTrivia(ownLine ? SyntaxFactory.Whitespace(indentation) : SyntaxFactory.Space)
                .WithTrailingTrivia(SyntaxFactory.Space))
            .WithTrailingTrivia(ownLine ? SyntaxFactory.TriviaList(eol) : SyntaxFactory.TriviaList(SyntaxFactory.Space));

        var parameterList = constructor.ParameterList.WithCloseParenToken(
            closeParen.WithTrailingTrivia(ownLine ? SyntaxFactory.TriviaList(eol) : default));

        return constructor
            .WithParameterList(parameterList)
            .WithInitializer(initializer)
            .WithBody(body);
    }
}
