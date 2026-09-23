using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using RefactorMCP.ConsoleApp.Tools.Moving;

namespace RefactorMCP.ConsoleApp.Tools.Generators;

[McpServerToolType]
public static class IntroduceNullObjectTool
{
    [McpServerTool, Description("Generate a null object for the interface a field is typed as, and replace the field's null checks " +
        "across the solution with calls on it. Null assignments to the field assign the null object instead.")]
    public static async Task<string> IntroduceNullObject(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file declaring the field")] string filePath,
        [Description("Name of the field whose null checks the null object replaces")] string fieldName,
        CancellationToken cancellationToken = default)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var document = MovingSupport.DocumentOrThrow(solution, filePath);
        var field = (IFieldSymbol)await MovingSupport.FindDeclaredSymbolAsync(
            document, fieldName, null, s => s is IFieldSymbol, "field", cancellationToken);

        var (updated, typeName) = await NullObjectIntroduction.IntroduceAsync(solution, field, cancellationToken);
        await MovingSupport.ApplyAsync(solution, updated, cancellationToken);
        return $"Successfully introduced {typeName} for {fieldName}";
    }
}

/// <summary>
/// Generates a null implementation of a field's interface and rewrites the
/// field's null checks into plain calls, remembering the value each check
/// fell back to so the null object can return it.
/// </summary>
internal static class NullObjectIntroduction
{
    /// <summary>Marks the body of a removed <c>if</c>, whose statements join the enclosing block.</summary>
    private static readonly SyntaxAnnotation Splice = new("NullObjectSplice");

    private sealed record Fallback(ExpressionSyntax Expression, object? Value, Location Location);

