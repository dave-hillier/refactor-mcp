using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Threading;

[McpServerToolType]
public static class ConvertLinqToForeachTool
{
    private static readonly HashSet<string> Terminals = new(StringComparer.Ordinal) { "ToList", "Sum", "Count", "Any" };

    [McpServerTool, Description("Convert a LINQ query of Where and Select ending in ToList, Sum, Count or Any, assigned to a local or returned, into a foreach loop")]
    public static async Task<string> ConvertLinqToForeach(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line of the query (1-based)")] int line,
        [Description("Column on that line inside the query (1-based)")] int column,
        [Description("Name of the local that holds a returned query's result (optional; defaults to result, total or count)")] string? name = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var target = await CaretTarget.FindAsync(solutionPath, filePath, line, column, cancellationToken);
            var model = target.Model;
            var statement = target.Enclosing<StatementSyntax>();
            var query = statement switch
            {
                LocalDeclarationStatementSyntax { UsingKeyword.RawKind: (int)SyntaxKind.None, Declaration.Variables: [{ Initializer.Value: InvocationExpressionSyntax value }] } => value,
                ReturnStatementSyntax { Expression: InvocationExpressionSyntax value } => value,
                _ => null,
            };

            if (query is null || !IsEnumerableCall(query, model))
            {
                if (target.Token.Parent?.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().Any(i => IsEnumerableCall(i, model)) == true)
                    throw new McpException("Error: The query is not assigned to a local or returned, so there is no statement a loop can replace");
                throw new McpException($"Error: The statement at {line}:{column} is not a LINQ query");
            }

            if (statement!.Parent is not BlockSyntax block)
                throw new McpException("Error: The query is not assigned to a local or returned in a block, so there is no room for a loop");

            var chain = Chain(query, model);
            var loop = new LoopWriter(target, statement, chain, name);
            var eol = TypeRefactoringHelpers.EndOfLine(target.Root);
            var statements = loop.Statements().Replace("\n", eol.ToString());

            var parsed = ((BlockSyntax)SyntaxFactory.ParseStatement("{" + eol + statements + eol + "}")).Statements
                .Select(s => s.WithAdditionalAnnotations(Formatter.Annotation))
                .ToList();
            parsed[0] = parsed[0].WithLeadingTrivia(statement.GetLeadingTrivia());
            parsed[^1] = parsed[^1].WithTrailingTrivia(statement.GetTrailingTrivia());
            if (parsed.Count > 1 && statement is LocalDeclarationStatementSyntax)
            {
                // A comment ending the declaration's line stays on the declaration.
                parsed[0] = parsed[0].WithTrailingTrivia(statement.GetTrailingTrivia());
                parsed[^1] = parsed[^1].WithTrailingTrivia(eol);
            }

            var index = block.Statements.IndexOf(statement);
            var newRoot = target.Root.ReplaceNode(block, block.WithStatements(block.Statements.RemoveAt(index).InsertRange(index, parsed)));
            if (chain[^1].Method.Name == "ToList")
                newRoot = TypeRefactoringHelpers.AddUsings((CompilationUnitSyntax)newRoot, new[] { "System.Collections.Generic" });

            await target.ApplyAsync(newRoot, cancellationToken);
            return $"Successfully converted the query to a foreach loop in {filePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error converting LINQ to foreach: {ex.Message}", ex);
        }
    }

    private static bool IsEnumerableCall(InvocationExpressionSyntax invocation, SemanticModel model) =>
        model.GetSymbolInfo(invocation).Symbol is IMethodSymbol method
        && (method.ReducedFrom ?? method).ContainingType.ToDisplayString() == "System.Linq.Enumerable";

    /// <summary>One call in the chain, with its lambda when it takes one.</summary>
    private sealed record Call(IMethodSymbol Method, ExpressionSyntax Source, LambdaExpressionSyntax? Lambda)
    {
        public IParameterSymbol? Parameter(SemanticModel model) =>
            Lambda is null ? null : (model.GetSymbolInfo(Lambda).Symbol as IMethodSymbol)?.Parameters.Single();

        public ExpressionSyntax Body => (ExpressionSyntax)Lambda!.Body;

        public string ParameterName => Lambda switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Parameter.Identifier.ValueText,
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ParameterList.Parameters[0].Identifier.ValueText,
            _ => throw new InvalidOperationException("The call takes no lambda"),
        };
    }

    /// <summary>
    /// The calls from the source outwards, refusing operators other than Where
    /// and Select before a final ToList, Sum, Count or Any, and lambdas other than
    /// a single parameter with an expression body.
    /// </summary>
    private static List<Call> Chain(InvocationExpressionSyntax query, SemanticModel model)
    {
        var calls = new List<Call>();
        ExpressionSyntax current = query;
        while (current is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } invocation && IsEnumerableCall(invocation, model))
        {
            var method = (IMethodSymbol)model.GetSymbolInfo(invocation).Symbol!;
            var arguments = invocation.ArgumentList.Arguments;
            var lambda = arguments.Count == 1 ? arguments[0].Expression as LambdaExpressionSyntax : null;
            var isLast = calls.Count == 0;

            var supported = isLast ? Terminals.Contains(method.Name) : method.Name is "Where" or "Select";
            if (!supported)
                throw new McpException($"Error: '{method.Name}' cannot be written as a loop by this refactoring");
            if (arguments.Count > 1 || (arguments.Count == 1 && lambda is null) || (arguments.Count == 0 && method.Name is "Where" or "Select"))
                throw new McpException($"Error: The argument to '{method.Name}' is not a lambda, so it cannot be written as a loop");
            if (lambda is not null && (lambda.ExpressionBody is null || lambda.AsyncKeyword != default
                    || lambda is ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: not 1 }))
            {
                throw new McpException($"Error: The lambda '{lambda}' has a block body or several parameters, so it cannot be written as a loop");
            }

            if (method.Name == "Sum" && method.ReturnType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                throw new McpException("Error: A Sum of nullable values skips nulls, so it cannot be written as a loop by this refactoring");

            calls.Insert(0, new Call(method, access.Expression, lambda));
            current = access.Expression;
        }

        return calls;
    }

    /// <summary>Writes the statements that replace the query: the accumulator, the loop and any return.</summary>
    private sealed class LoopWriter
    {
        private readonly CaretTarget _target;
        private readonly StatementSyntax _statement;
        private readonly List<Call> _chain;
        private readonly string? _name;
        private readonly HashSet<string> _declared = new(StringComparer.Ordinal);

        public LoopWriter(CaretTarget target, StatementSyntax statement, List<Call> chain, string? name)
        {
            _target = target;
            _statement = statement;
            _chain = chain;
            _name = name;
        }

        private SemanticModel Model => _target.Model;

        public string Statements()
        {
            var terminal = _chain[^1];
            var source = _chain[0].Source.WithoutTrivia();
            var loopVariable = _chain[0].Lambda is not null
                ? _chain[0].ParameterName
                : CaretTarget.Singular(LastName(source) ?? "") ?? "item";
            Declare(loopVariable);

            // The body from the inside out: the innermost statement, then each
            // Where around it and each Select before it.
            var current = loopVariable;
            var parts = new List<(bool IsFilter, string Text)>();
            string? value = null;
            for (var i = 0; i < _chain.Count - 1; i++)
            {
                var call = _chain[i];
                var body = Rename(call, current);
                if (call.Method.Name == "Where")
                {
                    parts.Add((true, body));
                }
                else if (_chain[i + 1].Lambda is not null)
                {
                    var nextName = _chain[i + 1].ParameterName;
                    Declare(nextName);
                    parts.Add((false, $"var {nextName} = {body};"));
                    current = nextName;
                }
                else if (terminal.Method.Name is "ToList" or "Sum")
                {
                    value = body;
                }
                else
                {
                    throw new McpException($"Error: A Select whose values are only counted by '{terminal.Method.Name}' cannot be written as a loop by this refactoring");
                }
            }

            var returned = _statement is ReturnStatementSyntax;
            var accumulator = returned ? AccumulatorName(terminal.Method.Name) : ((LocalDeclarationStatementSyntax)_statement).Declaration.Variables[0].Identifier.ValueText;
            if (terminal.Lambda is not null && terminal.Method.Name is "Count" or "Any")
                parts.Add((true, Rename(terminal, current)));
            if (terminal.Lambda is not null && terminal.Method.Name == "Sum")
                value = Rename(terminal, current);

            var innermost = terminal.Method.Name switch
            {
                "ToList" => $"{accumulator}.Add({value ?? current});",
                "Sum" => $"{accumulator} += {value ?? current};",
                "Count" => $"{accumulator}++;",
                _ when returned => "return true;",
                _ => $"{accumulator} = true;\nbreak;",
            };

            var loopBody = innermost;
            foreach (var (isFilter, text) in Enumerable.Reverse(parts))
                loopBody = isFilter ? $"if ({text})\n{{\n{loopBody}\n}}" : $"{text}\n{loopBody}";

            var result = new StringBuilder();
            if (terminal.Method.Name != "Any" || !returned)
                result.Append(Declaration(accumulator, terminal, returned)).Append('\n');
            result.Append($"foreach (var {loopVariable} in {source})\n{{\n{loopBody}\n}}");
            if (returned)
                result.Append(terminal.Method.Name == "Any" ? "\n\nreturn false;" : $"\n\nreturn {accumulator};");

            return result.ToString();
        }

        /// <summary>The accumulator's declaration, with its type as the local declared it or as the query returns it.</summary>
        private string Declaration(string accumulator, Call terminal, bool returned)
        {
            var position = _statement.SpanStart;
            var resultType = terminal.Method.ReturnType;
            if (terminal.Method.Name == "ToList")
            {
                var element = ((INamedTypeSymbol)resultType).TypeArguments[0].ToMinimalDisplayString(Model, position);
                var listType = returned ? "var" : ((LocalDeclarationStatementSyntax)_statement).Declaration.Type.ToString();
                return $"{listType} {accumulator} = new List<{element}>();";
            }

            var declared = (_statement as LocalDeclarationStatementSyntax)?.Declaration.Type;
            var type = declared is null || declared.IsVar ? resultType.ToMinimalDisplayString(Model, position) : declared.ToString();
            return $"{type} {accumulator} = {(terminal.Method.Name == "Any" ? "false" : "0")};";
        }

        private string AccumulatorName(string terminal)
        {
            if (_name is not null)
            {
                if (!CaretTarget.IsValidName(_name))
                    throw new McpException($"Error: '{_name}' is not a valid name");
                Declare(_name);
                return _name;
            }

            var stem = terminal switch { "Sum" => "total", "Count" => "count", _ => "result" };
            var name = Enumerable.Range(1, 100).Select(n => n == 1 ? stem : stem + n).First(n => !IsTaken(n));
            Declare(name);
            return name;
        }

        /// <summary>A lambda's body with its parameter renamed to the loop's current variable.</summary>
        private string Rename(Call call, string current)
        {
            var parameter = call.Parameter(Model);
            if (parameter is null || parameter.Name == current)
                return call.Body.WithoutTrivia().ToString();

            var references = call.Body.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
                .Where(n => n.Identifier.ValueText == parameter.Name
                    && SymbolEqualityComparer.Default.Equals(Model.GetSymbolInfo(n).Symbol, parameter));
            return call.Body
                .ReplaceNodes(references, (original, _) => SyntaxFactory.IdentifierName(current).WithTriviaFrom(original))
                .WithoutTrivia()
                .ToString();
        }

        private void Declare(string name)
        {
            if (IsTaken(name))
                throw new McpException($"Error: '{name}' is already declared, so the loop cannot declare it");
            _declared.Add(name);
        }

        private bool IsTaken(string name) =>
            _declared.Contains(name)
            || Model.LookupSymbols(_statement.SpanStart, name: name).Any(s => s is ILocalSymbol or IParameterSymbol or IRangeVariableSymbol);

        private static string? LastName(ExpressionSyntax expression) => expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            _ => null,
        };
    }
}
