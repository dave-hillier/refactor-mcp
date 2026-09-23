using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.Collections.Immutable;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Simplification;

[McpServerToolType]
public static class ReplaceExpressionWithFieldTool
{
    [McpServerTool, Description("Replace an expression with a readonly field that every construction of the class sets, through a constructor parameter, to an equivalent value")]
    public static async Task<string> ReplaceExpressionWithField(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file containing the expression")] string filePath,
        [Description("Name of the readonly field to use in place of the expression")] string fieldName,
        [Description("Range of the expression in format 'startLine:startColumn-endLine:endColumn'; or give memberName and expression")] string? selectionRange = null,
        [Description("Name of the member whose occurrences of the expression are replaced, when no range is given")] string? memberName = null,
        [Description("The expression as C#, with memberName")] string? expression = null,
        [Description("A line of the member's declaration (1-based), to choose between overloads")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var document = RefactoringHelpers.GetDocumentByPath(solution, filePath)
                ?? throw new McpException($"Error: File {filePath} not found in solution");
            var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
            var model = (await document.GetSemanticModelAsync(cancellationToken))!;
            var targets = await TargetsAsync(solution, document, root, selectionRange, memberName, expression, line, cancellationToken);
            var described = targets[0].ToString();

            var type = targets[0].Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
            var typeSymbol = type == null ? null : model.GetDeclaredSymbol(type, cancellationToken);
            var field = typeSymbol?.GetMembers(fieldName).OfType<IFieldSymbol>().FirstOrDefault(f => !f.IsStatic && !f.IsConst)
                ?? throw new McpException($"Error: '{typeSymbol?.Name}' has no instance field named '{fieldName}'");
            if (!field.IsReadOnly)
                throw new McpException($"Error: '{fieldName}' is not readonly, so code other than the constructors could change it");

            var values = new List<IOperation>();
            foreach (var target in targets)
            {
                if (!SymbolEqualityComparer.Default.Equals(model.GetTypeInfo(target, cancellationToken).Type, field.Type))
                    throw new McpException($"Error: '{fieldName}' is not of the expression's type '{model.GetTypeInfo(target, cancellationToken).Type?.ToDisplayString()}'");
                var member = InstanceMember(target, type!, model)
                    ?? throw new McpException($"Error: {described} is not in an instance method, property or accessor of '{typeSymbol!.Name}', which run once the object is constructed");
                if (member.IsOverride)
                    throw new McpException($"Error: '{member.Name}' overrides a base member, so it could run before '{fieldName}' is assigned, from a base constructor");

                var value = model.GetOperation(target, cancellationToken);
                if (!IsFixed(value))
                    throw new McpException($"Error: {described} is not built only from constants and constructions, so a constructor could not have been passed its value");
                values.Add(value!);
            }

            foreach (var (constructor, parameter) in await AssigningConstructorsAsync(solution, field, cancellationToken))
                await EnsureEveryConstructionPassesAsync(solution, constructor, parameter, values, described, cancellationToken);

            var replaced = root.ReplaceNodes(targets, (original, _) => SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.ThisExpression(),
                    SyntaxFactory.IdentifierName(field.Name))
                .WithAdditionalAnnotations(Simplifier.Annotation)
                .WithTriviaFrom(original));
            var changedDocument = await Simplifier.ReduceAsync(document.WithSyntaxRoot(replaced), Simplifier.Annotation, cancellationToken: cancellationToken);

            var changed = changedDocument.Project.Solution;
            await SolutionEdits.EnsureCompilesAsync(solution, changed, cancellationToken);
            await SolutionEdits.WriteAsync(solution, changed, cancellationToken);
            return $"Successfully replaced {targets.Count} occurrence(s) of {described} with '{fieldName}'";
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error replacing expression with field: {ex.Message}", ex);
        }
    }

    /// <summary>The selected expression, or every outermost occurrence of the expression in the member.</summary>
    private static async Task<List<ExpressionSyntax>> TargetsAsync(
        Solution solution,
        Document document,
        SyntaxNode root,
        string? selectionRange,
        string? memberName,
        string? expression,
        int? line,
        CancellationToken cancellationToken)
    {
        if (selectionRange != null)
        {
            var text = await document.GetTextAsync(cancellationToken);
            var span = RefactoringHelpers.ParseSelectionRange(text, selectionRange);
            var selected = FieldPropertyRefactoring.SelectedExpression(root, text, span)
                ?? throw new McpException("Error: The selection is not an expression");
            return new List<ExpressionSyntax> { selected };
        }

        if (memberName == null || expression == null)
            throw new McpException("Error: Give a selection range, or a member name and an expression");

        var member = await SolutionEdits.FindMemberAsync(solution, document.FilePath!, memberName, line, cancellationToken);
        var wanted = SyntaxFactory.ParseExpression(expression);
        var occurrences = member.DeclaringSyntaxReferences
            .Where(r => r.SyntaxTree == root.SyntaxTree)
            .SelectMany(r => r.GetSyntax(cancellationToken).DescendantNodes().OfType<ExpressionSyntax>())
            .Where(e => SyntaxFactory.AreEquivalent(e, wanted))
            .ToList();
        occurrences = occurrences.Where(o => !occurrences.Any(other => other != o && other.Span.Contains(o.Span))).ToList();
        return occurrences.Count > 0
            ? occurrences
            : throw new McpException($"Error: '{memberName}' does not contain the expression {expression}");
    }

    /// <summary>
    /// The instance method, property, indexer or event the expression is in, when it is a
    /// member of the type itself and not an init accessor or property initializer, which run
    /// while the object is constructed; otherwise null.
    /// </summary>
    private static ISymbol? InstanceMember(ExpressionSyntax expression, TypeDeclarationSyntax type, SemanticModel model)
    {
        var member = expression.Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault();
        if (member is not (MethodDeclarationSyntax or BasePropertyDeclarationSyntax) || member.Parent != type)
            return null;
        if (expression.Ancestors().OfType<AccessorDeclarationSyntax>().Any(a => a.IsKind(SyntaxKind.InitAccessorDeclaration))
            || (member is PropertyDeclarationSyntax { Initializer: { } initializer } && initializer.Span.Contains(expression.Span)))
            return null;

        return model.GetDeclaredSymbol(member) is { IsStatic: false } symbol ? symbol : null;
    }

    /// <summary>
    /// A value that is the same wherever it is evaluated: a constant, a construction without
    /// an initializer whose arguments are fixed, a static readonly field, or <c>typeof</c>.
    /// </summary>
    private static bool IsFixed(IOperation? operation) => operation switch
    {
        null => false,
        { ConstantValue.HasValue: true } => true,
        IObjectCreationOperation creation => creation.Initializer == null && creation.Arguments.All(a => IsFixed(a.Value)),
        IFieldReferenceOperation field => field.Instance == null && field.Field.IsReadOnly,
        ITypeOfOperation => true,
        IConversionOperation conversion => conversion.OperatorMethod == null && IsFixed(conversion.Operand),
        _ => false,
    };

    /// <summary>
    /// Each constructor that runs its own body, with the parameter it assigns the field from.
    /// Every such constructor must assign the field exactly once, as a statement of its body,
    /// from a required parameter of the field's type that it never writes, and must not
    /// return early; a constructor calling <c>this(...)</c> must not assign it; nothing else
    /// may. Before the assignment, the constructor must not use the object, since a member
    /// it calls would see the field unassigned.
    /// </summary>
    private static async Task<List<(IMethodSymbol Constructor, IParameterSymbol Parameter)>> AssigningConstructorsAsync(
        Solution solution,
        IFieldSymbol field,
        CancellationToken cancellationToken)
    {
        var notFromParameter = new McpException(
            $"Error: '{field.Name}' is not assigned from a parameter in every constructor, once, before anything else can use it");
        if (field.DeclaringSyntaxReferences.Select(r => r.GetSyntax(cancellationToken)).OfType<VariableDeclaratorSyntax>().Any(v => v.Initializer != null)
            || field.ContainingType.InstanceConstructors.Any(c => c.IsImplicitlyDeclared))
            throw notFromParameter;

        var assigning = new List<(IMethodSymbol, IParameterSymbol)>();
        var assignments = new List<SyntaxNode>();
        foreach (var constructor in field.ContainingType.InstanceConstructors)
        {
            if (constructor.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(cancellationToken) is not ConstructorDeclarationSyntax declaration)
                throw notFromParameter;

            var model = (await solution.GetDocument(declaration.SyntaxTree)!.GetSemanticModelAsync(cancellationToken))!;
            var writes = Writes(declaration, field, model).ToList();
            if (declaration.Initializer?.ThisOrBaseKeyword.IsKind(SyntaxKind.ThisKeyword) == true)
            {
                if (writes.Count > 0)
                    throw notFromParameter;
                continue;
            }

            if (declaration.Body == null
                || writes is not [var write]
                || write.Ancestors().OfType<ExpressionStatementSyntax>().FirstOrDefault() is not { Expression: AssignmentExpressionSyntax assignment } statement
                || statement.Parent != declaration.Body
                || !assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                || assignment.Left != FieldPropertyRefactoring.ReferenceExpression((ExpressionSyntax)write)
                || model.GetSymbolInfo(assignment.Right, cancellationToken).Symbol is not IParameterSymbol parameter
                || !SymbolEqualityComparer.Default.Equals(parameter.ContainingSymbol, constructor)
                || parameter is not { RefKind: RefKind.None, IsOptional: false, IsParams: false }
                || !SymbolEqualityComparer.Default.Equals(parameter.Type, field.Type)
                || Writes(declaration, parameter, model).Any()
                || declaration.Body.DescendantNodes(n => n is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax)
                    .OfType<ReturnStatementSyntax>().Any())
                throw notFromParameter;

            if (declaration.Body.Statements.TakeWhile(s => s != statement).Any(s => UsesObject(s, model)))
                throw new McpException($"Error: The constructor uses the object before assigning '{field.Name}', so a member it calls could run before the field is assigned");

            assigning.Add((constructor, parameter));
            assignments.Add(write);
        }

        // A readonly field can also be written by an init accessor, which would replace the value.
        var references = await SymbolFinder.FindReferencesAsync(field, solution, cancellationToken);
        foreach (var location in references.SelectMany(r => r.Locations).Where(l => l.Location.IsInSource))
        {
            var root = (await location.Document.GetSyntaxRootAsync(cancellationToken))!;
            var name = root.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true);
            if (name is ExpressionSyntax reference && FieldPropertyRefactoring.IsWrite(reference)
                && !assignments.Any(a => a.SyntaxTree == name.SyntaxTree && a.Span == name.Span))
                throw notFromParameter;
        }

        return assigning;
    }

    /// <summary>The names in a declaration that refer to the symbol and are written.</summary>
    private static IEnumerable<SimpleNameSyntax> Writes(SyntaxNode declaration, ISymbol symbol, SemanticModel model) =>
        declaration.DescendantNodes()
            .OfType<SimpleNameSyntax>()
            .Where(n => n.Identifier.ValueText == symbol.Name
                        && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(n).Symbol, symbol)
                        && FieldPropertyRefactoring.IsWrite(n));

    /// <summary>
    /// Whether a statement uses the object other than to read or write its fields: it calls
    /// a member on <c>this</c>, passes <c>this</c> on, or captures it.
    /// </summary>
    private static bool UsesObject(StatementSyntax statement, SemanticModel model) =>
        model.GetOperation(statement) is { } operation &&
        operation.DescendantsAndSelf().Any(o =>
            o is IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance }
            && o.Parent is not IFieldReferenceOperation);

    /// <summary>
    /// Refuses unless every construction that runs the constructor, whether <c>new</c>, a
    /// <c>this(...)</c> or <c>base(...)</c> call, passes for the parameter a value equivalent
    /// to each expression being replaced.
    /// </summary>
    private static async Task EnsureEveryConstructionPassesAsync(
        Solution solution,
        IMethodSymbol constructor,
        IParameterSymbol parameter,
        IReadOnlyList<IOperation> values,
        string described,
        CancellationToken cancellationToken)
    {
        var references = await SymbolFinder.FindReferencesAsync(constructor, solution, cancellationToken);
        foreach (var location in references.SelectMany(r => r.Locations).Where(l => l.Location.IsInSource))
        {
            var root = (await location.Document.GetSyntaxRootAsync(cancellationToken))!;
            var model = (await location.Document.GetSemanticModelAsync(cancellationToken))!;
            var node = root.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true);
            if (node.IsPartOfStructuredTrivia())
                continue;

            var construction = node.AncestorsAndSelf()
                .FirstOrDefault(n => n is BaseObjectCreationExpressionSyntax or ConstructorInitializerSyntax or PrimaryConstructorBaseTypeSyntax);
            var arguments = construction == null ? default : ArgumentsOf(model.GetOperation(construction, cancellationToken));
            var passed = arguments.IsDefault ? null : arguments.FirstOrDefault(a => a.Parameter?.Ordinal == parameter.Ordinal);
            if (passed == null || !values.All(v => Equivalent(passed.Value, v)))
                throw new McpException(
                    $"Error: The construction at {SolutionEdits.Describe(location.Location)} passes a different value for '{parameter.Name}' than {described}");
        }
    }

    private static ImmutableArray<IArgumentOperation> ArgumentsOf(IOperation? operation) => operation switch
    {
        IObjectCreationOperation creation => creation.Arguments,
        IInvocationOperation invocation => invocation.Arguments,
        IExpressionStatementOperation statement => ArgumentsOf(statement.Operation),
        _ => default,
    };

    /// <summary>
    /// Whether two fixed values are the same: equal constants of one type, or constructions
    /// of the same constructor with equivalent arguments, or the same static field or type.
    /// The values may come from different documents, so names written differently, such as
    /// qualified and unqualified, compare equal when they mean the same symbol.
    /// </summary>
    private static bool Equivalent(IOperation first, IOperation second)
    {
        if (first.ConstantValue.HasValue || second.ConstantValue.HasValue)
            return first.ConstantValue.HasValue && second.ConstantValue.HasValue
                && Equals(first.ConstantValue.Value, second.ConstantValue.Value)
                && SymbolEqualityComparer.Default.Equals(first.Type, second.Type);

        first = WithoutImplicitConversion(first);
        second = WithoutImplicitConversion(second);
        return (first, second) switch
        {
            (IObjectCreationOperation a, IObjectCreationOperation b) =>
                a.Initializer == null && b.Initializer == null
                && SymbolEqualityComparer.Default.Equals(a.Constructor, b.Constructor)
                && a.Arguments.Length == b.Arguments.Length
                && a.Arguments.OrderBy(x => x.Parameter?.Ordinal).Zip(b.Arguments.OrderBy(x => x.Parameter?.Ordinal))
                    .All(pair => pair.First.Parameter?.Ordinal == pair.Second.Parameter?.Ordinal && Equivalent(pair.First.Value, pair.Second.Value)),
            (IFieldReferenceOperation a, IFieldReferenceOperation b) =>
                a.Instance == null && b.Instance == null && a.Field.IsReadOnly && SymbolEqualityComparer.Default.Equals(a.Field, b.Field),
            (ITypeOfOperation a, ITypeOfOperation b) => SymbolEqualityComparer.Default.Equals(a.TypeOperand, b.TypeOperand),
            (IConversionOperation a, IConversionOperation b) =>
                a.OperatorMethod == null && b.OperatorMethod == null
                && SymbolEqualityComparer.Default.Equals(a.Type, b.Type)
                && Equivalent(a.Operand, b.Operand),
            _ => false,
        };
    }

    private static IOperation WithoutImplicitConversion(IOperation operation) =>
        operation is IConversionOperation { IsImplicit: true, OperatorMethod: null, Conversion.IsIdentity: true } conversion
            ? WithoutImplicitConversion(conversion.Operand)
            : operation;
}