    public static async Task<(Solution Solution, string TypeName)> IntroduceAsync(
        Solution solution,
        IFieldSymbol field,
        CancellationToken cancellationToken)
    {
        if (field.Type is not INamedTypeSymbol { TypeKind: TypeKind.Interface } contract)
            throw new McpException($"Error: {field.Name} has type {field.Type.ToDisplayString()}, which is not an interface; extract an interface first");

        var typeName = "Null" + (contract.Name.Length > 1 && contract.Name[0] == 'I' && char.IsUpper(contract.Name[1])
            ? contract.Name[1..]
            : contract.Name);
        var (filePath, fileRoot) = await HomeOfAsync(solution, contract, field, cancellationToken);
        var newPath = Path.Combine(Path.GetDirectoryName(filePath)!, typeName + ".cs");
        if (contract.ContainingNamespace.GetTypeMembers(typeName, contract.Arity).Any() || File.Exists(newPath))
            throw new McpException($"Error: A type named {typeName} already exists in {contract.ContainingNamespace.ToDisplayString()}");

        var instance = InstanceExpression(contract, typeName, field.Type);
        var edits = new SyntaxEdits();
        var fallbacks = new Dictionary<ISymbol, Fallback>(SymbolEqualityComparer.Default);
        var assignments = new List<SyntaxNode>();
        var checks = 0;

        foreach (var reference in await MemberReferences.FindAsync(solution, field, cancellationToken))
        {
            var use = reference.Callee;
            var document = reference.Document.Id;
            var model = reference.Model;
            switch (use.Parent)
            {
                case AssignmentExpressionSyntax assignment when assignment.Left == use && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression):
                    assignments.Add(assignment);
                    ReplaceAssignedValue(assignment.Right, model, field, instance, document, edits);
                    break;

                case ConditionalAccessExpressionSyntax access when access.Expression == use:
                    checks++;
                    RewriteConditionalAccess(access, model, fallbacks, document, edits);
                    break;

                case BinaryExpressionSyntax binary when IsNullComparison(binary, use, out var notNull):
                    checks++;
                    RewriteComparison(binary, notNull, model, field, fallbacks, document, edits);
                    break;

                case IsPatternExpressionSyntax pattern when IsNullPattern(pattern, use, out var notNull):
                    checks++;
                    RewriteComparison(pattern, notNull, model, field, fallbacks, document, edits);
                    break;

                case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.CoalesceExpression) && binary.Left == use:
                    throw Unsupported(binary);

                case AssignmentExpressionSyntax assignment when assignment.Left == use && assignment.IsKind(SyntaxKind.CoalesceAssignmentExpression):
                    throw Unsupported(assignment);
            }
        }

        if (checks == 0)
            throw new McpException($"Error: {field.Name} is never checked for null, so a null object would replace nothing");

        await EditDeclarationAsync(solution, field, instance, assignments, edits, cancellationToken);

        var home = solution.GetDocument(fileRoot.SyntaxTree)!;
        var root = NullObjectRoot(home.Project, contract.OriginalDefinition, typeName, fileRoot, fallbacks);
        var withType = MovingSupport.AddDocument(home.Project, newPath, root).Project.Solution;
        withType = (await MovingSupport.TidyAsync(withType.GetDocument(withType.Projects.SelectMany(p => p.Documents).First(d => d.FilePath == newPath).Id)!, cancellationToken)).Project.Solution;

        var updated = await edits.ApplyAsync(withType, cancellationToken);
        updated = await SpliceAsync(solution, updated, cancellationToken);

        var errors = await TypeRefactoringHelpers.NewErrorsAsync(solution, updated, cancellationToken);
        if (errors.Count > 0)
            throw new McpException($"Error: Introducing {typeName} would break the build: {TypeRefactoringHelpers.Describe(errors)}");

        return (updated, typeName);
    }

    /// <summary>The file the null object sits beside: the interface's, or the field's when the interface is not in source.</summary>
    private static async Task<(string Path, CompilationUnitSyntax Root)> HomeOfAsync(
        Solution solution,
        INamedTypeSymbol contract,
        IFieldSymbol field,
        CancellationToken cancellationToken)
    {
        var location = contract.Locations.FirstOrDefault(l => l.IsInSource && solution.GetDocument(l.SourceTree) is not null)
            ?? field.Locations.First(l => l.IsInSource);
        var root = (CompilationUnitSyntax)await location.SourceTree!.GetRootAsync(cancellationToken);
        return (location.SourceTree.FilePath, root);
    }

    /// <summary><c>NullLogger.Instance</c>, qualified so the simplifier can shorten it and import its namespace.</summary>
    private static ExpressionSyntax InstanceExpression(INamedTypeSymbol contract, string typeName, ITypeSymbol fieldType)
    {
        var ns = contract.ContainingNamespace.IsGlobalNamespace ? "global::" : $"global::{contract.ContainingNamespace.ToDisplayString()}.";
        var arguments = ((INamedTypeSymbol)fieldType).TypeArguments;
        var generic = arguments.Length == 0
            ? ""
            : "<" + string.Join(", ", arguments.Select(a => a.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))) + ">";
        return SyntaxFactory.ParseExpression($"{ns}{typeName}{generic}.Instance")
            .WithAdditionalAnnotations(Simplifier.Annotation, Simplifier.AddImportsAnnotation);
    }

    private static bool IsNullComparison(BinaryExpressionSyntax binary, ExpressionSyntax use, out bool notNull)
    {
        notNull = binary.IsKind(SyntaxKind.NotEqualsExpression);
        if (!binary.IsKind(SyntaxKind.EqualsExpression) && !notNull)
            return false;

        var other = binary.Left == use ? binary.Right : binary.Right == use ? binary.Left : null;
        return other.IsKind(SyntaxKind.NullLiteralExpression);
    }

    private static bool IsNullPattern(IsPatternExpressionSyntax pattern, ExpressionSyntax use, out bool notNull)
    {
        notNull = pattern.Pattern is UnaryPatternSyntax { Pattern: ConstantPatternSyntax inner } && inner.Expression.IsKind(SyntaxKind.NullLiteralExpression);
        return pattern.Expression == use
            && (notNull || pattern.Pattern is ConstantPatternSyntax constant && constant.Expression.IsKind(SyntaxKind.NullLiteralExpression));
    }

    private static McpException Unsupported(SyntaxNode check) =>
        new($"Error: The null check at {SolutionEdits.Describe(check.GetLocation())} is not one a null object can replace: {check}");

    /// <summary>
    /// A null assignment assigns the null object; any other value that might
    /// be null falls back to it.
    /// </summary>
    private static void ReplaceAssignedValue(
        ExpressionSyntax value,
        SemanticModel model,
        IFieldSymbol field,
        ExpressionSyntax instance,
        DocumentId document,
        SyntaxEdits edits)
    {
        if (value.IsKind(SyntaxKind.NullLiteralExpression) || value.IsKind(SyntaxKind.DefaultLiteralExpression))
        {
            edits.Replace(document, value, rewritten => instance.WithTriviaFrom(rewritten));
            return;
        }

        if (value is BaseObjectCreationExpressionSyntax or ThisExpressionSyntax
            || SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(value).Symbol, field))
            return;

        var sameType = SymbolEqualityComparer.Default.Equals(model.GetTypeInfo(value).Type, field.Type);
        edits.Replace(document, value, rewritten =>
        {
            var left = (ExpressionSyntax)rewritten.WithoutTrivia();
            if (!sameType)
                left = SyntaxFactory.CastExpression(MovingSupport.QualifiedType(field.Type.WithNullableAnnotation(NullableAnnotation.None)), Parenthesized(left));
            else if (left is not (IdentifierNameSyntax or MemberAccessExpressionSyntax or InvocationExpressionSyntax or ElementAccessExpressionSyntax or ParenthesizedExpressionSyntax))
                left = SyntaxFactory.ParenthesizedExpression(left);

            var coalesce = SyntaxFactory.Token(SyntaxKind.QuestionQuestionToken)
                .WithLeadingTrivia(SyntaxFactory.Space)
                .WithTrailingTrivia(SyntaxFactory.Space);
            return SyntaxFactory.BinaryExpression(SyntaxKind.CoalesceExpression, left, coalesce, instance).WithTriviaFrom(rewritten);
        });
    }

    private static ExpressionSyntax Parenthesized(ExpressionSyntax expression) =>
        expression is IdentifierNameSyntax or MemberAccessExpressionSyntax or InvocationExpressionSyntax or ParenthesizedExpressionSyntax
            ? expression
            : SyntaxFactory.ParenthesizedExpression(expression);

    /// <summary>
    /// <c>_f?.M()</c> as a statement becomes <c>_f.M()</c>;
    /// <c>_f?.M() ?? fallback</c> becomes <c>_f.M()</c> and M's null implementation returns the fallback.
    /// </summary>
    private static void RewriteConditionalAccess(
        ConditionalAccessExpressionSyntax access,
        SemanticModel model,
        Dictionary<ISymbol, Fallback> fallbacks,
        DocumentId document,
        SyntaxEdits edits)
    {
        if (access.Parent is ExpressionStatementSyntax)
        {
            edits.Replace(document, access, rewritten => Unconditional((ConditionalAccessExpressionSyntax)rewritten));
            return;
        }

        if (access.Parent is BinaryExpressionSyntax coalesce && coalesce.IsKind(SyntaxKind.CoalesceExpression) && coalesce.Left == access)
        {
            var name = access.WhenNotNull switch
            {
                MemberBindingExpressionSyntax binding => binding.Name,
                InvocationExpressionSyntax { Expression: MemberBindingExpressionSyntax binding } => binding.Name,
                _ => throw Unsupported(coalesce),
            };
            Record(fallbacks, model, name, coalesce.Right, coalesce);
            ReplaceCheck(coalesce, document, edits, rewritten => Unconditional((ConditionalAccessExpressionSyntax)((BinaryExpressionSyntax)rewritten).Left));
            return;
        }

        throw Unsupported(access);
    }

    /// <summary>
    /// <c>if (_f != null) S</c> becomes S; <c>_f != null ? _f.M() : fallback</c>
    /// (or its <c>==</c> mirror) becomes <c>_f.M()</c>.
    /// </summary>
    private static void RewriteComparison(
        ExpressionSyntax comparison,
        bool notNull,
        SemanticModel model,
        IFieldSymbol field,
        Dictionary<ISymbol, Fallback> fallbacks,
        DocumentId document,
        SyntaxEdits edits)
    {
        if (comparison.Parent is IfStatementSyntax statement && statement.Condition == comparison && notNull && statement.Else is null)
        {
            edits.Replace(document, statement, rewritten => Unwrap((IfStatementSyntax)rewritten));
            return;
        }

        if (comparison.Parent is ConditionalExpressionSyntax conditional && conditional.Condition == comparison)
        {
            var (value, fallback) = notNull ? (conditional.WhenTrue, conditional.WhenFalse) : (conditional.WhenFalse, conditional.WhenTrue);
            var name = value switch
            {
                MemberAccessExpressionSyntax access when IsField(access.Expression) => access.Name,
                InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } when IsField(access.Expression) => access.Name,
                _ => throw Unsupported(conditional),
            };
            Record(fallbacks, model, name, fallback, conditional);
            ReplaceCheck(conditional, document, edits, rewritten =>
            {
                var whole = (ConditionalExpressionSyntax)rewritten;
                return notNull ? whole.WhenTrue : whole.WhenFalse;
            });
            return;
        }

        throw Unsupported(comparison);

        bool IsField(ExpressionSyntax expression) =>
            SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(expression).Symbol, field);
    }

    /// <summary>Replaces a check with what it collapses to, dropping parentheses that no longer group anything.</summary>
    private static void ReplaceCheck(ExpressionSyntax check, DocumentId document, SyntaxEdits edits, Func<SyntaxNode, ExpressionSyntax> collapse)
    {
        if (check.Parent is ParenthesizedExpressionSyntax parentheses)
        {
            edits.Replace(document, parentheses, rewritten =>
            {
                var inner = collapse(((ParenthesizedExpressionSyntax)rewritten).Expression);
                return inner is InvocationExpressionSyntax or MemberAccessExpressionSyntax
                    ? inner.WithTriviaFrom(rewritten)
                    : ((ParenthesizedExpressionSyntax)rewritten).WithExpression(inner.WithoutTrivia());
            });
            return;
        }

        edits.Replace(document, check, rewritten => collapse(rewritten).WithTriviaFrom(rewritten));
    }

    private static void Record(Dictionary<ISymbol, Fallback> fallbacks, SemanticModel model, SimpleNameSyntax name, ExpressionSyntax value, SyntaxNode check)
    {
        var member = model.GetSymbolInfo(name).Symbol?.OriginalDefinition ?? throw Unsupported(check);
        var constant = model.GetConstantValue(value);
        var isDefault = value.IsKind(SyntaxKind.DefaultLiteralExpression) || value is DefaultExpressionSyntax;
        if (!constant.HasValue && !isDefault)
            throw new McpException($"Error: The null check at {SolutionEdits.Describe(check.GetLocation())} is not one a null object can replace: it falls back to {value}, which is not a constant");

        var fallback = new Fallback(value.WithoutTrivia(), isDefault ? null : constant.Value, check.GetLocation());
        if (fallbacks.TryGetValue(member, out var earlier) && !Equals(earlier.Value, fallback.Value))
        {
            throw new McpException(
                $"Error: The null checks fall back to different values for {member.Name}: {earlier.Expression} at {SolutionEdits.Describe(earlier.Location)} and {value} at {SolutionEdits.Describe(fallback.Location)}");
        }

        fallbacks[member] = fallback;
    }

    /// <summary><c>_f?.A?.B()</c> becomes <c>_f.A?.B()</c>: the first binding belongs to the outermost access.</summary>
    private static ExpressionSyntax Unconditional(ConditionalAccessExpressionSyntax access)
    {
        var binding = access.WhenNotNull.DescendantNodesAndSelf()
            .First(n => n is MemberBindingExpressionSyntax or ElementBindingExpressionSyntax);
        var receiver = access.Expression.WithoutTrivia();
        ExpressionSyntax replacement = binding is MemberBindingExpressionSyntax member
            ? SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, receiver, member.Name)
            : SyntaxFactory.ElementAccessExpression(receiver, ((ElementBindingExpressionSyntax)binding).ArgumentList);
        return access.WhenNotNull.ReplaceNode(binding, replacement).WithTriviaFrom(access);
    }

    /// <summary>
    /// The statements an <c>if</c> guarded, marked to join the enclosing
    /// block. The <c>if</c>'s leading comments stay above them.
    /// </summary>
    private static SyntaxNode Unwrap(IfStatementSyntax statement)
    {
        if (statement.Statement is not BlockSyntax block)
        {
            return statement.Statement
                .WithLeadingTrivia(statement.GetLeadingTrivia())
                .WithAdditionalAnnotations(Formatter.Annotation);
        }

        var statements = block.Statements;
        if (statements.Count == 0)
            return block.WithTriviaFrom(statement).WithAdditionalAnnotations(Splice);

        var first = statements[0];
        statements = statements.Replace(first, first.WithLeadingTrivia(
            statement.GetLeadingTrivia().AddRange(first.GetLeadingTrivia().SkipWhile(t => t.IsKind(SyntaxKind.WhitespaceTrivia)))));
        return block.WithStatements(statements)
            .WithTrailingTrivia(statement.GetTrailingTrivia())
            .WithAdditionalAnnotations(Splice);
    }

    /// <summary>Moves the statements of every unwrapped <c>if</c> into the block around it, and reformats them.</summary>
    private static async Task<Solution> SpliceAsync(Solution original, Solution updated, CancellationToken cancellationToken)
    {
        foreach (var change in updated.GetChanges(original).GetProjectChanges())
        {
            foreach (var id in change.GetChangedDocuments(onlyGetDocumentsWithTextChanges: true))
            {
                var document = updated.GetDocument(id)!;
                var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
                if (!root.GetAnnotatedNodes(Splice).Any())
                    continue;

                while (root.GetAnnotatedNodes(Splice).OfType<BlockSyntax>().FirstOrDefault(b => b.Parent is BlockSyntax) is { } spliced)
                {
                    var parent = (BlockSyntax)spliced.Parent!;
                    var index = parent.Statements.IndexOf(spliced);
                    var moved = spliced.Statements.Select(s => s.WithAdditionalAnnotations(Formatter.Annotation)).ToList();
                    if (moved.Count > 0)
                        moved[^1] = moved[^1].WithTrailingTrivia(moved[^1].GetTrailingTrivia().AddRange(Comments(spliced.GetTrailingTrivia())));

                    var statements = parent.Statements.RemoveAt(index).InsertRange(index, moved);
                    root = root.ReplaceNode(parent, parent.WithStatements(statements));
                }

                document = document.WithSyntaxRoot(root);
                document = await Formatter.FormatAsync(document, Formatter.Annotation, cancellationToken: cancellationToken);
                updated = document.Project.Solution;
            }
        }

        return updated;

        static IEnumerable<SyntaxTrivia> Comments(SyntaxTriviaList trivia) =>
            trivia.Any(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) || t.IsKind(SyntaxKind.MultiLineCommentTrivia))
                ? trivia.SkipWhile(t => t.IsKind(SyntaxKind.EndOfLineTrivia))
                : Enumerable.Empty<SyntaxTrivia>();
    }

    /// <summary>
    /// Makes a nullable field non-nullable, and gives it the null object as
    /// its initial value when some constructor could leave it null.
    /// </summary>
    private static async Task EditDeclarationAsync(
        Solution solution,
        IFieldSymbol field,
        ExpressionSyntax instance,
        IReadOnlyList<SyntaxNode> assignments,
        SyntaxEdits edits,
        CancellationToken cancellationToken)
    {
        var declarator = (VariableDeclaratorSyntax)await field.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken);
        var document = solution.GetDocument(declarator.SyntaxTree)!;
        var model = (await document.GetSemanticModelAsync(cancellationToken))!;
        var declaration = (VariableDeclarationSyntax)declarator.Parent!;

        if (declaration.Type is NullableTypeSyntax nullable && declaration.Variables.Count == 1)
            edits.Replace(document.Id, declaration.Type, _ => nullable.ElementType.WithTriviaFrom(nullable));

        if (declarator.Initializer is { } initializer)
        {
            ReplaceAssignedValue(initializer.Value, model, field, instance, document.Id, edits);
            return;
        }

        if (await AlwaysAssignedAsync(field, assignments, cancellationToken))
            return;

        edits.Replace(document.Id, declarator, rewritten =>
        {
            var variable = (VariableDeclaratorSyntax)rewritten;
            return variable
                .WithIdentifier(variable.Identifier.WithTrailingTrivia(SyntaxFactory.Space))
                .WithInitializer(SyntaxFactory.EqualsValueClause(
                    SyntaxFactory.Token(SyntaxKind.EqualsToken).WithTrailingTrivia(SyntaxFactory.Space),
                    instance));
        });
    }

    /// <summary>Whether every constructor assigns the field, or chains to one that does.</summary>
    private static async Task<bool> AlwaysAssignedAsync(IFieldSymbol field, IReadOnlyList<SyntaxNode> assignments, CancellationToken cancellationToken)
    {
        var constructors = field.IsStatic ? field.ContainingType.StaticConstructors : field.ContainingType.InstanceConstructors;
        foreach (var constructor in constructors)
        {
            if (constructor.IsImplicitlyDeclared)
                return false;

            var syntax = (ConstructorDeclarationSyntax)await constructor.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken);
            if (syntax.Initializer?.IsKind(SyntaxKind.ThisConstructorInitializer) == true)
                continue;
            if (!assignments.Any(a => a.SyntaxTree == syntax.SyntaxTree && syntax.Span.Contains(a.Span)))
                return false;
        }

        return constructors.Length > 0;
    }

    private static readonly SymbolDisplayFormat SignatureFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeTypeConstraints,
        memberOptions: SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeType,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName
            | SymbolDisplayParameterOptions.IncludeParamsRefOut | SymbolDisplayParameterOptions.IncludeDefaultValue,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private static readonly SymbolDisplayFormat TypeFormat = SignatureFormat.WithGenericsOptions(SymbolDisplayGenericsOptions.IncludeTypeParameters);

    /// <summary>
    /// The file declaring the null object, in the namespace and namespace
    /// style of <paramref name="home"/>: a sealed class with a single
    /// instance, implementing every member of the interface to do nothing.
    /// </summary>
    private static CompilationUnitSyntax NullObjectRoot(
        Project project,
        INamedTypeSymbol contract,
        string typeName,
        CompilationUnitSyntax home,
        IReadOnlyDictionary<ISymbol, Fallback> fallbacks)
    {
        var nullable = project.CompilationOptions is CSharpCompilationOptions { NullableContextOptions: not NullableContextOptions.Disable };
        var fileScoped = home.Members.OfType<FileScopedNamespaceDeclarationSyntax>().Any();
        var indent = fileScoped || contract.ContainingNamespace.IsGlobalNamespace ? "" : "    ";
        var eol = TypeRefactoringHelpers.EndOfLine(project);

        var parameters = contract.TypeParameters.Length == 0 ? "" : "<" + string.Join(", ", contract.TypeParameters.Select(t => t.Name)) + ">";
        var self = typeName + parameters;
        var lines = new List<string>
        {
            $"public sealed class {self} : {contract.ToDisplayString(SignatureFormat)}",
            "{",
            $"    public static readonly {self} Instance = new {self}();",
            "",
            $"    private {typeName}()",
            "    {",
            "    }",
        };

        foreach (var member in contract.GetMembers().Concat(contract.AllInterfaces.SelectMany(i => i.GetMembers())).Where(m => m.IsAbstract && !m.IsStatic))
        {
            var body = Member(member, fallbacks, nullable);
            if (body.Count == 0)
                continue;

            lines.Add("");
            lines.AddRange(body.Select(line => "    " + line));
        }

        lines.Add("}");

        var text = new StringBuilder();
        if (contract.ContainingNamespace.IsGlobalNamespace)
        {
            text.Append(string.Join(eol, lines)).Append(eol);
        }
        else if (fileScoped)
        {
            text.Append($"namespace {contract.ContainingNamespace.ToDisplayString()};{eol}{eol}");
            text.Append(string.Join(eol, lines)).Append(eol);
        }
        else
        {
            text.Append($"namespace {contract.ContainingNamespace.ToDisplayString()}{eol}{{{eol}");
            text.Append(string.Join(eol, lines.Select(l => l.Length == 0 ? l : indent + l))).Append(eol);
            text.Append($"}}{eol}");
        }

        var root = (CompilationUnitSyntax)SyntaxFactory.ParseCompilationUnit(text.ToString());
        return root.ReplaceNodes(
            root.DescendantNodes().OfType<AliasQualifiedNameSyntax>().Select(Outermost).Distinct(),
            (_, rewritten) => rewritten.WithAdditionalAnnotations(Simplifier.Annotation, Simplifier.AddImportsAnnotation));

        // global::System.Array.Empty<T>() is simplified as a whole, not from its first name.
        static SyntaxNode Outermost(SyntaxNode node)
        {
            while (node.Parent is QualifiedNameSyntax q && q.Left == node || node.Parent is MemberAccessExpressionSyntax m && m.Expression == node)
                node = node.Parent!;
            return node;
        }
    }

    /// <summary>The lines of one member of the null object, unindented.</summary>
    private static List<string> Member(ISymbol member, IReadOnlyDictionary<ISymbol, Fallback> fallbacks, bool nullable)
    {
        switch (member)
        {
            case IMethodSymbol { MethodKind: MethodKind.Ordinary } method:
            {
                var lines = new List<string> { "public " + method.ToDisplayString(SignatureFormat), "{" };
                foreach (var parameter in method.Parameters.Where(p => p.RefKind == RefKind.Out))
                    lines.Add($"    {parameter.Name} = {DefaultFor(parameter.Type, nullable)};");
                if (!method.ReturnsVoid)
                    lines.Add($"    return {ValueFor(method, method.ReturnType, fallbacks, nullable)};");
                lines.Add("}");
                return lines;
            }

            case IPropertySymbol property:
            {
                var type = property.Type.ToDisplayString(TypeFormat);
                var name = property.IsIndexer
                    ? $"this[{string.Join(", ", property.Parameters.Select(p => p.ToDisplayString(SignatureFormat)))}]"
                    : property.Name;
                var value = ValueFor(property, property.Type, fallbacks, nullable);
                if (property.SetMethod is null)
                    return new List<string> { $"public {type} {name} => {value};" };

                var setter = property.SetMethod.IsInitOnly ? "init" : "set";
                return new List<string> { property.GetMethod is null
                    ? $"public {type} {name} {{ {setter} {{ }} }}"
                    : $"public {type} {name} {{ get => {value}; {setter} {{ }} }}" };
            }

            case IEventSymbol @event:
                return new List<string> { $"public event {@event.Type.ToDisplayString(TypeFormat)} {@event.Name} {{ add {{ }} remove {{ }} }}" };

            default:
                return new List<string>();
        }
    }

    /// <summary>
    /// What a member of the null object returns: the fallback the removed
    /// checks used, an empty sequence for a sequence type, or the default.
    /// </summary>
    private static string ValueFor(ISymbol member, ITypeSymbol type, IReadOnlyDictionary<ISymbol, Fallback> fallbacks, bool nullable)
    {
        if (fallbacks.TryGetValue(member.OriginalDefinition, out var fallback))
            return fallback.Expression.ToString();

        var element = type switch
        {
            IArrayTypeSymbol { Rank: 1 } array => array.ElementType,
            INamedTypeSymbol { IsGenericType: true } named when named.ConstructedFrom.SpecialType is SpecialType.System_Collections_Generic_IEnumerable_T
                or SpecialType.System_Collections_Generic_IReadOnlyList_T or SpecialType.System_Collections_Generic_IReadOnlyCollection_T
                => named.TypeArguments[0],
            _ => null,
        };

        return element is null
            ? DefaultFor(type, nullable)
            : $"global::System.Array.Empty<{element.ToDisplayString(TypeFormat)}>()";
    }

    /// <summary><c>default</c>, forgiven when nullable analysis would warn that it is null.</summary>
    private static string DefaultFor(ITypeSymbol type, bool nullable) =>
        nullable && type.NullableAnnotation != NullableAnnotation.Annotated && !type.IsValueType ? "default!" : "default";
}
