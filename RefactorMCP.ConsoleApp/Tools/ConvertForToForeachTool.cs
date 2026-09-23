using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Threading;

[McpServerToolType]
public static class ConvertForToForeachTool
{
    /// <summary>Methods that change a collection they are called on, beyond those returning void.</summary>
    private static readonly HashSet<string> Mutators = new(StringComparer.Ordinal)
    {
        "Remove", "TryAdd", "TryRemove", "TryDequeue", "TryPop", "Pop", "Dequeue",
    };

    [McpServerTool, Description("Convert a for loop that counts an index over a collection and only reads its elements into a foreach loop")]
    public static async Task<string> ConvertForToForeach(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line of the for loop (1-based)")] int line,
        [Description("Column on that line inside the loop (1-based)")] int column,
        [Description("Name of the element variable (optional; defaults to the singular of the collection's name)")] string? name = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var target = await CaretTarget.FindAsync(solutionPath, filePath, line, column, cancellationToken);
            var loop = target.Enclosing<ForStatementSyntax>()
                ?? throw new McpException($"Error: {line}:{column} is not in a for loop");
            var model = target.Model;

            var (index, collection) = CountingLoop(loop, model)
                ?? throw new McpException("Error: The loop does not count an index up from 0 to a collection's Length or Count one at a time");
            var collectionType = model.GetTypeInfo(collection, cancellationToken).Type!;
            var collectionSymbol = model.GetSymbolInfo(collection, cancellationToken).Symbol;

            var reads = new List<ElementAccessExpressionSyntax>();
            foreach (var reference in References(loop.Statement, index, model))
            {
                if (reference.Parent is not ArgumentSyntax { Parent: BracketedArgumentListSyntax { Arguments.Count: 1, Parent: ElementAccessExpressionSyntax access } }
                    || !SameCollection(access.Expression, collection, collectionSymbol, model)
                    || reference.Ancestors().TakeWhile(a => a != loop).Any(a => a is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
                {
                    throw new McpException($"Error: The index '{index.Name}' is used other than to read an element of '{collection}'");
                }

                if (LocalVariableTarget.IsWrite(access))
                    throw new McpException($"Error: '{collection}' is modified in the loop, which a foreach does not allow");
                reads.Add(access);
            }

            if (collectionSymbol is null || Modifies(loop.Statement, collectionSymbol, model))
                throw new McpException($"Error: '{collection}' is modified in the loop, which a foreach does not allow");

            var enumerated = IndexedCollection.EnumeratedType(collectionType, model, loop.SpanStart);
            var indexed = IndexedCollection.For(collectionType, model, loop.SpanStart);
            if (enumerated is null || indexed is null || !SymbolEqualityComparer.Default.Equals(enumerated, indexed.ElementType))
                throw new McpException($"Error: '{collection}' cannot be enumerated with foreach to give the elements it indexes");

            name ??= DefaultName(target, loop, collection);
            if (!CaretTarget.IsValidName(name))
                throw new McpException($"Error: '{name}' is not a valid name");
            if (target.NameTaken(loop, name))
                throw new McpException($"Error: '{name}' is already declared or used in the loop's scope; pass another name");

            var elementType = reads.Count > 0 ? model.GetTypeInfo(reads[0], cancellationToken).Type! : indexed.ElementType;
            var type = loop.Declaration!.Type.IsVar
                ? loop.Declaration.Type
                : SyntaxFactory.ParseTypeName(elementType.ToMinimalDisplayString(model, loop.SpanStart));

            var body = loop.Statement.ReplaceNodes(
                reads,
                (original, _) => SyntaxFactory.IdentifierName(name).WithTriviaFrom(original));

            var space = SyntaxFactory.Space;
            var foreachLoop = SyntaxFactory.ForEachStatement(
                    type.WithoutTrivia().WithTrailingTrivia(space),
                    SyntaxFactory.Identifier(name).WithTrailingTrivia(space),
                    collection.WithoutTrivia(),
                    body)
                .WithForEachKeyword(SyntaxFactory.Token(loop.ForKeyword.LeadingTrivia, SyntaxKind.ForEachKeyword, loop.ForKeyword.TrailingTrivia))
                .WithOpenParenToken(loop.OpenParenToken)
                .WithInKeyword(SyntaxFactory.Token(SyntaxKind.InKeyword).WithTrailingTrivia(space))
                .WithCloseParenToken(loop.CloseParenToken);

            await target.ApplyAsync(target.Root.ReplaceNode(loop, foreachLoop), cancellationToken);
            return $"Successfully converted the for loop over '{collection}' to a foreach in {filePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error converting for to foreach: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// The index and collection of <c>for (int i = 0; i &lt; c.Length; i++)</c>, with
    /// Count for Length, and <c>++i</c> or <c>i += 1</c> for the increment.
    /// </summary>
    private static (ILocalSymbol Index, ExpressionSyntax Collection)? CountingLoop(ForStatementSyntax loop, SemanticModel model)
    {
        if (loop.Declaration is not { Variables: [{ Initializer.Value: var start } variable] } || loop.Initializers.Count > 0)
            return null;
        if (model.GetDeclaredSymbol(variable) is not ILocalSymbol { Type.SpecialType: SpecialType.System_Int32 } index)
            return null;
        if (model.GetConstantValue(start).Value is not 0)
            return null;

        if (loop.Condition is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.LessThanExpression } condition
            || !IsIndex(condition.Left, index, model)
            || condition.Right is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Length" or "Count" } bound
            || !ExpressionFacts.IsSimple(bound.Expression)
            || bound.Expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Any(n => IsIndex(n, index, model)))
        {
            return null;
        }

        var increments = loop.Incrementors.Count == 1 && loop.Incrementors[0] switch
        {
            PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PostIncrementExpression } postfix => IsIndex(postfix.Operand, index, model),
            PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PreIncrementExpression } prefix => IsIndex(prefix.Operand, index, model),
            AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.AddAssignmentExpression } add =>
                IsIndex(add.Left, index, model) && model.GetConstantValue(add.Right).Value is 1,
            _ => false,
        };
        if (!increments)
            return null;

