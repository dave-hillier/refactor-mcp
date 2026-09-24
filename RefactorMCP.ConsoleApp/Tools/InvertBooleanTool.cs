using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using System.Threading;

[McpServerToolType]
public static class InvertBooleanTool
{
    private static readonly SyntaxAnnotation Flippable = new("InvertBoolean.Flippable");
    private static readonly SyntaxAnnotation Declaration = new("InvertBoolean.Declaration");

    [McpServerTool, Description("Invert the meaning of a bool field, property, method or local: rename it, negate every value it is given and every use of it")]
    public static async Task<string> InvertBoolean(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line of the symbol's declaration or of a use of it (1-based)")] int line,
        [Description("Column of the symbol's name on that line (1-based)")] int column,
        [Description("New name for the inverted symbol")] string newName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var caret = await CaretDocument.FindAsync(solutionPath, filePath, line, column, cancellationToken);
            var symbol = SymbolAtCaret(caret)
                ?? throw new McpException($"Error: There is no symbol at {line}:{column}");
            Validate(caret, symbol, newName);

            var solution = caret.Document.Project.Solution;
            var edits = await CollectEditsAsync(solution, symbol, cancellationToken);

            var changed = solution;
            foreach (var (documentId, documentEdits) in edits)
                changed = await ApplyAsync(changed, documentId, documentEdits, cancellationToken);

            var renamed = await RenameAsync(changed, edits.Keys, newName, cancellationToken);
            await SolutionEdits.EnsureCompilesAsync(solution, renamed, cancellationToken);
            await SolutionEdits.WriteAsync(solution, renamed, cancellationToken);
            return $"Successfully inverted '{symbol.Name}' as '{newName}'";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error inverting boolean: {ex.Message}", ex);
        }
    }

    private static ISymbol? SymbolAtCaret(CaretDocument caret)
    {
        var token = caret.Root.FindToken(caret.Position);
        return token.Parent?.AncestorsAndSelf()
            .Take(3)
            .Select(node => caret.Model.GetDeclaredSymbol(node) ?? caret.Model.GetSymbolInfo(node).Symbol)
            .FirstOrDefault(s => s is not null);
    }

    private static void Validate(CaretDocument caret, ISymbol symbol, string newName)
    {
        var type = symbol switch
        {
            IFieldSymbol field => field.Type,
            IPropertySymbol { IsIndexer: false } property => property.Type,
            IMethodSymbol { MethodKind: MethodKind.Ordinary } method => method.ReturnType,
            ILocalSymbol localSymbol => localSymbol.Type,
            _ => throw new McpException($"Error: '{symbol.Name}' is not a field, property, method or local, so it cannot be inverted"),
        };
        if (type.SpecialType != SpecialType.System_Boolean)
            throw new McpException($"Error: '{symbol.Name}' is not a bool");

        if (symbol.IsVirtual || symbol.IsAbstract || symbol.IsOverride || symbol.ContainingType?.TypeKind == TypeKind.Interface || ImplementsInterface(symbol))
            throw new McpException($"Error: '{symbol.Name}' is virtual, abstract, an override or an interface member, so its hierarchy would have to change too");

        if (symbol is IPropertySymbol && symbol.DeclaringSyntaxReferences.Select(r => r.GetSyntax()).OfType<PropertyDeclarationSyntax>()
                .Any(p => p.AccessorList?.Accessors.Any(a => !a.IsKind(SyntaxKind.GetAccessorDeclaration) && (a.Body ?? (SyntaxNode?)a.ExpressionBody) is not null) == true))
            throw new McpException($"Error: '{symbol.Name}' has a setter with a body, which would have to store the negation of the value it is given");

        if (symbol is ILocalSymbol local)
        {
            if (local.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not VariableDeclaratorSyntax { Parent.Parent: LocalDeclarationStatementSyntax })
                throw new McpException($"Error: '{symbol.Name}' is not declared by a local declaration statement");

            var declared = local.DeclaringSyntaxReferences[0].GetSyntax();
            var member = declared.Ancestors().FirstOrDefault(a => a is MemberDeclarationSyntax) ?? caret.Root;
            if (caret.Model.LookupSymbols(declared.SpanStart, name: newName).Any()
                || member.DescendantNodes().Any(n => n is VariableDeclaratorSyntax v && v.Identifier.ValueText == newName
                    || n is SingleVariableDesignationSyntax d && d.Identifier.ValueText == newName))
                throw new McpException($"Error: The name '{newName}' is already declared where '{symbol.Name}' is");
        }
        else if (symbol.ContainingType.GetMembers(newName).Any())
        {
            throw new McpException($"Error: '{symbol.ContainingType.Name}' already has a member named '{newName}'");
        }
    }

    private static bool ImplementsInterface(ISymbol symbol) =>
        symbol.ContainingType?.AllInterfaces
            .SelectMany(i => i.GetMembers())
            .Any(m => SymbolEqualityComparer.Default.Equals(symbol.ContainingType.FindImplementationForInterfaceMember(m), symbol)) == true;

    /// <summary>
    /// For each document, the edits that negate the symbol's uses and the
    /// values it is given, located by span so they can be applied to the
    /// document once its flippable comparisons are annotated. The phase orders
    /// edits of the same node: reads before the values they are part of, and
    /// marking the declaration last.
    /// </summary>
    private static async Task<Dictionary<DocumentId, List<(TextSpanKey Span, int Phase, Func<SyntaxNode, SyntaxNode> Change)>>> CollectEditsAsync(
        Solution solution,
        ISymbol symbol,
        CancellationToken cancellationToken)
    {
        var edits = new Dictionary<DocumentId, List<(TextSpanKey, int, Func<SyntaxNode, SyntaxNode>)>>();
        void Add(Document document, SyntaxNode node, int phase, Func<SyntaxNode, SyntaxNode> change)
        {
            if (!edits.TryGetValue(document.Id, out var list))
                edits[document.Id] = list = new();
            list.Add((new TextSpanKey(node.Span, node.RawKind), phase, change));
        }

        foreach (var reference in await SymbolFinder.FindReferencesAsync(symbol, solution, cancellationToken))
        {
            foreach (var location in reference.Locations)
            {
                var root = (await location.Document.GetSyntaxRootAsync(cancellationToken))!;
                if (root.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true) is not SimpleNameSyntax name || IsNameOnly(name))
                    continue;

                AddUseEdits(symbol, name, (node, phase, change) => Add(location.Document, node, phase, change));
            }
        }

        foreach (var syntaxReference in symbol.DeclaringSyntaxReferences)
        {
            var declaration = await syntaxReference.GetSyntaxAsync(cancellationToken);
            var document = solution.GetDocument(declaration.SyntaxTree)!;
            AddDeclarationEdits(declaration, (node, phase, change) => Add(document, node, phase, change));
            Add(document, declaration, 3, node => node.WithAdditionalAnnotations(Declaration));
        }

        return edits;
    }

    private readonly record struct TextSpanKey(TextSpan Span, int Kind);

    /// <summary>Whether a reference only names the symbol, in <c>nameof</c>, where its value is not used.</summary>
    private static bool IsNameOnly(SimpleNameSyntax name) =>
        name.Ancestors().OfType<InvocationExpressionSyntax>().Any(i => i.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" });

    /// <summary>
    /// A read becomes its negation, removing a <c>!</c> already applied to it;
    /// an assignment is given the negation of its value, and a compound
    /// <c>&amp;=</c> or <c>|=</c> swaps operator by De Morgan's laws.
    /// </summary>
    private static void AddUseEdits(ISymbol symbol, SimpleNameSyntax name, Action<SyntaxNode, int, Func<SyntaxNode, SyntaxNode>> add)
    {
        var value = ValueExpression(name);
        if (symbol is IMethodSymbol && value is not InvocationExpressionSyntax)
            throw new McpException($"Error: '{symbol.Name}' is used without being called at {SolutionEdits.Describe(name.GetLocation())}, so its callers cannot be negated");

        if (value.Parent is ArgumentSyntax argument && !argument.RefKindKeyword.IsKind(SyntaxKind.None))
            throw new McpException($"Error: '{symbol.Name}' is passed by reference at {SolutionEdits.Describe(name.GetLocation())}, where what is written through the reference cannot be negated");

        if (value.Parent is AssignmentExpressionSyntax assignment && assignment.Left == value)
        {
            if (assignment.Parent is not (ExpressionStatementSyntax or InitializerExpressionSyntax))
                throw new McpException($"Error: The value of an assignment to '{symbol.Name}' is used at {SolutionEdits.Describe(assignment.GetLocation())}");

            switch (assignment.Kind())
            {
                case SyntaxKind.SimpleAssignmentExpression:
                    add(assignment.Right, 1, Negated);
                    break;
                case SyntaxKind.AndAssignmentExpression:
                case SyntaxKind.OrAssignmentExpression:
                    add(assignment, 1, node => SwapCompound((AssignmentExpressionSyntax)node));
                    break;
                case SyntaxKind.ExclusiveOrAssignmentExpression:
                    // !a ^ b is !(a ^ b), so the inverted value is updated the same way.
                    break;
                default:
                    throw new McpException($"Error: '{symbol.Name}' is assigned at {SolutionEdits.Describe(assignment.GetLocation())} in a way that cannot be negated");
            }

            return;
        }

        var outer = value;
        while (outer.Parent is ParenthesizedExpressionSyntax parenthesized)
            outer = parenthesized;

        if (outer.Parent is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } not)
            add(not, 0, node => WithoutParentheses(((PrefixUnaryExpressionSyntax)node).Operand));
        else
            add(value, 0, node => BooleanNegation.Not((ExpressionSyntax)node));
    }

    /// <summary>
    /// The values the declaration gives the symbol: an initialiser, or
    /// <c>true</c> for a field or auto-property that defaulted to false, and
    /// the values a getter or method returns.
    /// </summary>
    private static void AddDeclarationEdits(SyntaxNode declaration, Action<SyntaxNode, int, Func<SyntaxNode, SyntaxNode>> add)
    {
        switch (declaration)
        {
            case VariableDeclaratorSyntax { Initializer: { } initializer }:
                add(initializer.Value, 1, Negated);
                break;
            case VariableDeclaratorSyntax { Parent.Parent: FieldDeclarationSyntax } field:
                add(field, 1, node => ((VariableDeclaratorSyntax)node)
                    .WithInitializer(SyntaxFactory.EqualsValueClause(True()))
                    .WithAdditionalAnnotations(Formatter.Annotation));
                break;
            case PropertyDeclarationSyntax { Initializer: { } initializer }:
                add(initializer.Value, 1, Negated);
                break;
            case PropertyDeclarationSyntax { ExpressionBody: { } body }:
                add(body.Expression, 1, Negated);
                break;
            case PropertyDeclarationSyntax { AccessorList: { } accessors } property:
                if (accessors.Accessors.All(a => a.Body is null && a.ExpressionBody is null))
                {
                    add(property, 1, node => WithTrueInitializer((PropertyDeclarationSyntax)node));
                    break;
                }

                foreach (var getter in accessors.Accessors.Where(a => a.IsKind(SyntaxKind.GetAccessorDeclaration)))
                    AddReturnEdits(getter.ExpressionBody, getter.Body, add);
                break;
            case MethodDeclarationSyntax method:
                AddReturnEdits(method.ExpressionBody, method.Body, add);
                break;
        }
    }

    private static void AddReturnEdits(ArrowExpressionClauseSyntax? arrow, BlockSyntax? body, Action<SyntaxNode, int, Func<SyntaxNode, SyntaxNode>> add)
    {
        if (arrow is not null)
            add(arrow.Expression, 1, Negated);

        var returns = body?
            .DescendantNodes(n => n is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
            .OfType<ReturnStatementSyntax>() ?? Enumerable.Empty<ReturnStatementSyntax>();
        foreach (var returned in returns)
        {
            if (returned.Expression is { } expression)
                add(expression, 1, Negated);
        }
    }

    private static SyntaxNode Negated(SyntaxNode node) =>
        BooleanNegation.Negate((ExpressionSyntax)node, b => b.HasAnnotation(Flippable));

    private static SyntaxNode SwapCompound(AssignmentExpressionSyntax assignment)
    {
        var and = assignment.IsKind(SyntaxKind.OrAssignmentExpression);
        var right = BooleanNegation.Negate(assignment.Right, b => b.HasAnnotation(Flippable));
        var swapped = SyntaxFactory.AssignmentExpression(
            and ? SyntaxKind.AndAssignmentExpression : SyntaxKind.OrAssignmentExpression,
            assignment.Left,
            SyntaxFactory.Token(and ? SyntaxKind.AmpersandEqualsToken : SyntaxKind.BarEqualsToken).WithTriviaFrom(assignment.OperatorToken),
            right);
        return swapped.ReplaceNode(swapped.Right, ExpressionPlacement.Fit(swapped.Right, swapped.Right));
    }

    private static PropertyDeclarationSyntax WithTrueInitializer(PropertyDeclarationSyntax property)
    {
        var accessors = property.AccessorList!;
        return property
            .WithAccessorList(accessors.WithCloseBraceToken(accessors.CloseBraceToken.WithTrailingTrivia(SyntaxFactory.Space)))
            .WithInitializer(SyntaxFactory.EqualsValueClause(True()).WithEqualsToken(SyntaxFactory.Token(SyntaxKind.EqualsToken).WithTrailingTrivia(SyntaxFactory.Space)))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken).WithTrailingTrivia(accessors.CloseBraceToken.TrailingTrivia));
    }

    private static ExpressionSyntax True() => SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression);

    /// <summary>
    /// The expression whose value is the symbol's: the name itself, a member
    /// access or conditional access ending in it, or the call of a method.
    /// </summary>
    private static ExpressionSyntax ValueExpression(SimpleNameSyntax name)
    {
        ExpressionSyntax value = name;
        if (value.Parent is MemberAccessExpressionSyntax access && access.Name == value)
            value = access;
        else if (value.Parent is MemberBindingExpressionSyntax binding)
            value = binding;

        if (value.Parent is InvocationExpressionSyntax invocation && invocation.Expression == value)
            value = invocation;

        while (value.Parent is ConditionalAccessExpressionSyntax conditional && conditional.WhenNotNull == value)
            value = conditional;

        return value;
    }

    private static ExpressionSyntax WithoutParentheses(ExpressionSyntax expression) =>
        expression is ParenthesizedExpressionSyntax parenthesized ? WithoutParentheses(parenthesized.Expression) : expression;

    /// <summary>
    /// Applies a document's edits, innermost first, after annotating the
    /// comparisons whose operators can be flipped while the semantic model
    /// still describes the document.
    /// </summary>
    private static async Task<Solution> ApplyAsync(
        Solution solution,
        DocumentId documentId,
        List<(TextSpanKey Span, int Phase, Func<SyntaxNode, SyntaxNode> Change)> edits,
        CancellationToken cancellationToken)
    {
        var document = solution.GetDocument(documentId)!;
        var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
        var model = (await document.GetSemanticModelAsync(cancellationToken))!;
        var annotated = root.ReplaceNodes(
            root.DescendantNodes().OfType<BinaryExpressionSyntax>().Where(b => BooleanNegation.CanFlip(b, model)),
            (_, rewritten) => rewritten.WithAdditionalAnnotations(Flippable));

        // Edits of the same node run in phase order as one change, and the
        // result is fitted where the node stands once.
        var editor = new SyntaxEditor(annotated, solution.Services);
        foreach (var group in edits.GroupBy(e => e.Span).OrderBy(g => g.Key.Span.Length))
        {
            var node = annotated.FindNode(group.Key.Span, getInnermostNodeForTie: true)
                .AncestorsAndSelf()
                .First(n => n.Span == group.Key.Span && n.RawKind == group.Key.Kind);
            var changes = group.OrderBy(e => e.Phase).Select(e => e.Change).ToList();
            editor.ReplaceNode(node, (current, _) =>
            {
                var result = changes.Aggregate(current, (changed, change) => change(changed));
                return result is ExpressionSyntax expression && current is ExpressionSyntax replaced
                    ? ExpressionPlacement.Fit(expression, replaced)
                    : result;
            });
        }

        var changed = document.WithSyntaxRoot(editor.GetChangedRoot());
        changed = await Formatter.FormatAsync(changed, Formatter.Annotation, cancellationToken: cancellationToken);
        return changed.Project.Solution;
    }

    /// <summary>Finds the declaration again by its annotation and renames the symbol it declares.</summary>
    private static async Task<Solution> RenameAsync(Solution solution, IEnumerable<DocumentId> documents, string newName, CancellationToken cancellationToken)
    {
        foreach (var documentId in documents)
        {
            var document = solution.GetDocument(documentId)!;
            var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
            var declaration = root.GetAnnotatedNodes(Declaration).FirstOrDefault();
            if (declaration is null)
                continue;

            var model = (await document.GetSemanticModelAsync(cancellationToken))!;
            var symbol = model.GetDeclaredSymbol(declaration, cancellationToken)
                ?? throw new McpException("Error: The inverted declaration cannot be found again to rename it");
            return await Renamer.RenameSymbolAsync(solution, symbol, new SymbolRenameOptions(), newName, cancellationToken);
        }

        throw new McpException("Error: The inverted declaration cannot be found again to rename it");
    }
}
