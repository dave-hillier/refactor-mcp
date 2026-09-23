using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using RefactorMCP.ConsoleApp.Tools.Moving;

[McpServerToolType]
public static class FeatureFlagRefactorTool
{
    [McpServerTool, Description("Replace the if/else on a feature flag check such as flags.IsEnabled(\"Flag\") with strategies: " +
        "each branch moves to the Apply method of its own class, and a private property named after the flag checks it " +
        "and returns the strategy to apply")]
    public static async Task<string> FeatureFlagRefactor(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Feature flag name")] string flagName,
        CancellationToken cancellationToken = default)
    {
        if (!SyntaxFacts.IsValidIdentifier(flagName))
            throw new McpException($"Error: '{flagName}' cannot name the strategy types; use a flag name that is a valid identifier");

        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var document = MovingSupport.DocumentOrThrow(solution, filePath);
        var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
        var model = (await document.GetSemanticModelAsync(cancellationToken))!;

        var checks = root.DescendantNodes().OfType<IfStatementSyntax>()
            .Where(candidate => IsFlagCheck(candidate.Condition, flagName))
            .ToList();
        if (checks.Count == 0)
            throw new McpException($"Error: Feature flag '{flagName}' not found in {filePath}");
        if (checks.Count > 1)
            throw new McpException($"Error: Feature flag '{flagName}' is checked more than once in {filePath}; each check would need its own strategies");

        var check = checks[0];
        var member = check.Ancestors().OfType<MemberDeclarationSyntax>().First(m => m.Parent is TypeDeclarationSyntax);
        var type = (TypeDeclarationSyntax)member.Parent!;
        var typeSymbol = model.GetDeclaredSymbol(type, cancellationToken)!;
        var isStatic = model.GetDeclaredSymbol(member, cancellationToken)?.IsStatic == true;

        var receiver = ((MemberAccessExpressionSyntax)((InvocationExpressionSyntax)check.Condition).Expression).Expression;
        if (model.GetSymbolInfo(receiver, cancellationToken).Symbol is not (IFieldSymbol or IPropertySymbol or INamedTypeSymbol))
            throw new McpException($"Error: The flags checked for '{flagName}' come from '{receiver}', which is not a field, property or type the class can read");

        var names = new StrategyNames(flagName);
        foreach (var name in new[] { names.Interface, names.Enabled, names.Disabled })
        {
            if (model.LookupNamespacesAndTypes(type.SpanStart, name: name).Length > 0)
                throw new McpException($"Error: A type named '{name}' already exists");
        }
        if (typeSymbol.GetMembers(flagName).Length > 0)
            throw new McpException($"Error: {typeSymbol.Name} already has a member named '{flagName}'");

        var branches = new[] { check.Statement, check.Else?.Statement }.OfType<StatementSyntax>().ToList();
        foreach (var branch in branches)
            EnsureSelfContained(branch, model, typeSymbol, flagName);
        var parameters = Parameters(branches, model);

        var updated = Rewrite(root, check, member, type, names, parameters, isStatic);
        var changed = document.WithSyntaxRoot(updated);
        changed = await Formatter.FormatAsync(changed, Formatter.Annotation, cancellationToken: cancellationToken);
        await SolutionEdits.EnsureCompilesAsync(solution, changed.Project.Solution, cancellationToken);
        await MovingSupport.ApplyAsync(solution, changed.Project.Solution, cancellationToken);
        return $"Refactored feature flag '{flagName}' in {filePath} into {names.Enabled} and {names.Disabled}";
    }

    private sealed record StrategyNames(string Flag)
    {
        public string Interface => $"I{Flag}Strategy";
        public string Enabled => $"{Flag}Strategy";
        public string Disabled => $"No{Flag}Strategy";
    }

    private sealed record Parameter(string Name, string Type);