        // The bound must be the collection's own Length or Count, not another property of that name.
        var collectionType = model.GetTypeInfo(bound.Expression).Type;
        var indexed = collectionType is null ? null : IndexedCollection.For(collectionType, model, loop.SpanStart);
        if (indexed is null || indexed.CountProperty != bound.Name.Identifier.ValueText)
            return null;

        return (index, bound.Expression);
    }

    private static bool IsIndex(ExpressionSyntax expression, ILocalSymbol index, SemanticModel model) =>
        expression is IdentifierNameSyntax name
        && name.Identifier.ValueText == index.Name
        && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(name).Symbol, index);

    private static IEnumerable<IdentifierNameSyntax> References(SyntaxNode scope, ILocalSymbol local, SemanticModel model) =>
        scope.DescendantNodes().OfType<IdentifierNameSyntax>().Where(n => IsIndex(n, local, model));

    private static bool SameCollection(ExpressionSyntax expression, ExpressionSyntax collection, ISymbol? symbol, SemanticModel model) =>
        SyntaxFactory.AreEquivalent(Unqualified(expression), Unqualified(collection))
        && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(expression).Symbol, symbol);

    private static ExpressionSyntax Unqualified(ExpressionSyntax expression) =>
        expression is MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } access ? access.Name : expression;

    /// <summary>
    /// Whether the body assigns the collection, assigns one of its members or
    /// elements, or calls a method on it that changes it.
    /// </summary>
    private static bool Modifies(SyntaxNode body, ISymbol collection, SemanticModel model)
    {
        foreach (var name in body.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (name.Identifier.ValueText != collection.Name
                || !SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(name).Symbol, collection))
            {
                continue;
            }

            ExpressionSyntax use = name.Parent is MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } qualified && qualified.Name == name
                ? qualified
                : name;
            if (LocalVariableTarget.IsWrite(use))
                return true;

            switch (use.Parent)
            {
                case ElementAccessExpressionSyntax element when element.Expression == use:
                    if (LocalVariableTarget.IsWrite(element))
                        return true;
                    break;
                case MemberAccessExpressionSyntax member when member.Expression == use:
                    if (member.Parent is InvocationExpressionSyntax invocation && invocation.Expression == member)
                    {
                        if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method
                            || method.ReturnsVoid
                            || Mutators.Contains(method.Name))
                        {
                            return true;
                        }
                    }
                    else if (LocalVariableTarget.IsWrite(member))
                    {
                        return true;
                    }

                    break;
            }
        }

        return false;
    }

    /// <summary>The singular of the collection's name, or <c>item</c>, whichever is free.</summary>
    private static string DefaultName(CaretTarget target, ForStatementSyntax loop, ExpressionSyntax collection)
    {
        var collectionName = Unqualified(collection) switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            _ => null,
        };
        var candidates = new[] { collectionName is null ? null : CaretTarget.Singular(collectionName), "item" };
        return candidates.FirstOrDefault(c => c is not null && !target.NameTaken(loop, c))
            ?? throw new McpException("Error: 'item' is already declared or used in the loop's scope; pass a name for the element");
    }
}
