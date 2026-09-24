using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using RefactorMCP.ConsoleApp.Tools.Moving;

namespace RefactorMCP.ConsoleApp.Tools.Generators;

[McpServerToolType]
public static class ReplaceConditionalWithPolymorphismTool
{
    [McpServerTool, Description("Replace a switch or if chain with polymorphism. The conditional may test a type code " +
        "property of the method's own class that each subclass overrides with a constant, or the type of one of the " +
        "method's parameters. Each case becomes an override in its subclass; the method becomes abstract, or virtual " +
        "keeping the default. A conditional on a parameter's type gains a method in the parameter's class, which the " +
        "original method delegates to.")]
    public static async Task<string> ReplaceConditionalWithPolymorphism(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the method")] string filePath,
        [Description("Name of the method whose body is the conditional")] string methodName,
        [Description("Line of the method's declaration (1-based, optional), to choose between overloads")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var document = MovingSupport.DocumentOrThrow(solution, filePath);
        var method = (IMethodSymbol)await MovingSupport.FindDeclaredSymbolAsync(
            document, methodName, line, s => s is IMethodSymbol { MethodKind: MethodKind.Ordinary }, "method", cancellationToken);

        var updated = await ConditionalPolymorphism.ReplaceAsync(solution, method, cancellationToken);
        var errors = await TypeRefactoringHelpers.NewErrorsAsync(solution, updated, cancellationToken);
        if (errors.Count > 0)
            throw new McpException($"Error: Replacing the conditional in {methodName} with polymorphism would not compile: {TypeRefactoringHelpers.Describe(errors)}");

        await MovingSupport.ApplyAsync(solution, updated, cancellationToken);
        return $"Successfully replaced the conditional in {methodName} with polymorphism";
    }
}

/// <summary>
/// Moves the cases of a conditional on a type into overrides in the
/// subclasses that each case selects.
/// </summary>
internal static class ConditionalPolymorphism
{
    /// <summary>What a case tests for: a constant code, or a type with the variable it declares.</summary>
    private sealed record Label(SyntaxNode Syntax, Optional<object?> Constant, INamedTypeSymbol? Type, ISymbol? Variable);

    /// <summary>A case's code: statements, or the expression of a switch expression arm.</summary>
    private sealed record Body(IReadOnlyList<StatementSyntax>? Statements, ExpressionSyntax? Expression)
    {
        public bool Throws => Statements is [ThrowStatementSyntax] || Expression is ThrowExpressionSyntax;

        public IEnumerable<SyntaxNode> Nodes => (IEnumerable<SyntaxNode>?)Statements ?? new[] { Expression! };
    }

    private sealed record Case(IReadOnlyList<Label> Labels, Body Body);

    private sealed record Conditional(ExpressionSyntax Governing, IReadOnlyList<Case> Cases, Body? Default);

    public static async Task<Solution> ReplaceAsync(Solution solution, IMethodSymbol method, CancellationToken cancellationToken)
    {
        var declaration = (MethodDeclarationSyntax)await method.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken);
        var document = solution.GetDocument(declaration.SyntaxTree)!;
        var model = (await document.GetSemanticModelAsync(cancellationToken))!;
        var conditional = Parse(declaration, model)
            ?? throw new McpException($"Error: {method.Name} is not a single switch or if chain on a type code or on the type of a parameter");

        var governed = model.GetSymbolInfo(conditional.Governing, cancellationToken).Symbol;
        var labels = conditional.Cases.SelectMany(c => c.Labels).ToList();
        IParameterSymbol? switched = null;
        INamedTypeSymbol baseType;
        if (governed is IParameterSymbol parameter && SymbolEqualityComparer.Default.Equals(parameter.ContainingSymbol, method)
            && labels.All(l => l.Type is not null) && parameter.Type is INamedTypeSymbol { TypeKind: TypeKind.Class } parameterType)
        {
            switched = parameter;
            baseType = parameterType.OriginalDefinition;
        }
        else if (governed is IPropertySymbol && IsOnThis(conditional.Governing) && labels.All(l => l.Constant.HasValue))
        {
            baseType = method.ContainingType;
        }
        else
        {
            throw new McpException($"Error: {method.Name} is not a single switch or if chain on a type code or on the type of a parameter");
        }

