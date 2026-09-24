using ModelContextProtocol.Server;
using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using System.ComponentModel;

[McpServerToolType]
public static class SafeDeleteSymbolTool
{
    [McpServerTool, Description("Delete an unused method, property, field or event, refusing when anything refers to it, overrides it or relies on it to implement an interface")]
    public static async Task<string> SafeDeleteMember(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the member")] string filePath,
        [Description("Name of the member to delete")] string memberName,
        [Description("Line of the member's declaration, to choose between overloads (1-based, optional)")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var symbol = await SolutionEdits.FindMemberAsync(solution, filePath, memberName, line, cancellationToken);
            if (symbol is INamedTypeSymbol)
                throw new McpException($"Error: '{memberName}' is a type; delete it with safe-delete-type");
            if (symbol is not (IMethodSymbol { MethodKind: MethodKind.Ordinary } or IPropertySymbol or IFieldSymbol or IEventSymbol))
                throw new McpException($"Error: '{memberName}' is not a method, property, field or event");

            await EnsureNothingReliesOnAsync(solution, symbol, cancellationToken);
            await EnsureUnreferencedAsync(solution, symbol, new[] { symbol }, $"'{Display(symbol)}' is referenced", cancellationToken);

            var declaration = symbol.DeclaringSyntaxReferences.Single().GetSyntax(cancellationToken);
            var document = solution.GetDocument(declaration.SyntaxTree)!;
            var root = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync(cancellationToken))!;
            var changed = solution.WithDocumentSyntaxRoot(document.Id, WithoutMember(root, declaration));

            await SolutionEdits.EnsureCompilesAsync(solution, changed, cancellationToken);
            await TypeRefactoringHelpers.ApplyAsync(solution, changed, cancellationToken);
            return $"Successfully deleted {Display(symbol)}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error deleting member: {ex.Message}", ex);
        }
    }

    [McpServerTool, Description("Delete an unused type, and each file left with nothing else in it, refusing when anything refers to the type or its members")]
    public static async Task<string> SafeDeleteType(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the type")] string filePath,
        [Description("Name of the type to delete")] string typeName,
        [Description("Line of the type's declaration, to choose between types of the same name (1-based, optional)")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var symbol = await SolutionEdits.FindMemberAsync(solution, filePath, typeName, line, cancellationToken);
            if (symbol is not INamedTypeSymbol type)
                throw new McpException($"Error: '{typeName}' is not a type; delete a member with safe-delete-member");

            // Extension methods are called without naming their class, so the
            // members are checked as well as the type.
            await EnsureUnreferencedAsync(solution, type, new[] { type }, $"'{Display(type)}' is referenced", cancellationToken);
            foreach (var member in Members(type))
                await EnsureUnreferencedAsync(solution, member, new[] { type }, $"'{Display(type)}' is referenced through {Display(member)}", cancellationToken);

            var changed = solution;
            var deletedFiles = new List<string>();
            foreach (var group in type.DeclaringSyntaxReferences.GroupBy(r => r.SyntaxTree))
            {
                var document = solution.GetDocument(group.Key)!;
                var root = (CompilationUnitSyntax)(await group.Key.GetRootAsync(cancellationToken));
                var declarations = group.Select(r => (MemberDeclarationSyntax)r.GetSyntax(cancellationToken)).ToList();
                var remaining = DeclarationRemoval.RemoveMembers(root, declarations);
                if (DeclarationRemoval.IsEmpty(remaining))
                {
                    changed = changed.RemoveDocument(document.Id);
                    deletedFiles.Add(Path.GetFileName(document.FilePath!));
                }
                else
                {
                    changed = changed.WithDocumentSyntaxRoot(document.Id, remaining);
                }
            }

            await SolutionEdits.EnsureCompilesAsync(solution, changed, cancellationToken);
            await TypeRefactoringHelpers.ApplyAsync(solution, changed, cancellationToken);

            var files = deletedFiles.Count == 0 ? "" : $" and {string.Join(", ", deletedFiles)}";
            return $"Successfully deleted {Display(type)}{files}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error deleting type: {ex.Message}", ex);
        }
    }

    [McpServerTool, Description("Delete an unused local variable, keeping an initializer with side effects as a statement")]
    public static async Task<string> SafeDeleteLocal(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line of the local's declaration (1-based)")] int line,
        [Description("Column of the local's name (1-based)")] int column,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var target = await LocalVariableTarget.FindAsync(solutionPath, filePath, line, column, cancellationToken);
            var statement = target.DeclarationStatement
                ?? throw new McpException($"Error: '{target.Name}' is not declared by a local declaration statement");
            if (!statement.UsingKeyword.IsKind(SyntaxKind.None))
                throw new McpException($"Error: '{target.Name}' is a using declaration; deleting it would skip its disposal");

            var references = target.References().ToList();
            if (references.Count > 0)
            {
                throw new McpException(
                    $"Error: '{target.Name}' is referenced {references.Count} time(s): {string.Join(", ", references.Take(3).Select(r => SolutionEdits.Describe(r.GetLocation())))}");
            }

            var replacement = ReplacementStatements(target, statement);
            if (replacement.Count == 0)
            {
                var editor = await target.EditorAsync();
                target.RemoveDeclarationStatement(editor);
                await target.WriteAsync(editor);
            }
            else
            {
                var statements = target.SiblingStatements()!.Value;
                var index = statements.IndexOf(statement);
                await target.WriteStatementsAsync(statements.Take(index).Concat(replacement).Concat(statements.Skip(index + 1)));
            }

            return $"Successfully deleted local '{target.Name}'";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error deleting local: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Refuses to delete a member that overrides another, is overridden or
    /// implemented, or implements an interface member: calls elsewhere reach
    /// it without naming it.
    /// </summary>
    private static async Task EnsureNothingReliesOnAsync(Solution solution, ISymbol symbol, CancellationToken cancellationToken)
    {
        var overridden = symbol switch
        {
            IMethodSymbol method => (ISymbol?)method.OverriddenMethod,
            IPropertySymbol property => property.OverriddenProperty,
            IEventSymbol @event => @event.OverriddenEvent,
            _ => null,
        };
        if (symbol.IsOverride && overridden is not null)
            throw new McpException($"Error: '{Display(symbol)}' overrides {Display(overridden)}; deleting it would change what calls through the base class run");

        if (symbol.IsVirtual || symbol.IsAbstract)
        {
            var overrides = await SymbolFinder.FindOverridesAsync(symbol, solution, cancellationToken: cancellationToken);
            if (overrides.FirstOrDefault() is { } first)
                throw new McpException($"Error: '{Display(symbol)}' is overridden by {Display(first)}");
        }

        if (symbol.ContainingType.TypeKind == TypeKind.Interface)
        {
            var implementations = await SymbolFinder.FindImplementationsAsync(symbol, solution, cancellationToken: cancellationToken);
            if (implementations.FirstOrDefault() is { } first)
                throw new McpException($"Error: '{Display(symbol)}' is implemented by {Display(first)}");
        }

        var implemented = symbol.ContainingType.AllInterfaces
            .SelectMany(i => i.GetMembers())
            .FirstOrDefault(m => SymbolEqualityComparer.Default.Equals(symbol.ContainingType.FindImplementationForInterfaceMember(m), symbol));
        if (implemented is not null)
            throw new McpException($"Error: '{Display(symbol)}' implements {Display(implemented)}");
    }

    /// <summary>
    /// Refuses when <paramref name="symbol"/> is referenced from outside the
    /// declarations of <paramref name="deleted"/>, which go with it.
    /// </summary>
    private static async Task EnsureUnreferencedAsync(
        Solution solution,
        ISymbol symbol,
        IEnumerable<ISymbol> deleted,
        string message,
        CancellationToken cancellationToken)
    {
        var spans = deleted.SelectMany(d => d.DeclaringSyntaxReferences).ToList();
        var references = await SymbolFinder.FindReferencesAsync(symbol, solution, cancellationToken);
        var outside = references
            .SelectMany(r => r.Locations)
            .Select(l => l.Location)
            .Where(l => l.IsInSource && !spans.Any(s => s.SyntaxTree == l.SourceTree && s.Span.Contains(l.SourceSpan)))
            .Distinct()
            .ToList();

        if (outside.Count > 0)
            throw new McpException($"Error: {message} {outside.Count} time(s): {string.Join(", ", outside.Take(3).Select(SolutionEdits.Describe))}");
    }

    /// <summary>The members of a type and its nested types that code outside could use.</summary>
    private static IEnumerable<ISymbol> Members(INamedTypeSymbol type) =>
        type.GetMembers()
            .Where(m => !m.IsImplicitlyDeclared && m is not INamedTypeSymbol)
            .Concat(type.GetTypeMembers().SelectMany(nested => Members(nested).Prepend(nested)));

    /// <summary>
    /// The file without the member: a field or event declared alongside
    /// others loses just its declarator.
    /// </summary>
    private static SyntaxNode WithoutMember(CompilationUnitSyntax root, SyntaxNode declaration)
    {
        if (declaration is VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax { Variables.Count: > 1 } variables } declarator)
            return root.ReplaceNode(variables, variables.WithVariables(variables.Variables.Remove(declarator)));

        var member = declaration.AncestorsAndSelf().OfType<MemberDeclarationSyntax>().First();
        return DeclarationRemoval.RemoveMembers(root, new[] { member });
    }

    /// <summary>
    /// What replaces a local declaration statement when the local goes: the
    /// other locals it declares, and the initializer when running it has side
    /// effects. Empty when the statement can simply be removed.
    /// </summary>
    private static List<StatementSyntax> ReplacementStatements(LocalVariableTarget target, LocalDeclarationStatementSyntax statement)
    {
        var variables = target.Declaration.Variables;
        var index = variables.IndexOf(target.Declarator);
        var initializer = target.Declarator.Initializer?.Value;
        var effect = initializer is not null && HasSideEffects(initializer) ? EffectStatement(target, initializer) : null;
        if (effect is null && variables.Count == 1)
            return new List<StatementSyntax>();

        if (effect is null)
            return new List<StatementSyntax> { statement.WithDeclaration(target.Declaration.WithVariables(variables.RemoveAt(index))) };

        // The other locals are declared either side of the effect, so every
        // initializer still runs in the order it was written.
        var pieces = new List<StatementSyntax>();
        if (index > 0)
            pieces.Add(Declaring(statement, variables.Take(index)));
        pieces.Add(effect);
        if (index < variables.Count - 1)
            pieces.Add(Declaring(statement, variables.Skip(index + 1)));

        var indentation = statement.GetLeadingTrivia().LastOrDefault(t => t.IsKind(SyntaxKind.WhitespaceTrivia));
        return pieces
            .Select((piece, i) => piece
                .WithLeadingTrivia(i == 0 ? statement.GetLeadingTrivia() : SyntaxFactory.TriviaList(indentation))
                .WithTrailingTrivia(statement.GetTrailingTrivia()))
            .ToList();
    }

    private static LocalDeclarationStatementSyntax Declaring(LocalDeclarationStatementSyntax statement, IEnumerable<VariableDeclaratorSyntax> variables)
    {
        var list = variables.ToList();
        list[^1] = list[^1].WithoutTrailingTrivia();
        return statement.WithDeclaration(statement.Declaration.WithVariables(SyntaxFactory.SeparatedList(
            list,
            Enumerable.Repeat(SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space), list.Count - 1))));
    }

    /// <summary>
    /// The initializer as a statement: itself when C# allows it as one,
    /// otherwise assigned to a discard.
    /// </summary>
    private static StatementSyntax EffectStatement(LocalVariableTarget target, ExpressionSyntax initializer)
    {
        var expression = initializer.WithoutTrivia().ToFullString();
        if (initializer is InvocationExpressionSyntax or ObjectCreationExpressionSyntax or AssignmentExpressionSyntax or AwaitExpressionSyntax
            || initializer.IsKind(SyntaxKind.PreIncrementExpression) || initializer.IsKind(SyntaxKind.PreDecrementExpression)
            || initializer.IsKind(SyntaxKind.PostIncrementExpression) || initializer.IsKind(SyntaxKind.PostDecrementExpression))
        {
            return SyntaxFactory.ParseStatement($"{expression};");
        }

        if (target.Model.LookupSymbols(initializer.SpanStart, name: "_").Any())
            throw new McpException($"Error: The initializer of '{target.Name}' has side effects and '_' names a variable, so it cannot be discarded");

        return SyntaxFactory.ParseStatement($"_ = {expression};");
    }

    /// <summary>
    /// Whether evaluating an expression could do something beyond producing a
    /// value: call code, create an object, assign, or index. Lambda bodies do
    /// not run when the lambda is created, so they do not count.
    /// </summary>
    private static bool HasSideEffects(ExpressionSyntax expression) =>
        expression.DescendantNodesAndSelf(node => node is not AnonymousFunctionExpressionSyntax)
            .Any(node => node is InvocationExpressionSyntax
                or BaseObjectCreationExpressionSyntax
                or AssignmentExpressionSyntax
                or AwaitExpressionSyntax
                or ElementAccessExpressionSyntax
                || node.IsKind(SyntaxKind.PreIncrementExpression)
                || node.IsKind(SyntaxKind.PreDecrementExpression)
                || node.IsKind(SyntaxKind.PostIncrementExpression)
                || node.IsKind(SyntaxKind.PostDecrementExpression));

    private static string Display(ISymbol symbol) => symbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
}