    private static bool IsFlagCheck(ExpressionSyntax condition, string flag) =>
        condition is InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "IsEnabled" },
            ArgumentList.Arguments: [{ Expression: LiteralExpressionSyntax literal }],
        }
        && literal.IsKind(SyntaxKind.StringLiteralExpression)
        && literal.Token.ValueText == flag;

    /// <summary>
    /// Refuses a branch that could not run as the body of another class's
    /// method: one that leaves the method early, reaches members of the class,
    /// or assigns a variable declared outside it.
    /// </summary>
    private static void EnsureSelfContained(StatementSyntax branch, SemanticModel model, INamedTypeSymbol type, string flag)
    {
        var flow = model.AnalyzeControlFlow(branch);
        var yields = branch.DescendantNodesAndSelf(n => n is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
            .OfType<YieldStatementSyntax>();
        if (flow is { Succeeded: true, ExitPoints.Length: > 0 } || yields.Any())
            throw new McpException($"Error: A branch of the '{flag}' check leaves the method early, which a strategy's Apply cannot do for it");

        foreach (var node in branch.DescendantNodesAndSelf())
        {
            if (node is ThisExpressionSyntax or BaseExpressionSyntax)
                throw new McpException($"Error: A branch of the '{flag}' check uses '{node}', a member of {type.Name} that a strategy class cannot reach");

            if (node is not SimpleNameSyntax name)
                continue;

            var symbol = model.GetSymbolInfo(name).Symbol;
            var ofTheClass = symbol is IFieldSymbol or IPropertySymbol or IMethodSymbol or IEventSymbol
                && symbol.ContainingType is { } owner
                && InHierarchyOf(type, owner);
            if (ofTheClass || symbol is IMethodSymbol { MethodKind: MethodKind.LocalFunction })
                throw new McpException($"Error: A branch of the '{flag}' check uses '{name.Identifier.ValueText}', a member of {type.Name} that a strategy class cannot reach");
        }

        var data = model.AnalyzeDataFlow(branch)!;
        var assigned = data.WrittenInside.Except(data.VariablesDeclared, SymbolEqualityComparer.Default).FirstOrDefault();
        if (assigned is not null)
            throw new McpException($"Error: A branch of the '{flag}' check assigns '{assigned.Name}', declared outside it, which Apply would change only in its own copy");
    }

    private static bool InHierarchyOf(INamedTypeSymbol type, INamedTypeSymbol owner)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, owner.OriginalDefinition))
                return true;
        }

        return false;
    }

    /// <summary>
    /// The parameters and locals the branches read, in the order the method
    /// declares them, each typed with the nullability it has where it is read.
    /// </summary>
    private static List<Parameter> Parameters(IReadOnlyList<StatementSyntax> branches, SemanticModel model)
    {
        var read = branches
            .SelectMany(b => model.AnalyzeDataFlow(b)!.DataFlowsIn)
            .Where(s => s is ILocalSymbol or IParameterSymbol { IsThis: false })
            .Distinct(SymbolEqualityComparer.Default)
            .OrderBy(s => s.Locations[0].SourceSpan.Start);

        var parameters = new List<Parameter>();
        foreach (var symbol in read)
        {
            var reference = branches.SelectMany(b => b.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
                .First(n => SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(n).Symbol, symbol));
            var info = model.GetTypeInfo(reference);
            var type = info.Type!;
            if (info.Nullability.FlowState == NullableFlowState.NotNull && type.IsReferenceType)
                type = type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
            parameters.Add(new Parameter(symbol.Name, type.ToMinimalDisplayString(model, reference.SpanStart, DisplayFormat)));
        }

        return parameters;
    }

    private static readonly SymbolDisplayFormat DisplayFormat = SymbolDisplayFormat.MinimallyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>
    /// Replaces the check with a call through the selecting property, adds the
    /// property after the member holding the check, and the strategy types
    /// after the outermost type.
    /// </summary>
    private static SyntaxNode Rewrite(
        SyntaxNode root,
        IfStatementSyntax check,
        MemberDeclarationSyntax member,
        TypeDeclarationSyntax type,
        StrategyNames names,
        IReadOnlyList<Parameter> parameters,
        bool isStatic)
    {
        var eol = TypeRefactoringHelpers.EndOfLine(root);
        var outermost = type.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().Last();
        var checkMark = new SyntaxAnnotation();
        var memberMark = new SyntaxAnnotation();
        var outermostMark = new SyntaxAnnotation();
        root = root.ReplaceNodes(
            new SyntaxNode[] { check, member, outermost },
            (original, rewritten) => rewritten.WithAdditionalAnnotations(
                original == check ? checkMark : original == member ? memberMark : outermostMark));

        var arguments = string.Join(", ", parameters.Select(p => p.Name));
        // A comment trailing the branch moves with it into the strategy.
        var call = SyntaxFactory.ParseStatement($"{names.Flag}.Apply({arguments});")
            .WithLeadingTrivia(check.GetLeadingTrivia())
            .WithTrailingTrivia(eol);
        root = root.ReplaceNode(root.GetAnnotatedNodes(checkMark).Single(), call);

        var property = SyntaxFactory.ParseMemberDeclaration(
                $"private {(isStatic ? "static " : "")}{names.Interface} {names.Flag} => " +
                $"{check.Condition.WithoutTrivia()} ? new {names.Enabled}() : new {names.Disabled}();")!
            .WithLeadingTrivia(eol)
            .WithTrailingTrivia(eol)
            .WithAdditionalAnnotations(Formatter.Annotation);
        root = InsertAfter(root, root.GetAnnotatedNodes(memberMark).Single(), new[] { property });

        var types = StrategyTypes(check, names, parameters)
            .Select(t => t.WithLeadingTrivia(eol).WithTrailingTrivia(eol).WithAdditionalAnnotations(Formatter.Annotation))
            .ToList();
        return InsertAfter(root, root.GetAnnotatedNodes(outermostMark).Single(), types);
    }

    private static SyntaxNode InsertAfter(SyntaxNode root, SyntaxNode anchor, IEnumerable<MemberDeclarationSyntax> members) =>
        anchor.Parent switch
        {
            TypeDeclarationSyntax parent => root.ReplaceNode(parent, parent.WithMembers(parent.Members.InsertRange(parent.Members.IndexOf((MemberDeclarationSyntax)anchor) + 1, members))),
            BaseNamespaceDeclarationSyntax parent => root.ReplaceNode(parent, parent.WithMembers(parent.Members.InsertRange(parent.Members.IndexOf((MemberDeclarationSyntax)anchor) + 1, members))),
            CompilationUnitSyntax parent => parent.WithMembers(parent.Members.InsertRange(parent.Members.IndexOf((MemberDeclarationSyntax)anchor) + 1, members)),
            _ => throw new McpException("Error: The strategy types have nowhere to go"),
        };

    /// <summary>
    /// The strategy interface and its two implementations: the enabled one
    /// runs the if branch, the disabled one the else branch or nothing.
    /// </summary>
    private static IEnumerable<MemberDeclarationSyntax> StrategyTypes(
        IfStatementSyntax check,
        StrategyNames names,
        IReadOnlyList<Parameter> parameters)
    {
        var parameterList = SyntaxFactory.ParseParameterList($"({string.Join(", ", parameters.Select(p => $"{p.Type} {p.Name}"))})");
        var apply = SyntaxFactory.MethodDeclaration(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)), "Apply")
            .WithParameterList(parameterList);

        yield return SyntaxFactory.InterfaceDeclaration(names.Interface)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.InternalKeyword))
            .AddMembers(apply.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));

        yield return Strategy(names.Enabled, names.Interface, apply, check.Statement);
        yield return Strategy(names.Disabled, names.Interface, apply, check.Else?.Statement);
    }

    private static ClassDeclarationSyntax Strategy(string name, string @interface, MethodDeclarationSyntax apply, StatementSyntax? branch)
    {
        var statements = branch switch
        {
            null => SyntaxFactory.List<StatementSyntax>(),
            BlockSyntax block => block.Statements,
            _ => SyntaxFactory.SingletonList(branch),
        };

        return SyntaxFactory.ClassDeclaration(name)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.InternalKeyword), SyntaxFactory.Token(SyntaxKind.SealedKeyword))
            .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.IdentifierName(@interface)))
            .AddMembers(apply
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .WithBody(SyntaxFactory.Block(statements)));
    }
}