        var baseDeclaration = await HierarchyMemberHelpers.DeclarationAsync(solution, baseType, cancellationToken)
            ?? throw new McpException($"Error: {baseType.Name} is not declared in the solution");
        var subclasses = await HierarchyMemberHelpers.SubclassesAsync(solution, baseType, transitive: true, cancellationToken);
        var assigned = switched is null
            ? await AssignByCodeAsync(solution, (IPropertySymbol)governed!, conditional, subclasses, cancellationToken)
            : AssignByType(baseType, conditional, subclasses);

        var defaultBody = conditional.Default is { Throws: false } kept
            ? kept
            : conditional.Default is null && method.ReturnsVoid ? new Body(Array.Empty<StatementSyntax>(), null) : null;
        if (defaultBody is null)
        {
            if (!baseType.IsAbstract)
                throw new McpException($"Error: {baseType.Name} is not abstract, so {method.Name} cannot be abstract for the subclasses without a case");

            var uncovered = subclasses.FirstOrDefault(s => !s.Symbol.IsAbstract && !Covered(s.Symbol, baseType, assigned));
            if (uncovered is not null)
                throw new McpException($"Error: {uncovered.Symbol.Name} has no case and there is no default, so it would not implement {method.Name}");
        }

        if (switched is not null)
            RefuseCallerMembers(method, conditional, model, assigned);

        var edits = new SyntaxEdits();
        var imports = new Dictionary<DocumentId, HashSet<string>>();
        foreach (var (subclass, choice) in assigned)
        {
            var target = subclasses.First(s => SymbolEqualityComparer.Default.Equals(s.Symbol, subclass));
            var subclassModel = (await target.Document.GetSemanticModelAsync(cancellationToken))!;
            var types = subclass.BaseType is { } direct && SymbolEqualityComparer.Default.Equals(direct.OriginalDefinition, baseType)
                ? HierarchyMemberHelpers.TowardsSubclass(subclass, subclassModel, target.Declaration.SpanStart)
                : new Dictionary<ITypeParameterSymbol, TypeSyntax>(SymbolEqualityComparer.Default);
            var member = Member(declaration, model, switched, choice.Label.Variable, types, $"{Access(declaration)} override", choice.Case.Body, withConstraints: false);
            edits.Replace(target.Document.Id, target.Declaration, rewritten => MemberLayout.Append((TypeDeclarationSyntax)rewritten, member));
            Import(imports, target.Document.Id, choice.Case.Body, model);
        }

        var noTypes = new Dictionary<ITypeParameterSymbol, TypeSyntax>(SymbolEqualityComparer.Default);
        var kind = defaultBody is null ? "abstract" : "virtual";
        if (switched is null)
        {
            var modifiers = string.Join(" ", declaration.Modifiers.Where(m => !m.IsKind(SyntaxKind.VirtualKeyword)).Select(m => m.Text));
            var replacement = Member(declaration, model, null, null, noTypes, AfterAccess(modifiers, kind), defaultBody, withConstraints: true);
            edits.Replace(document.Id, declaration, rewritten => replacement
                .WithLeadingTrivia(rewritten.GetLeadingTrivia())
                .WithTrailingTrivia(rewritten.GetTrailingTrivia()));
            await WidenPrivateMembersAsync(conditional, model, baseType, edits, document.Id, cancellationToken);
        }
        else
        {
            var added = Member(declaration, model, switched, null, noTypes, $"{Access(declaration)} {kind}", defaultBody, withConstraints: true);
            edits.Replace(baseDeclaration.Document.Id, baseDeclaration.Declaration, rewritten => MemberLayout.Append((TypeDeclarationSyntax)rewritten, added));
            edits.Replace(document.Id, declaration, rewritten => Delegate((MethodDeclarationSyntax)rewritten, switched));
            if (defaultBody is not null)
                Import(imports, baseDeclaration.Document.Id, defaultBody, model);
        }

        foreach (var (id, namespaces) in imports)
        {
            var root = await solution.GetDocument(id)!.GetSyntaxRootAsync(cancellationToken);
            edits.Replace(id, root!, rewritten => TypeRefactoringHelpers.AddUsings((CompilationUnitSyntax)rewritten, namespaces));
        }

