using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Operations;

[McpServerToolType]
public static class RedirectCallsWithConstantArgumentTool
{
    [McpServerTool, Description("Make calls that pass a constant for a parameter call the method the original runs for that value directly, without the parameter")]
    public static async Task<string> RedirectCallsWithConstantArgument(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file declaring the method whose calls are redirected")] string filePath,
        [Description("Name of the method whose calls are redirected")] string methodName,
        [Description("The parameter the calls pass the constant for")] string parameterName,
        [Description("The constant as a C# expression, such as \"height\" or Zone.Europe")] string value,
        [Description("Name of the method the original calls for that value, which the calls will call instead")] string targetMethodName,
        [Description("A line of the method's declaration (1-based), to choose between overloads")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var method = (await SolutionEdits.FindMethodAsync(solution, filePath, methodName, line, cancellationToken)).OriginalDefinition;
            var declaration = (await method.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken)) as MethodDeclarationSyntax;
            if (method.MethodKind != MethodKind.Ordinary || declaration?.Body == null)
                throw new McpException($"Error: '{methodName}' is not an ordinary method with a block body");
            if (method.IsVirtual || method.IsAbstract || method.IsOverride || method.ExplicitInterfaceImplementations.Length > 0
                || method.ContainingType.AllInterfaces.SelectMany(i => i.GetMembers().OfType<IMethodSymbol>())
                    .Any(m => SymbolEqualityComparer.Default.Equals(method.ContainingType.FindImplementationForInterfaceMember(m), method)))
                throw new McpException($"Error: '{methodName}' is virtual, an override or an interface implementation, so a call may run other code");
            if (method.IsGenericMethod)
                throw new McpException($"Error: '{methodName}' is generic, and calls pass type arguments that '{targetMethodName}' does not take");

            var parameter = SolutionEdits.FindParameter(method, parameterName);
            var model = (await solution.GetDocument(declaration.SyntaxTree)!.GetSemanticModelAsync(cancellationToken))!;
            var constant = ConstantValue(model, declaration.Body, parameter, value);
            if (!MethodsNamed(method.ContainingType, targetMethodName).Any())
                throw new McpException($"Error: '{method.ContainingType.Name}' has no method named '{targetMethodName}'");

            var call = CallMadeFor(declaration, method, parameter, constant, targetMethodName, model)
                ?? throw new McpException(
                    $"Error: When '{parameterName}' is {value}, '{methodName}' does more than call '{targetMethodName}' with its parameters and return what it returns");

            var (changed, redirected) = await RedirectAsync(solution, method, parameter.Ordinal, constant, call, cancellationToken);
            await SolutionEdits.EnsureCompilesAsync(solution, changed, cancellationToken);
            await SolutionEdits.WriteAsync(solution, changed, cancellationToken);
            return $"Successfully redirected {redirected} call(s) of '{methodName}' passing {value} to '{targetMethodName}'";
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error redirecting calls: {ex.Message}", ex);
        }
    }

    /// <summary>What the method does for the value: call <see cref="Callee"/>, passing these of its parameters.</summary>
    private sealed record Call(IMethodSymbol Callee, IReadOnlyList<int> Ordinals);

    private static IEnumerable<IMethodSymbol> MethodsNamed(INamedTypeSymbol type, string name)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            foreach (var method in current.GetMembers(name).OfType<IMethodSymbol>())
                yield return method;
        }
    }

    /// <summary>The value converted to the parameter's type, bound where the method's body starts.</summary>
    private static object? ConstantValue(SemanticModel model, BlockSyntax body, IParameterSymbol parameter, string value)
    {
        var type = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var statement = SyntaxFactory.ParseStatement($"{type} __value = {value};");
        var position = body.Statements.FirstOrDefault()?.SpanStart ?? body.CloseBraceToken.SpanStart;
        if (statement is LocalDeclarationStatementSyntax { Declaration.Variables: [{ Initializer: { } initializer }] }
            && !statement.ContainsDiagnostics
            && model.TryGetSpeculativeSemanticModel(position, statement, out var speculative)
            && speculative.GetOperation(initializer) is IVariableInitializerOperation { Value.ConstantValue.HasValue: true } bound
            && bound.Value is not IConversionOperation { Conversion.Exists: false })
            return bound.Value.ConstantValue.Value;

        throw new McpException($"Error: {value} is not a constant of type '{parameter.Type.ToDisplayString()}'");
    }

    /// <summary>
    /// The call the method makes when the parameter holds the value, or null when it does
    /// anything more: the statements it runs must be <c>return M(...);</c>, or for a void
    /// method <c>M(...); return;</c> or <c>M(...);</c> as its last statement.
    /// </summary>
    private static Call? CallMadeFor(
        MethodDeclarationSyntax declaration,
        IMethodSymbol method,
        IParameterSymbol parameter,
        object? value,
        string name,
        SemanticModel model)
    {
        var path = Path(declaration.Body!.Statements.ToList(), parameter, value, model);
        return path switch
        {
            [ReturnStatementSyntax { Expression: InvocationExpressionSyntax call }, ..] when !method.ReturnsVoid => Passed(call, method, name, model),
            [ExpressionStatementSyntax { Expression: InvocationExpressionSyntax call }, ReturnStatementSyntax { Expression: null }, ..] when method.ReturnsVoid => Passed(call, method, name, model),
            [ExpressionStatementSyntax { Expression: InvocationExpressionSyntax call }] when method.ReturnsVoid => Passed(call, method, name, model),
            _ => null,
        };
    }

    /// <summary>
    /// The statements that run, in order, when the parameter holds the value. An if
    /// statement comparing the parameter with a constant, or a switch on it, is followed
    /// into the branch the value takes, and the statements after it follow that branch.
    /// Any other statement ends the walk, with the statements from it on as they are.
    /// </summary>
    private static List<StatementSyntax> Path(List<StatementSyntax> statements, IParameterSymbol parameter, object? value, SemanticModel model)
    {
        if (statements.Count == 0)
            return statements;

        var rest = statements.Skip(1);
        switch (statements[0])
        {
            case BlockSyntax block:
                return Path(block.Statements.Concat(rest).ToList(), parameter, value, model);

            case IfStatementSyntax @if when Matches(@if.Condition, parameter, value, model) is { } matches:
                var taken = matches ? @if.Statement : @if.Else?.Statement;
                return Path((taken == null ? rest : rest.Prepend(taken)).ToList(), parameter, value, model);

            case SwitchStatementSyntax @switch when IsParameter(model.GetOperation(@switch.Expression), parameter)
                                                    && SectionFor(@switch, value, model) is (true, var section):
                if (section == null)
                    return Path(rest.ToList(), parameter, value, model);

                // A break at the end of the section leaves the switch for the statements after it.
                var body = section.Statements.ToList();
                if (body.LastOrDefault() is BreakStatementSyntax)
                    body.RemoveAt(body.Count - 1);
                return Path(body.Concat(rest).ToList(), parameter, value, model);

            default:
                return statements;
        }
    }

    /// <summary>
    /// Whether a condition comparing the parameter with a constant, using built-in
    /// equality, holds for the value; null when the condition is anything else.
    /// </summary>
    private static bool? Matches(ExpressionSyntax condition, IParameterSymbol parameter, object? value, SemanticModel model)
    {
        if (model.GetOperation(condition) is not IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals } comparison
            || comparison.OperatorMethod is { ContainingType.SpecialType: not SpecialType.System_String })
            return null;

        var constant = IsParameter(comparison.LeftOperand, parameter) ? comparison.RightOperand
            : IsParameter(comparison.RightOperand, parameter) ? comparison.LeftOperand
            : null;
        if (constant is not { ConstantValue.HasValue: true })
            return null;

        var equal = Equals(constant.ConstantValue.Value, value);
        return comparison.OperatorKind == BinaryOperatorKind.Equals ? equal : !equal;
    }

    /// <summary>
    /// The section a switch runs for the value, or null when none does. Not decided
    /// when a label is anything but a constant of the parameter's type or <c>default</c>.
    /// </summary>
    private static (bool Decided, SwitchSectionSyntax? Section) SectionFor(SwitchStatementSyntax @switch, object? value, SemanticModel model)
    {
        SwitchSectionSyntax? fallback = null;
        foreach (var section in @switch.Sections)
        {
            foreach (var label in section.Labels)
            {
                if (label is DefaultSwitchLabelSyntax)
                {
                    fallback = section;
                    continue;
                }

                if (label is not CaseSwitchLabelSyntax { Value: var constant }
                    || model.GetConstantValue(constant) is not { HasValue: true } labelValue
                    || model.GetTypeInfo(constant) is not { Type: { } type, ConvertedType: { } converted }
                    || !SymbolEqualityComparer.Default.Equals(type, converted))
                    return (false, null);

                if (Equals(labelValue.Value, value))
                    return (true, section);
            }
        }

        return (true, fallback);
    }

    private static bool IsParameter(IOperation? operation, IParameterSymbol parameter) =>
        operation is IParameterReferenceOperation reference &&
        reference.Parameter.Ordinal == parameter.Ordinal &&
        SymbolEqualityComparer.Default.Equals(reference.Parameter.ContainingSymbol.OriginalDefinition, parameter.ContainingSymbol.OriginalDefinition);

    /// <summary>
    /// The call as a <see cref="Call"/> when it calls the named method on the same object,
    /// passing each of the method's parameters it passes unchanged, of the same type, so a
    /// caller can pass its arguments straight to it; otherwise null.
    /// </summary>
    private static Call? Passed(InvocationExpressionSyntax call, IMethodSymbol method, string name, SemanticModel model)
    {
        if (call.Expression is not (IdentifierNameSyntax or MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax })
            || model.GetSymbolInfo(call).Symbol is not IMethodSymbol { MethodKind: MethodKind.Ordinary, IsGenericMethod: false } callee
            || callee.Name != name
            || callee.IsStatic != method.IsStatic
            || (!method.ReturnsVoid && !SymbolEqualityComparer.Default.Equals(callee.ReturnType, method.ReturnType))
            || callee.Parameters.Length != call.ArgumentList.Arguments.Count)
            return null;

        var ordinals = new List<int>();
        for (var i = 0; i < callee.Parameters.Length; i++)
        {
            var argument = call.ArgumentList.Arguments[i];
            if (argument.NameColon != null
                || !argument.RefKindKeyword.IsKind(SyntaxKind.None)
                || callee.Parameters[i].RefKind != RefKind.None
                || callee.Parameters[i].IsParams
                || model.GetSymbolInfo(argument.Expression).Symbol is not IParameterSymbol passed
                || !SymbolEqualityComparer.Default.Equals(passed.ContainingSymbol, method)
                || passed.RefKind != RefKind.None
                || !SymbolEqualityComparer.Default.Equals(passed.Type, callee.Parameters[i].Type))
                return null;
            ordinals.Add(passed.Ordinal);
        }

        return new Call(callee, ordinals);
    }

    /// <summary>
    /// Points each call passing the value for the parameter at the named method, passing
    /// the call's arguments for the parameters the method passes on. A call is left alone
    /// when that would change what it evaluates: an argument it would drop, repeat or
    /// reorder has side effects, or an argument was not written out.
    /// </summary>
    private static async Task<(Solution Changed, int Redirected)> RedirectAsync(
        Solution solution,
        IMethodSymbol method,
        int ordinal,
        object? value,
        Call call,
        CancellationToken cancellationToken)
    {
        var edits = new Dictionary<DocumentId, Dictionary<InvocationExpressionSyntax, Func<InvocationExpressionSyntax, InvocationExpressionSyntax>>>();
        var redirected = 0;
        var locations = (await SymbolFinder.FindReferencesAsync(method, solution, cancellationToken))
            .SelectMany(r => r.Locations)
            .Where(l => l.Location.IsInSource);
        foreach (var location in locations)
        {
            var root = (await location.Document.GetSyntaxRootAsync(cancellationToken))!;
            var model = (await location.Document.GetSemanticModelAsync(cancellationToken))!;
            var invocation = root.FindNode(location.Location.SourceSpan).AncestorsAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault();
            if (invocation == null
                || model.GetOperation(invocation, cancellationToken) is not IInvocationOperation operation
                || !SymbolEqualityComparer.Default.Equals(operation.TargetMethod.OriginalDefinition, method))
                continue;

            var rewrite = Redirection(invocation, operation, ordinal, value, call);
            if (rewrite == null || !BindsTo(rewrite(invocation), invocation, call.Callee, model))
                continue;

            if (!edits.TryGetValue(location.Document.Id, out var calls))
                edits[location.Document.Id] = calls = new();
            calls[invocation] = rewrite;
            redirected++;
        }

        var changed = solution;
        foreach (var (documentId, calls) in edits)
        {
            var root = (await changed.GetDocument(documentId)!.GetSyntaxRootAsync(cancellationToken))!;
            changed = changed.WithDocumentSyntaxRoot(documentId, root.ReplaceNodes(calls.Keys, (original, current) => calls[original](current)));
        }

        return (changed, redirected);
    }

    /// <summary>How to rewrite the call as a call of the named method, or null when it must be left alone.</summary>
    private static Func<InvocationExpressionSyntax, InvocationExpressionSyntax>? Redirection(
        InvocationExpressionSyntax invocation,
        IInvocationOperation operation,
        int ordinal,
        object? value,
        Call call)
    {
        var arguments = invocation.ArgumentList.Arguments;
        if (operation.Arguments.Any(a => a.ArgumentKind != ArgumentKind.Explicit)
            || arguments.Any(a => !a.RefKindKeyword.IsKind(SyntaxKind.None)))
            return null;

        var constant = operation.Arguments.First(a => a.Parameter!.Ordinal == ordinal).Value.ConstantValue;
        if (!constant.HasValue || !Equals(constant.Value, value))
            return null;

        var valueAt = operation.Arguments.ToDictionary(a => arguments.IndexOf((ArgumentSyntax)a.Syntax), a => a.Value);
        var kept = call.Ordinals
            .Select(o => arguments.IndexOf((ArgumentSyntax)operation.Arguments.First(a => a.Parameter!.Ordinal == o).Syntax))
            .ToList();
        var inOrder = kept.Zip(kept.Skip(1)).All(pair => pair.First < pair.Second);
        var mustBePure = inOrder ? Enumerable.Range(0, arguments.Count).Except(kept) : Enumerable.Range(0, arguments.Count);
        if (mustBePure.Any(i => !IsPure(valueAt[i])))
            return null;

        var names = call.Callee.Parameters.Select(p => p.Name).ToList();
        return current =>
        {
            var list = current.ArgumentList.Arguments;
            if (inOrder)
            {
                foreach (var dropped in Enumerable.Range(0, list.Count).Except(kept).OrderByDescending(i => i))
                    list = list.RemoveAt(dropped);
            }
            else
            {
                var reordered = kept.Select(i => list[i].WithoutTrivia()).ToList();
                list = SyntaxFactory.SeparatedList(
                    reordered,
                    Enumerable.Repeat(SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space), Math.Max(0, reordered.Count - 1)));
            }

            list = SyntaxFactory.SeparatedList(
                list.Select((argument, i) => argument.NameColon == null
                    ? argument
                    : argument.WithNameColon(argument.NameColon.WithName(SyntaxFactory.IdentifierName(names[i]).WithTriviaFrom(argument.NameColon.Name)))),
                list.GetSeparators());
            return current
                .WithExpression(Renamed(current.Expression, call.Callee.Name))
                .WithArgumentList(current.ArgumentList.WithArguments(list));
        };
    }

    /// <summary>A constant, a local, a parameter, <c>this</c> or a field of this object: nothing evaluating it could change.</summary>
    private static bool IsPure(IOperation value)
    {
        while (value is IConversionOperation { OperatorMethod: null, IsImplicit: true } conversion)
            value = conversion.Operand;

        return value.ConstantValue.HasValue || value switch
        {
            ILocalReferenceOperation or IParameterReferenceOperation or IInstanceReferenceOperation => true,
            IFieldReferenceOperation field => field.Instance is null or IInstanceReferenceOperation,
            _ => false,
        };
    }

    private static ExpressionSyntax Renamed(ExpressionSyntax callee, string name)
    {
        SimpleNameSyntax Rename(SimpleNameSyntax simple) => SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(name)).WithTriviaFrom(simple);
        return callee switch
        {
            SimpleNameSyntax simple => Rename(simple),
            MemberAccessExpressionSyntax access => access.WithName(Rename(access.Name)),
            MemberBindingExpressionSyntax binding => binding.WithName(Rename(binding.Name)),
            _ => callee,
        };
    }

    /// <summary>
    /// Whether the rewritten call binds to the named method where the call is, rather than
    /// to another method of that name, such as one a derived class hides it with. A call
    /// in a conditional access cannot be bound on its own, so it is left to the compile check.
    /// </summary>
    private static bool BindsTo(InvocationExpressionSyntax rewritten, InvocationExpressionSyntax original, IMethodSymbol callee, SemanticModel model)
    {
        if (rewritten.Expression is MemberBindingExpressionSyntax)
            return true;

        var bound = model.GetSpeculativeSymbolInfo(original.SpanStart, rewritten, SpeculativeBindingOption.BindAsExpression);
        return bound.Symbol is not IMethodSymbol symbol
            || SymbolEqualityComparer.Default.Equals(symbol.OriginalDefinition, callee.OriginalDefinition);
    }
}
