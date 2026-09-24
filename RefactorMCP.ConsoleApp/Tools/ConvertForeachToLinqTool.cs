using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Threading;

[McpServerToolType]
public static class ConvertForeachToLinqTool
{
    [McpServerTool, Description("Convert a foreach loop that filters and projects into a list, sums, counts or looks for a match into a LINQ query in method syntax")]
    public static async Task<string> ConvertForeachToLinq(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line of the foreach loop (1-based)")] int line,
        [Description("Column on that line inside the loop (1-based)")] int column,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var target = await CaretTarget.FindAsync(solutionPath, filePath, line, column, cancellationToken);
            var loop = target.Enclosing<CommonForEachStatementSyntax>()
                ?? throw new McpException($"Error: {line}:{column} is not in a foreach loop");
            var model = target.Model;

            if (loop is not ForEachStatementSyntax forEach || loop.AwaitKeyword != default)
                throw new McpException("Error: The loop deconstructs or awaits its elements, so it is not a filter, projection or aggregation a query can express");
            if (!IsGenericSequence(model.GetTypeInfo(forEach.Expression, cancellationToken).Type))
                throw new McpException($"Error: '{forEach.Expression}' is not a generic sequence, which a LINQ query needs");
            if (!model.GetForEachStatementInfo(forEach).ElementConversion.IsIdentity)
                throw new McpException("Error: The loop converts each element to its declared type, so it is not a filter, projection or aggregation a query can express as written");

            var pipeline = new Pipeline(model, forEach);
            pipeline.Read(Statements(forEach.Statement));
            pipeline.Check();

            var newRoot = (CompilationUnitSyntax)Replace(target.Root, forEach, pipeline);
            newRoot = TypeRefactoringHelpers.AddUsings(newRoot, new[] { "System.Linq" });

            await target.ApplyAsync(newRoot, cancellationToken);
            return $"Successfully converted the foreach loop over '{forEach.Expression}' to a LINQ query in {filePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error converting foreach to LINQ: {ex.Message}", ex);
        }
    }

    private static IReadOnlyList<StatementSyntax> Statements(StatementSyntax statement) =>
        statement is BlockSyntax block ? block.Statements : new[] { statement };

    private static bool IsGenericSequence(ITypeSymbol? type) =>
        type is IArrayTypeSymbol
        || (type is not null && type.AllInterfaces.Append(type).Any(i =>
            i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T));

    /// <summary>
    /// Replaces the loop with the query. When the statement before the loop
    /// declares the accumulator with its starting value (an empty list, zero or
    /// false), the query becomes that declaration's initializer; otherwise the
    /// query is added to the accumulator.
    /// </summary>
    private static SyntaxNode Replace(SyntaxNode root, ForEachStatementSyntax loop, Pipeline pipeline)
    {
        var block = loop.Parent as BlockSyntax;
        var index = block?.Statements.IndexOf(loop) ?? -1;
        var previous = index > 0 ? block!.Statements[index - 1] : null;
        var next = block is not null && index + 1 < block.Statements.Count ? block.Statements[index + 1] : null;
        var query = pipeline.Query();

        if (pipeline.Terminal is ReturnMatch)
        {
            if (next is not ReturnStatementSyntax { Expression: LiteralExpressionSyntax { RawKind: (int)SyntaxKind.FalseLiteralExpression } } returnFalse)
                throw new McpException("Error: The loop returns true on a match but is not followed by return false, so it is not a filter, projection or aggregation a query can express");

            var returned = SyntaxFactory.ParseStatement($"return {query};")
                .WithLeadingTrivia(loop.GetLeadingTrivia())
                .WithTrailingTrivia(returnFalse.GetTrailingTrivia());
            return root.ReplaceNode(block!, block!.WithStatements(block.Statements.RemoveAt(index + 1).RemoveAt(index).Insert(index, returned)));
        }

        if (previous is LocalDeclarationStatementSyntax { Declaration.Variables: [{ Initializer: { } initializer } declarator] } declaration
            && SymbolEqualityComparer.Default.Equals(pipeline.Model.GetDeclaredSymbol(declarator), pipeline.Accumulator)
            && pipeline.StartsEmpty(initializer.Value))
        {
            // Comments above the loop join those above the declaration.
            var loopComments = loop.GetLeadingTrivia().SkipWhile(t => t.IsKind(SyntaxKind.WhitespaceTrivia) || t.IsKind(SyntaxKind.EndOfLineTrivia));
            var value = pipeline.Terminal is Collect ? query + ".ToList()" : query;
            var replaced = declaration
                .ReplaceNode(initializer.Value, SyntaxFactory.ParseExpression(value).WithTriviaFrom(initializer.Value))
                .WithLeadingTrivia(declaration.GetLeadingTrivia().AddRange(loopComments));
            return root.ReplaceNode(block!, block!.WithStatements(block.Statements.RemoveAt(index).RemoveAt(index - 1).Insert(index - 1, replaced)));
        }

        var statement = pipeline.Terminal switch
        {
            Collect collect => $"{collect.List.WithoutTrivia()}.AddRange({query});",
            Total or Counter => $"{pipeline.Accumulator!.Name} += {query};",
            _ => throw new McpException($"Error: '{pipeline.Accumulator!.Name}' is not declared false just before the loop, so the loop is not a filter, projection or aggregation a query can replace"),
        };
        return root.ReplaceNode(loop, SyntaxFactory.ParseStatement(statement).WithTriviaFrom(loop));
    }

    private abstract record Accumulation;

    /// <summary><c>list.Add(value)</c></summary>
    private sealed record Collect(ExpressionSyntax List, ExpressionSyntax Value) : Accumulation;

    /// <summary><c>total += value</c></summary>
    private sealed record Total(ExpressionSyntax Value) : Accumulation;

    /// <summary><c>count++</c></summary>
    private sealed record Counter : Accumulation;

    /// <summary><c>found = true; break;</c></summary>
    private sealed record Flag : Accumulation;

    /// <summary><c>return true;</c></summary>
    private sealed record ReturnMatch : Accumulation;

    /// <summary>A Where or Select, with the name of its lambda's parameter.</summary>
    private sealed record Step(string Method, string Parameter, ExpressionSyntax Body);

    /// <summary>
    /// The loop body read as a query: each <c>if</c> without an else is a Where,
    /// each local declared from the current element is a Select, and the innermost
    /// statement is the accumulation.
    /// </summary>
    private sealed class Pipeline
    {
        private readonly ForEachStatementSyntax _loop;
        private readonly List<Step> _steps = new();
        private string _current;
        private ISymbol _currentSymbol;

        public Pipeline(SemanticModel model, ForEachStatementSyntax loop)
        {
            Model = model;
            _loop = loop;
            _current = loop.Identifier.ValueText;
            _currentSymbol = model.GetDeclaredSymbol(loop)!;
        }

        public SemanticModel Model { get; }

        public Accumulation? Terminal { get; private set; }

        /// <summary>The list, total, counter or flag the loop accumulates into.</summary>
        public ISymbol? Accumulator { get; private set; }

        public void Read(IReadOnlyList<StatementSyntax> statements)
        {
            if (statements.Count > 1
                && statements[0] is LocalDeclarationStatementSyntax { Declaration.Variables: [{ Initializer: { } initializer } declarator] } local
                && local.UsingKeyword == default && !local.IsConst && local.Declaration.Type is not RefTypeSyntax)
            {
                var rest = statements.Skip(1).ToList();
                if (rest.Any(s => Mentions(s, _currentSymbol)))
                    throw new McpException($"Error: The loop uses '{_current}' after declaring '{declarator.Identifier}', so it is not a filter, projection or aggregation a query can express");

                var declared = (ILocalSymbol)Model.GetDeclaredSymbol(declarator)!;
                if (!SymbolEqualityComparer.Default.Equals(declared.Type, Model.GetTypeInfo(initializer.Value).Type))
                    throw new McpException($"Error: '{declarator.Identifier}' converts its value, so the loop is not a filter, projection or aggregation a query can express as written");

                _steps.Add(new Step("Select", _current, initializer.Value));
                _current = declarator.Identifier.ValueText;
                _currentSymbol = declared;
                Read(rest);
                return;
            }

            if (statements is [IfStatementSyntax { Else: null } condition])
            {
                _steps.Add(new Step("Where", _current, condition.Condition));
                Read(Statements(condition.Statement));
                return;
            }

            Terminal = Accumulation(statements) ?? throw Unsupported(statements);
        }

        private Accumulation? Accumulation(IReadOnlyList<StatementSyntax> statements)
        {
            switch (statements)
            {
                case [ExpressionStatementSyntax
                {
                    Expression: InvocationExpressionSyntax
                    {
                        Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Add" } access,
                        ArgumentList.Arguments: [{ RefKindKeyword.RawKind: (int)SyntaxKind.None } argument],
                    } invocation,
                }]
                    when Model.GetSymbolInfo(invocation).Symbol is IMethodSymbol { ContainingType: var list }
                         && list.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>"
                         && ExpressionFacts.IsSimple(access.Expression)
                         && SetAccumulator(access.Expression):
                    var element = ((INamedTypeSymbol)list).TypeArguments[0];
                    if (!SymbolEqualityComparer.Default.Equals(element, Model.GetTypeInfo(argument.Expression).Type))
                        throw new McpException($"Error: The loop adds values that are not {element.ToDisplayString()} to a list of them, so it is not a filter, projection or aggregation a query can express as written");
                    return new Collect(access.Expression, argument.Expression);

                case [ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.AddAssignmentExpression } add }]
                    when SetAccumulator(add.Left):
                    var type = Model.GetTypeInfo(add.Left).Type;
                    if (type?.SpecialType == SpecialType.System_Int32 && Model.GetConstantValue(add.Right).Value is 1)
                        return new Counter();
                    if (type?.SpecialType is SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_Single
                            or SpecialType.System_Double or SpecialType.System_Decimal
                        && SymbolEqualityComparer.Default.Equals(type, Model.GetTypeInfo(add.Right).Type))
                    {
                        return new Total(add.Right);
                    }

                    return null;

                case [ExpressionStatementSyntax { Expression: PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PostIncrementExpression, Operand: var operand } }]
                    when SetAccumulator(operand) && Model.GetTypeInfo(operand).Type?.SpecialType == SpecialType.System_Int32:
                    return new Counter();

                case [ExpressionStatementSyntax { Expression: PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PreIncrementExpression, Operand: var operand } }]
                    when SetAccumulator(operand) && Model.GetTypeInfo(operand).Type?.SpecialType == SpecialType.System_Int32:
                    return new Counter();

                case [ExpressionStatementSyntax
                {
                    Expression: AssignmentExpressionSyntax
                    {
                        RawKind: (int)SyntaxKind.SimpleAssignmentExpression,
                        Right: LiteralExpressionSyntax { RawKind: (int)SyntaxKind.TrueLiteralExpression },
                    } flag,
                }, BreakStatementSyntax]
                    when SetAccumulator(flag.Left) && Accumulator is ILocalSymbol:
                    return new Flag();

                case [ReturnStatementSyntax { Expression: LiteralExpressionSyntax { RawKind: (int)SyntaxKind.TrueLiteralExpression } }]:
                    return new ReturnMatch();

                default:
                    return null;
            }
        }

        private bool SetAccumulator(ExpressionSyntax expression)
        {
            Accumulator = Model.GetSymbolInfo(expression).Symbol;
            return Accumulator is ILocalSymbol or IFieldSymbol or IParameterSymbol;
        }

        /// <summary>Why a body that is not a recognised accumulation cannot become a query.</summary>
        private static McpException Unsupported(IReadOnlyList<StatementSyntax> statements)
        {
            var nodes = statements.SelectMany(s => s.DescendantNodesAndSelf()).ToList();
            if (nodes.FirstOrDefault(n => n is BreakStatementSyntax or ContinueStatementSyntax or ReturnStatementSyntax
                    or GotoStatementSyntax or YieldStatementSyntax or ThrowStatementSyntax) is { } exit)
            {
                return new McpException($"Error: '{exit}' leaves the loop early, which the query would not");
            }

            if (nodes.OfType<ExpressionStatementSyntax>().FirstOrDefault(e => e.Expression is not
                    (AssignmentExpressionSyntax or PostfixUnaryExpressionSyntax or PrefixUnaryExpressionSyntax)) is { } effect)
            {
                return new McpException($"Error: The loop has side effects beyond the accumulation, such as '{effect}'");
            }

            return new McpException("Error: The loop body is not a filter, projection or aggregation a query can express");
        }

        /// <summary>Refuses moved expressions that change state or read the accumulator.</summary>
        public void Check()
        {
            var moved = _steps.Select(s => (SyntaxNode)s.Body).ToList();
            switch (Terminal)
            {
                case Collect collect:
                    moved.Add(collect.Value);
                    break;
                case Total total:
                    moved.Add(total.Value);
                    break;
            }

            foreach (var expression in moved)
            {
                if (expression.DescendantNodesAndSelf().Any(n => n is AssignmentExpressionSyntax or AwaitExpressionSyntax
                        || n.Kind() is SyntaxKind.PreIncrementExpression or SyntaxKind.PreDecrementExpression
                            or SyntaxKind.PostIncrementExpression or SyntaxKind.PostDecrementExpression))
                {
                    throw new McpException($"Error: '{expression}' has side effects, which a query would run lazily");
                }

                if (Accumulator is not null && Mentions(expression, Accumulator))
                    throw new McpException($"Error: '{expression}' reads the accumulator '{Accumulator.Name}', which is not known until the query ends");
            }

            if (Accumulator is not null && Mentions(_loop.Expression, Accumulator))
                throw new McpException($"Error: The loop walks '{_loop.Expression}', which reads the accumulator '{Accumulator.Name}'");
        }

        /// <summary>Whether a declaration's initializer is the accumulation's starting value.</summary>
        public bool StartsEmpty(ExpressionSyntax value) => Terminal switch
        {
            Collect => value is BaseObjectCreationExpressionSyntax { Initializer: null } creation
                       && (creation.ArgumentList is null || creation.ArgumentList.Arguments.Count == 0),
            Total or Counter => ConstantValues.Same(Model.GetConstantValue(value).Value, 0),
            Flag => Model.GetConstantValue(value).Value is false,
            _ => false,
        };

        /// <summary>The query in method syntax, without the ToList a new list needs.</summary>
        public string Query()
        {
            var steps = _steps.ToList();
            string terminal;
            switch (Terminal)
            {
                case Collect collect:
                    AddSelect(steps, collect.Value);
                    terminal = "";
                    break;
                case Total total:
                    terminal = IsCurrent(total.Value) ? ".Sum()" : $".Sum({_current} => {total.Value.WithoutTrivia()})";
                    break;
                case Counter:
                    terminal = Folded(steps, "Count");
                    break;
                default:
                    terminal = Folded(steps, "Any");
                    break;
            }

            return Source() + string.Concat(steps.Select(s => $".{s.Method}({s.Parameter} => {s.Body.WithoutTrivia()})")) + terminal;
        }

        private void AddSelect(List<Step> steps, ExpressionSyntax value)
        {
            if (!IsCurrent(value))
                steps.Add(new Step("Select", _current, value));
        }

        private bool IsCurrent(ExpressionSyntax value) =>
            value is IdentifierNameSyntax name && name.Identifier.ValueText == _current;

        /// <summary>Count or Any, taking the condition of a final Where as its predicate.</summary>
        private static string Folded(List<Step> steps, string method)
        {
            if (steps.Count == 0 || steps[^1].Method != "Where")
                return $".{method}()";

            var last = steps[^1];
            steps.RemoveAt(steps.Count - 1);
            return $".{method}({last.Parameter} => {last.Body.WithoutTrivia()})";
        }

        private string Source()
        {
            var source = _loop.Expression.WithoutTrivia();
            return source is IdentifierNameSyntax or MemberAccessExpressionSyntax or InvocationExpressionSyntax
                or ElementAccessExpressionSyntax or ParenthesizedExpressionSyntax or ThisExpressionSyntax
                or BaseObjectCreationExpressionSyntax or ArrayCreationExpressionSyntax or ImplicitArrayCreationExpressionSyntax
                ? source.ToString()
                : $"({source})";
        }

        private bool Mentions(SyntaxNode node, ISymbol symbol) =>
            node.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Any(n =>
                n.Identifier.ValueText == symbol.Name && SymbolEqualityComparer.Default.Equals(Model.GetSymbolInfo(n).Symbol, symbol));
    }
}