        var updated = await edits.ApplyAsync(solution, cancellationToken);
        return await MovingSupport.RemoveNewlyUnnecessaryUsingsAsync(solution, updated, cancellationToken);
    }

    private static bool IsOnThis(ExpressionSyntax governing) =>
        governing is IdentifierNameSyntax or MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax };

    private sealed record Choice(Case Case, Label Label);

    /// <summary>Each subclass whose override of the type code returns a case's constant, with that case.</summary>
    private static async Task<Dictionary<INamedTypeSymbol, Choice>> AssignByCodeAsync(
        Solution solution,
        IPropertySymbol code,
        Conditional conditional,
        IReadOnlyList<HierarchyMemberHelpers.SourceType> subclasses,
        CancellationToken cancellationToken)
    {
        var codes = new List<(INamedTypeSymbol Subclass, object? Value)>();
        foreach (var subclass in subclasses)
        {
            var property = subclass.Symbol.GetMembers(code.Name).OfType<IPropertySymbol>().FirstOrDefault(p => p.IsOverride);
            if (property?.DeclaringSyntaxReferences.FirstOrDefault() is not { } reference
                || await reference.GetSyntaxAsync(cancellationToken) is not PropertyDeclarationSyntax syntax)
                continue;

            var returned = syntax.ExpressionBody?.Expression
                ?? syntax.AccessorList?.Accessors.SingleOrDefault()?.ExpressionBody?.Expression
                ?? (syntax.AccessorList?.Accessors.SingleOrDefault()?.Body?.Statements.SingleOrDefault() as ReturnStatementSyntax)?.Expression;
            var model = await solution.GetDocument(syntax.SyntaxTree)!.GetSemanticModelAsync(cancellationToken);
            var value = returned is null ? default : model!.GetConstantValue(returned, cancellationToken);
            if (value.HasValue)
                codes.Add((subclass.Symbol, value.Value));
        }

        var assigned = new Dictionary<INamedTypeSymbol, Choice>(SymbolEqualityComparer.Default);
        foreach (var @case in conditional.Cases)
        {
            foreach (var label in @case.Labels)
            {
                var matching = codes.Where(c => Equals(c.Value, label.Constant.Value)).ToList();
                if (matching.Count == 0)
                    throw Unsupported(label, $"no subclass's {code.Name} is that value");
                foreach (var (subclass, _) in matching)
                {
                    if (!assigned.TryAdd(subclass, new Choice(@case, label)))
                        throw Unsupported(label, $"{subclass.Name} already has a case");
                }
            }
        }

        return assigned;
    }

    /// <summary>Each subclass a case's type pattern names, with that case.</summary>
    private static Dictionary<INamedTypeSymbol, Choice> AssignByType(
        INamedTypeSymbol baseType,
        Conditional conditional,
        IReadOnlyList<HierarchyMemberHelpers.SourceType> subclasses)
    {
        var assigned = new Dictionary<INamedTypeSymbol, Choice>(SymbolEqualityComparer.Default);
        foreach (var @case in conditional.Cases)
        {
            foreach (var label in @case.Labels)
            {
                var subclass = subclasses.FirstOrDefault(s => SymbolEqualityComparer.Default.Equals(s.Symbol, label.Type!.OriginalDefinition))
                    ?? throw Unsupported(label, $"{label.Type!.Name} is not a subclass of {baseType.Name} declared in the solution");
                if (!assigned.TryAdd(subclass.Symbol, new Choice(@case, label)))
                    throw Unsupported(label, $"{subclass.Symbol.Name} already has a case");
            }
        }

        return assigned;
    }

    /// <summary>Whether a subclass, or a class between it and the base, has a case.</summary>
    private static bool Covered(INamedTypeSymbol subclass, INamedTypeSymbol baseType, IReadOnlyDictionary<INamedTypeSymbol, Choice> assigned)
    {
        for (var type = subclass; type is not null && !SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, baseType); type = type.BaseType)
        {
            if (assigned.ContainsKey(type.OriginalDefinition))
                return true;
        }

        return false;
    }

    /// <summary>
    /// A case moving out of the class that declares the method cannot bring
    /// that class's members with it.
    /// </summary>
    private static void RefuseCallerMembers(
        IMethodSymbol method,
        Conditional conditional,
        SemanticModel model,
        IReadOnlyDictionary<INamedTypeSymbol, Choice> assigned)
    {
        var bodies = assigned.Values.Select(c => (Name: c.Label.Type!.Name, c.Case.Body));
        if (conditional.Default is { Throws: false } kept)
            bodies = bodies.Append(("the default", kept));

        foreach (var (name, body) in bodies)
        {
            foreach (var used in body.Nodes.SelectMany(n => n.DescendantNodesAndSelf()).OfType<SimpleNameSyntax>())
            {
                var symbol = model.GetSymbolInfo(used).Symbol;
                if (symbol is null or ITypeSymbol || symbol.ContainingSymbol is not INamedTypeSymbol owner
                    || !SymbolEqualityComparer.Default.Equals(owner.OriginalDefinition, method.ContainingType.OriginalDefinition))
                    continue;

                var qualifiedByType = used.Parent is MemberAccessExpressionSyntax access && access.Name == used
                    && model.GetSymbolInfo(access.Expression).Symbol is ITypeSymbol;
                if (!qualifiedByType)
                    throw new McpException($"Error: The case for {name} uses {symbol.Name}, a member of {method.ContainingType.Name}, which the subclass cannot reach");
            }
        }
    }

    /// <summary>Private members of the base that the moved cases use become protected, so the subclasses keep access.</summary>
    private static async Task WidenPrivateMembersAsync(
        Conditional conditional,
        SemanticModel model,
        INamedTypeSymbol baseType,
        SyntaxEdits edits,
        DocumentId document,
        CancellationToken cancellationToken)
    {
        var used = conditional.Cases
            .SelectMany(c => c.Body.Nodes)
            .SelectMany(n => n.DescendantNodesAndSelf().OfType<SimpleNameSyntax>())
            .Select(n => model.GetSymbolInfo(n, cancellationToken).Symbol)
            .Where(s => s is { DeclaredAccessibility: Accessibility.Private } and not ITypeSymbol
                && SymbolEqualityComparer.Default.Equals(s.ContainingType?.OriginalDefinition, baseType))
            .Distinct(SymbolEqualityComparer.Default);

        foreach (var symbol in used)
        {
            var syntax = await symbol!.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken);
            var member = syntax as MemberDeclarationSyntax ?? syntax.FirstAncestorOrSelf<MemberDeclarationSyntax>()!;
            edits.Replace(document, member, rewritten =>
                HierarchyMemberHelpers.WithModifiers((MemberDeclarationSyntax)rewritten, HierarchyMemberHelpers.WidenPrivate));
        }
    }

    private static void Import(Dictionary<DocumentId, HashSet<string>> imports, DocumentId document, Body body, SemanticModel model)
    {
        if (!imports.TryGetValue(document, out var namespaces))
            imports[document] = namespaces = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in body.Nodes)
            namespaces.UnionWith(TypeRefactoringHelpers.NamespacesUsedBy(node, model));
    }

    private static string Access(MethodDeclarationSyntax declaration)
    {
        var access = declaration.Modifiers.Where(m => SyntaxFacts.IsAccessibilityModifier(m.Kind())).Select(m => m.Text).ToList();
        return access.Count == 0 || access is ["private"] ? "internal" : string.Join(" ", access);
    }

    private static string AfterAccess(string modifiers, string keyword)
    {
        var parts = modifiers.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var index = parts.TakeWhile(p => p is "public" or "internal" or "protected" or "private").Count();
        parts.Insert(index, keyword);
        return string.Join(" ", parts);
    }

    /// <summary>
    /// A method with the declaration's signature, less the switched
    /// parameter, and <paramref name="body"/> rewritten to run on the
    /// subclass: a single <c>return</c> becomes an expression body.
    /// </summary>
    private static MemberDeclarationSyntax Member(
        MethodDeclarationSyntax declaration,
        SemanticModel model,
        IParameterSymbol? switched,
        ISymbol? variable,
        IReadOnlyDictionary<ITypeParameterSymbol, TypeSyntax> types,
        string modifiers,
        Body? body,
        bool withConstraints)
    {
        var parameters = declaration.ParameterList.Parameters.Where(p => switched is null || p.Identifier.ValueText != switched.Name);
        var parameterList = string.Join(", ", parameters.Select(p => Rewrite(p, model, switched, variable, types).WithoutTrivia().ToString()));
        var returnType = Rewrite(declaration.ReturnType, model, switched, variable, types).WithoutTrivia();
        var signature = $"{modifiers} {returnType} {declaration.Identifier.ValueText}{declaration.TypeParameterList}({parameterList})";
        if (withConstraints)
            signature += string.Concat(declaration.ConstraintClauses.Select(c => " " + c.WithoutTrivia()));

        var text = body switch
        {
            null => signature + ";",
            { Expression: { } expression } => $"{signature} => {Rewrite(expression, model, switched, variable, types).WithoutTrivia()};",
            { Statements: [ReturnStatementSyntax { Expression: { } returned }] } =>
                $"{signature} => {Rewrite(returned, model, switched, variable, types).WithoutTrivia()};",
            { Statements: var statements } =>
                $"{signature}\n{{\n{string.Concat(statements!.Select(s => Rewrite(s, model, switched, variable, types).ToFullString()))}}}",
        };

        return SyntaxFactory.ParseMemberDeclaration(text + "\n")!.WithAdditionalAnnotations(Formatter.Annotation);
    }

    /// <summary>
    /// Code from the conditional as the subclass reads it: the switched
    /// parameter and the case's pattern variable become the instance, and the
    /// base's type parameters become the subclass's type arguments.
    /// </summary>
    private static T Rewrite<T>(
        T node,
        SemanticModel model,
        IParameterSymbol? switched,
        ISymbol? variable,
        IReadOnlyDictionary<ITypeParameterSymbol, TypeSyntax> types)
        where T : SyntaxNode
    {
        var declaredNames = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault() is { } method
            ? method.ParameterList.Parameters.Select(p => p.Identifier.ValueText)
                .Concat(method.DescendantNodes().OfType<VariableDeclaratorSyntax>().Select(v => v.Identifier.ValueText))
                .ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        var replacements = new Dictionary<SyntaxNode, SyntaxNode>();
        foreach (var name in node.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            var symbol = model.GetSymbolInfo(name).Symbol;
            if (symbol is ITypeParameterSymbol parameter && types.TryGetValue(parameter, out var replacement))
            {
                replacements[name] = replacement.WithTriviaFrom(name);
            }
            else if (symbol is not null && (SymbolEqualityComparer.Default.Equals(symbol, switched) || SymbolEqualityComparer.Default.Equals(symbol, variable)))
            {
                if (name.Parent is MemberAccessExpressionSyntax access && access.Expression == name
                    && !declaredNames.Contains(access.Name.Identifier.ValueText))
                    replacements[access] = access.Name.WithTriviaFrom(access);
                else
                    replacements[name] = SyntaxFactory.ThisExpression().WithTriviaFrom(name);
            }
        }

        return node.ReplaceNodes(replacements.Keys, (original, _) => replacements[original]);
    }

    /// <summary>The original method now asks the switched argument to do the work.</summary>
    private static MethodDeclarationSyntax Delegate(MethodDeclarationSyntax declaration, IParameterSymbol switched)
    {
        var arguments = declaration.ParameterList.Parameters
            .Where(p => p.Identifier.ValueText != switched.Name)
            .Select(p => p.Identifier.ValueText);
        var call = SyntaxFactory.ParseExpression($"{switched.Name}.{declaration.Identifier.ValueText}{declaration.TypeParameterList}({string.Join(", ", arguments)})");
        if (declaration.ExpressionBody is { } arrow)
            return declaration.WithExpressionBody(arrow.WithExpression(call.WithTriviaFrom(arrow.Expression)));

        var isVoid = declaration.ReturnType is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.VoidKeyword };
        var statement = SyntaxFactory.ParseStatement(isVoid ? $"{call};\n" : $"return {call};\n");
        return declaration.WithBody(declaration.Body!
            .WithStatements(SyntaxFactory.SingletonList(statement))
            .WithAdditionalAnnotations(Formatter.Annotation));
    }

    private static McpException Unsupported(Label label, string reason) =>
        new($"Error: The case '{label.Syntax}' cannot be moved to one subclass: {reason}");

    private static Conditional? Parse(MethodDeclarationSyntax declaration, SemanticModel model)
    {
        var statements = declaration.Body?.Statements;
        var expression = declaration.ExpressionBody?.Expression
            ?? (statements is [ReturnStatementSyntax { Expression: { } returned }] ? returned : null);

        if (expression is SwitchExpressionSyntax switchExpression)
            return FromSwitchExpression(switchExpression, model);
        if (statements is [SwitchStatementSyntax switchStatement])
            return FromSwitchStatement(switchStatement, model);
        if (statements is [IfStatementSyntax, ..] list)
            return FromIfChain(list, model);
        return null;
    }

    private static Conditional FromSwitchExpression(SwitchExpressionSyntax switchExpression, SemanticModel model)
    {
        var cases = new List<Case>();
        Body? fallback = null;
        foreach (var arm in switchExpression.Arms)
        {
            if (arm.WhenClause is not null)
                throw Unsupported(new Label(arm.Pattern, default, null, null), "it has a when clause");

            if (arm.Pattern is DiscardPatternSyntax)
                fallback = new Body(null, arm.Expression);
            else
                cases.Add(new Case(new[] { FromPattern(arm.Pattern, model) }, new Body(null, arm.Expression)));
        }

        return new Conditional(switchExpression.GoverningExpression, cases, fallback);
    }

    private static Conditional FromSwitchStatement(SwitchStatementSyntax switchStatement, SemanticModel model)
    {
        var cases = new List<Case>();
        Body? fallback = null;
        foreach (var section in switchStatement.Sections)
        {
            var statements = section.Statements.Count == 1 && section.Statements[0] is BlockSyntax block
                ? block.Statements.ToList()
                : section.Statements.ToList();
            if (statements.LastOrDefault() is BreakStatementSyntax)
                statements.RemoveAt(statements.Count - 1);

            var labels = new List<Label>();
            foreach (var label in section.Labels)
            {
                switch (label)
                {
                    case DefaultSwitchLabelSyntax:
                        break;
                    case CasePatternSwitchLabelSyntax { WhenClause: not null } guarded:
                        throw Unsupported(new Label(guarded, default, null, null), "it has a when clause");
                    case CasePatternSwitchLabelSyntax pattern:
                        labels.Add(FromPattern(pattern.Pattern, model));
                        break;
                    case CaseSwitchLabelSyntax constant:
                        labels.Add(FromExpression(constant.Value, model));
                        break;
                }
            }

            var leavesEarly = statements.SelectMany(s => s.DescendantNodesAndSelf()).Any(n =>
                n is GotoStatementSyntax
                || (n is BreakStatementSyntax && n.Ancestors().FirstOrDefault(a => a is SwitchStatementSyntax or ForStatementSyntax
                    or ForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax) == switchStatement));
            if (leavesEarly)
                throw Unsupported(labels.FirstOrDefault() ?? new Label(section, default, null, null), "it leaves the switch part way through");

            var body = new Body(statements, null);
            if (section.Labels.Any(l => l is DefaultSwitchLabelSyntax))
            {
                if (labels.Count > 0)
                    throw Unsupported(labels[0], "it shares its code with the default");
                fallback = body;
            }
            else
            {
                cases.Add(new Case(labels, body));
            }
        }

        return new Conditional(switchStatement.Expression, cases, fallback);
    }

    /// <summary>
    /// <c>if (x == A) ... else if (x is B b) ... else ...</c>, or a run of
    /// such tests without <c>else</c> whose branches all return, followed by
    /// the default.
    /// </summary>
    private static Conditional? FromIfChain(SyntaxList<StatementSyntax> statements, SemanticModel model)
    {
        var cases = new List<Case>();
        Body? fallback = null;
        ExpressionSyntax? governing = null;
        ISymbol? governed = null;
        var consumed = 0;

        while (consumed < statements.Count && fallback is null && statements[consumed] is IfStatementSyntax chain
            && (consumed == 0 || cases.All(Returns)))
        {
            if (consumed > 0 && FromCondition(chain.Condition, model) is null)
                break;

            for (var current = (IfStatementSyntax?)chain; current is not null;)
            {
                if (FromCondition(current.Condition, model) is not var (tested, label))
                    return null;

                var symbol = model.GetSymbolInfo(tested).Symbol;
                if (symbol is null || (governed is not null && !SymbolEqualityComparer.Default.Equals(symbol, governed)))
                    return null;
                governing ??= tested;
                governed = symbol;

                cases.Add(new Case(new[] { label }, BodyOf(current.Statement)));
                var next = current.Else?.Statement;
                current = next as IfStatementSyntax;
                if (next is not null and not IfStatementSyntax)
                    fallback = BodyOf(next);
            }

            consumed++;
        }

        var rest = statements.Skip(consumed).ToList();
        if (rest.Count > 0)
        {
            if (fallback is not null || !cases.All(Returns))
                return null;
            fallback = new Body(rest, null);
        }

        return new Conditional(governing!, cases, fallback);

        static bool Returns(Case @case) => @case.Body.Statements!.LastOrDefault() is ReturnStatementSyntax or ThrowStatementSyntax;
    }

    private static Body BodyOf(StatementSyntax statement) =>
        new(statement is BlockSyntax block ? block.Statements.ToList() : new List<StatementSyntax> { statement }, null);

    private static (ExpressionSyntax Tested, Label Label)? FromCondition(ExpressionSyntax condition, SemanticModel model)
    {
        switch (condition)
        {
            case BinaryExpressionSyntax equals when equals.IsKind(SyntaxKind.EqualsExpression):
                if (model.GetConstantValue(equals.Right).HasValue)
                    return (equals.Left, FromExpression(equals.Right, model));
                if (model.GetConstantValue(equals.Left).HasValue)
                    return (equals.Right, FromExpression(equals.Left, model));
                return null;
            case BinaryExpressionSyntax isType when isType.IsKind(SyntaxKind.IsExpression):
                return (isType.Left, FromExpression(isType.Right, model));
            case IsPatternExpressionSyntax isPattern:
                return (isPattern.Expression, FromPattern(isPattern.Pattern, model));
            default:
                return null;
        }
    }

    private static Label FromExpression(ExpressionSyntax expression, SemanticModel model)
    {
        if (model.GetSymbolInfo(expression).Symbol is INamedTypeSymbol type)
            return new Label(expression, default, type, null);

        var constant = model.GetConstantValue(expression);
        return constant.HasValue
            ? new Label(expression, new Optional<object?>(constant.Value), null, null)
            : throw Unsupported(new Label(expression, default, null, null), "it is neither a constant nor a type");
    }

    private static Label FromPattern(PatternSyntax pattern, SemanticModel model)
    {
        switch (pattern)
        {
            case ConstantPatternSyntax constant:
                return FromExpression(constant.Expression, model);
            case DeclarationPatternSyntax declaration:
                return new Label(pattern, default, TypeOf(declaration.Type, model), Variable(declaration.Designation, model));
            case TypePatternSyntax type:
                return new Label(pattern, default, TypeOf(type.Type, model), null);
            case RecursivePatternSyntax { Type: { } type, PositionalPatternClause: null } recursive
                when recursive.PropertyPatternClause is null || recursive.PropertyPatternClause.Subpatterns.Count == 0:
                return new Label(pattern, default, TypeOf(type, model), recursive.Designation is null ? null : Variable(recursive.Designation, model));
            default:
                throw Unsupported(new Label(pattern, default, null, null), "it is neither a constant nor a type");
        }
    }

    private static INamedTypeSymbol TypeOf(TypeSyntax type, SemanticModel model) =>
        model.GetTypeInfo(type).Type as INamedTypeSymbol
        ?? throw Unsupported(new Label(type, default, null, null), "it is not a class");

    private static ISymbol? Variable(VariableDesignationSyntax designation, SemanticModel model) =>
        designation is SingleVariableDesignationSyntax single ? model.GetDeclaredSymbol(single) : null;
}
