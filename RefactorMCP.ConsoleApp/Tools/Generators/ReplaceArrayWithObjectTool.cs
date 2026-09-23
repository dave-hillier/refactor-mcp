using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
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
public static class ReplaceArrayWithObjectTool
{
    [McpServerTool, Description("Replace an array field or local whose elements mean different things with a new class " +
        "that has one property per element, rewriting its creations and constant-index accesses.")]
    public static async Task<string> ReplaceArrayWithObject(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file containing the array")] string filePath,
        [Description("Line of the array's declaration or of a use of it (1-based)")] int line,
        [Description("Column on that line of the array's name (1-based)")] int column,
        [Description("Name of the class to create")] string className,
        [Description("Property names, one per array index in order")] string[] memberNames,
        CancellationToken cancellationToken = default)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var document = MovingSupport.DocumentOrThrow(solution, filePath);
        var variable = await ArrayReplacement.FindVariableAsync(document, line, column, cancellationToken);

        var updated = await ArrayReplacement.ReplaceAsync(solution, variable, className, memberNames, cancellationToken);
        await MovingSupport.ApplyAsync(solution, updated, cancellationToken);
        return $"Successfully replaced the array {variable.Name} with {className}";
    }
}

/// <summary>
/// Turns an array used as a record, each index meaning something different,
/// into a class with a named property per index.
/// </summary>
internal static class ArrayReplacement
{
    /// <summary>The field or local declared or used at a 1-based position.</summary>
    public static async Task<ISymbol> FindVariableAsync(Document document, int line, int column, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken);
        if (line < 1 || line > text.Lines.Count)
            throw new McpException($"Error: Line {line} is outside {document.FilePath}");

        var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
        var model = (await document.GetSemanticModelAsync(cancellationToken))!;
        var token = root.FindToken(text.Lines[line - 1].Start + column - 1);
        var symbol = model.GetDeclaredSymbol(token.Parent!, cancellationToken) ?? model.GetSymbolInfo(token.Parent!, cancellationToken).Symbol;
        return symbol is IFieldSymbol or ILocalSymbol
            ? symbol
            : throw new McpException($"Error: '{token.ValueText}' at line {line} is not an array field or local");
    }

    public static async Task<Solution> ReplaceAsync(
        Solution solution,
        ISymbol variable,
        string className,
        IReadOnlyList<string> members,
        CancellationToken cancellationToken)
    {
        var type = variable is IFieldSymbol field ? field.Type : ((ILocalSymbol)variable).Type;
        if (type is not IArrayTypeSymbol { Rank: 1 } array)
            throw new McpException($"Error: {variable.Name} is not a single-dimensional array field or local");
        if (array.ElementType.DescendantTypes().Any(t => t is ITypeParameterSymbol))
            throw new McpException($"Error: The elements of {variable.Name} have type {array.ElementType.ToDisplayString()}, which uses a type parameter the new class would have to declare");

        foreach (var name in members.Prepend(className))
        {
            if (!SyntaxFacts.IsValidIdentifier(name) || SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None)
                throw new McpException($"Error: '{name}' is not a valid name");
        }
        if (members.Count == 0 || members.Distinct(StringComparer.Ordinal).Count() != members.Count)
            throw new McpException($"Error: The member names must be distinct and there must be at least one; got '{string.Join("', '", members)}'");

        var declarator = (VariableDeclaratorSyntax)await variable.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken);
        var home = solution.GetDocument(declarator.SyntaxTree)!;
        var homeModel = (await home.GetSemanticModelAsync(cancellationToken))!;
        var ns = homeModel.GetEnclosingSymbol(declarator.SpanStart, cancellationToken)!.ContainingNamespace;
        var newPath = Path.Combine(Path.GetDirectoryName(home.FilePath!)!, className + ".cs");
        if (ns.GetTypeMembers(className).Any() || File.Exists(newPath))
            throw new McpException($"Error: A type named {className} already exists in {ns.ToDisplayString()}");

        var qualifiedName = ns.IsGlobalNamespace ? $"global::{className}" : $"global::{ns.ToDisplayString()}.{className}";
        var context = new Context(variable, members, qualifiedName);
        var edits = new SyntaxEdits();

        var uses = await UsesAsync(solution, variable, declarator, cancellationToken);
        foreach (var (document, use) in uses)
            RewriteIndex(document.Id, use, context, edits);
        foreach (var (document, use) in uses)
            RewriteUse(document.Id, use, context, edits);

        RewriteDeclaration(home.Id, declarator, context, edits);

        var root = ClassRoot(home.Project, (CompilationUnitSyntax)(await home.GetSyntaxRootAsync(cancellationToken))!, ns, className, members, array.ElementType);
        var withType = MovingSupport.AddDocument(home.Project, newPath, root).Project.Solution;
        var added = withType.Projects.SelectMany(p => p.Documents).First(d => d.FilePath == newPath);
        withType = (await MovingSupport.TidyAsync(added, cancellationToken)).Project.Solution;

        var updated = await edits.ApplyAsync(withType, cancellationToken);
        var errors = await TypeRefactoringHelpers.NewErrorsAsync(solution, updated, cancellationToken);
        if (errors.Count > 0)
            throw new McpException($"Error: Replacing {variable.Name} with {className} would break the build: {TypeRefactoringHelpers.Describe(errors)}");

        return updated;
    }

    private sealed record Context(ISymbol Variable, IReadOnlyList<string> Members, string QualifiedName)
    {
        public TypeSyntax Type() =>
            SyntaxFactory.ParseTypeName(QualifiedName).WithAdditionalAnnotations(Simplifier.Annotation, Simplifier.AddImportsAnnotation);
    }

    /// <summary>
    /// Every expression naming the variable: <c>row</c>, <c>this._row</c> or
    /// <c>other._row</c>. A local's uses are in its declaring member; a field's
    /// anywhere in the solution.
    /// </summary>
    private static async Task<List<(Document Document, ExpressionSyntax Use)>> UsesAsync(
        Solution solution,
        ISymbol variable,
        VariableDeclaratorSyntax declarator,
        CancellationToken cancellationToken)
    {
        if (variable is IFieldSymbol)
        {
            return (await MemberReferences.FindAsync(solution, variable, cancellationToken))
                .Select(r => (r.Document, r.Callee))
                .ToList();
        }

        var document = solution.GetDocument(declarator.SyntaxTree)!;
        var model = (await document.GetSemanticModelAsync(cancellationToken))!;
        var scope = declarator.Ancestors().First(a => a is MemberDeclarationSyntax or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax);
        return scope.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(n => SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(n, cancellationToken).Symbol, variable))
            .Select(n => (document, (ExpressionSyntax)n))
            .ToList();
    }

    /// <summary><c>row[1]</c> with a constant index becomes <c>row.Wins</c>.</summary>
    private static void RewriteIndex(DocumentId document, ExpressionSyntax use, Context context, SyntaxEdits edits)
    {
        if (use.Parent is not ElementAccessExpressionSyntax access || access.Expression != use)
            return;

        if (access.ArgumentList.Arguments is not [{ Expression: LiteralExpressionSyntax { Token.Value: int index } }])
            throw Unsupported(use, context);
        if (index < 0 || index >= context.Members.Count)
            throw new McpException($"Error: {access} at {SolutionEdits.Describe(access.GetLocation())} has no member to name it; there are {context.Members.Count} members");

        edits.Replace(document, access, rewritten =>
        {
            var element = (ElementAccessExpressionSyntax)rewritten;
            return SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    element.Expression.WithoutTrivia(),
                    SyntaxFactory.IdentifierName(context.Members[index]))
                .WithTriviaFrom(element);
        });
    }

    /// <summary>A use other than an element access must assign a new array, which becomes a new object.</summary>
    private static void RewriteUse(DocumentId document, ExpressionSyntax use, Context context, SyntaxEdits edits)
    {
        if (use.Parent is ElementAccessExpressionSyntax access && access.Expression == use)
            return;

        if (use.Parent is AssignmentExpressionSyntax assignment && assignment.Left == use && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
        {
            ReplaceCreation(document, assignment.Right, context, edits);
            return;
        }

        throw Unsupported(use, context);
    }

    private static McpException Unsupported(ExpressionSyntax use, Context context)
    {
        var usage = use.Parent is ArgumentSyntax or null ? use : use.Parent;
        return new McpException(
            $"Error: {context.Variable.Name} is used as an array at {SolutionEdits.Describe(use.GetLocation())} ({usage}), which a class with named members cannot replace");
    }

    /// <summary>The declaration takes the class as its type, and its initial array becomes a new object.</summary>
    private static void RewriteDeclaration(DocumentId document, VariableDeclaratorSyntax declarator, Context context, SyntaxEdits edits)
    {
        var declaration = (VariableDeclarationSyntax)declarator.Parent!;
        if (declaration.Variables.Count > 1)
            throw new McpException($"Error: {context.Variable.Name} is declared together with other variables; declare it on its own first");

        if (declarator.Initializer is { } initializer)
            ReplaceCreation(document, initializer.Value, context, edits);

        if (declaration.Type.IsVar)
            return;

        edits.Replace(document, declaration.Type, rewritten =>
        {
            var type = rewritten is NullableTypeSyntax { ElementType: ArrayTypeSyntax }
                ? SyntaxFactory.NullableType(context.Type())
                : context.Type();
            return type.WithTriviaFrom(rewritten);
        });
    }

    /// <summary>
    /// <c>new T[2]</c> becomes <c>new C()</c>; <c>new T[] { a, b }</c>,
    /// <c>new[] { a, b }</c>, <c>{ a, b }</c> and <c>[a, b]</c> become
    /// <c>new C { A = a, B = b }</c>, keeping the initialiser's layout.
    /// </summary>
    private static void ReplaceCreation(DocumentId document, ExpressionSyntax value, Context context, SyntaxEdits edits)
    {
        if (value.IsKind(SyntaxKind.NullLiteralExpression))
            return;

        switch (value)
        {
            case ArrayCreationExpressionSyntax { Initializer: null } creation
                when creation.Type.RankSpecifiers[0].Sizes is [LiteralExpressionSyntax { Token.Value: int size }]:
                CheckCount(size, value, context);
                edits.Replace(document, value, rewritten => SyntaxFactory.ObjectCreationExpression(
                        SyntaxFactory.Token(SyntaxKind.NewKeyword).WithTrailingTrivia(SyntaxFactory.Space),
                        context.Type(),
                        SyntaxFactory.ArgumentList(),
                        null)
                    .WithTriviaFrom(rewritten));
                return;

            case ArrayCreationExpressionSyntax { Initializer: { } initializer } creation:
                if (creation.Type.RankSpecifiers[0].Sizes is [LiteralExpressionSyntax { Token.Value: int declared }])
                    CheckCount(declared, value, context);
                CheckCount(initializer.Expressions.Count, value, context);
                edits.Replace(document, value, rewritten =>
                {
                    var array = (ArrayCreationExpressionSyntax)rewritten;
                    return Creation(array.Initializer!, array.Type.GetTrailingTrivia(), context).WithTriviaFrom(array);
                });
                return;

            case ImplicitArrayCreationExpressionSyntax implicitCreation:
                CheckCount(implicitCreation.Initializer.Expressions.Count, value, context);
                edits.Replace(document, value, rewritten =>
                {
                    var array = (ImplicitArrayCreationExpressionSyntax)rewritten;
                    return Creation(array.Initializer, array.CloseBracketToken.TrailingTrivia, context).WithTriviaFrom(array);
                });
                return;

            case InitializerExpressionSyntax arrayInitializer:
                CheckCount(arrayInitializer.Expressions.Count, value, context);
                edits.Replace(document, value, rewritten =>
                {
                    var initializer = (InitializerExpressionSyntax)rewritten;
                    return Creation(initializer.WithoutLeadingTrivia(), SyntaxFactory.TriviaList(SyntaxFactory.Space), context)
                        .WithLeadingTrivia(initializer.GetLeadingTrivia());
                });
                return;

            case CollectionExpressionSyntax collection when collection.Elements.All(e => e is ExpressionElementSyntax):
                CheckCount(collection.Elements.Count, value, context);
                edits.Replace(document, value, rewritten =>
                {
                    var elements = (CollectionExpressionSyntax)rewritten;
                    var initializer = SyntaxFactory.InitializerExpression(
                        SyntaxKind.ArrayInitializerExpression,
                        SyntaxFactory.Token(SyntaxKind.OpenBraceToken).WithTriviaFrom(elements.OpenBracketToken),
                        SyntaxFactory.SeparatedList(
                            elements.Elements.Cast<ExpressionElementSyntax>().Select(e => e.Expression.WithTriviaFrom(e)),
                            elements.Elements.GetSeparators()),
                        SyntaxFactory.Token(SyntaxKind.CloseBraceToken).WithTriviaFrom(elements.CloseBracketToken));
                    return Creation(Spaced(initializer), SyntaxFactory.TriviaList(SyntaxFactory.Space), context).WithTriviaFrom(elements);
                });
                return;

            default:
                throw new McpException(
                    $"Error: {context.Variable.Name} is used as an array at {SolutionEdits.Describe(value.GetLocation())}: it is assigned {value}, which is not a new array a class with named members can replace");
        }
    }

    /// <summary><c>[a, b]</c> has no spaces inside its brackets; an object initialiser has them.</summary>
    private static InitializerExpressionSyntax Spaced(InitializerExpressionSyntax initializer)
    {
        if (initializer.Expressions.Count == 0 || initializer.OpenBraceToken.TrailingTrivia.Any(t => t.IsKind(SyntaxKind.EndOfLineTrivia)))
            return initializer;

        return initializer
            .WithOpenBraceToken(initializer.OpenBraceToken.WithTrailingTrivia(SyntaxFactory.Space))
            .WithCloseBraceToken(initializer.CloseBraceToken.WithLeadingTrivia(SyntaxFactory.Space));
    }

    private static void CheckCount(int count, ExpressionSyntax creation, Context context)
    {
        if (count != context.Members.Count)
        {
            throw new McpException(
                $"Error: {creation} at {SolutionEdits.Describe(creation.GetLocation())} has {count} elements but {context.Members.Count} member names were given");
        }
    }

    /// <summary>
    /// <c>new C { A = a, B = b }</c> from an array initialiser, keeping its
    /// braces, separators and the trivia around each element.
    /// </summary>
    private static ObjectCreationExpressionSyntax Creation(InitializerExpressionSyntax initializer, SyntaxTriviaList afterType, Context context)
    {
        var assignments = initializer.Expressions.Select((element, index) => (ExpressionSyntax)SyntaxFactory.AssignmentExpression(
            SyntaxKind.SimpleAssignmentExpression,
            SyntaxFactory.IdentifierName(context.Members[index]).WithLeadingTrivia(element.GetLeadingTrivia()).WithTrailingTrivia(SyntaxFactory.Space),
            SyntaxFactory.Token(SyntaxKind.EqualsToken).WithTrailingTrivia(SyntaxFactory.Space),
            element.WithoutLeadingTrivia()));

        var objectInitializer = SyntaxFactory.InitializerExpression(
            SyntaxKind.ObjectInitializerExpression,
            initializer.OpenBraceToken,
            SyntaxFactory.SeparatedList(assignments, initializer.Expressions.GetSeparators()),
            initializer.CloseBraceToken);

        return SyntaxFactory.ObjectCreationExpression(
            SyntaxFactory.Token(SyntaxKind.NewKeyword).WithTrailingTrivia(SyntaxFactory.Space),
            context.Type().WithTrailingTrivia(afterType),
            null,
            objectInitializer);
    }

    /// <summary>
    /// The new class's file, in the namespace and namespace style of the
    /// file declaring the array: one auto-property per member.
    /// </summary>
    private static CompilationUnitSyntax ClassRoot(
        Project project,
        CompilationUnitSyntax home,
        INamespaceSymbol ns,
        string className,
        IReadOnlyList<string> members,
        ITypeSymbol elementType)
    {
        var nullable = project.CompilationOptions is CSharpCompilationOptions { NullableContextOptions: not NullableContextOptions.Disable };
        var startsNull = nullable && !elementType.IsValueType && elementType.NullableAnnotation != NullableAnnotation.Annotated;
        var type = elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat
            .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier));
        var eol = TypeRefactoringHelpers.EndOfLine(project);
        var fileScoped = home.Members.OfType<FileScopedNamespaceDeclarationSyntax>().Any();
        var indent = fileScoped || ns.IsGlobalNamespace ? "" : "    ";

        var lines = new List<string> { $"public class {className}", "{" };
        lines.AddRange(members.Select(m => $"    public {type} {m} {{ get; set; }}" + (startsNull ? " = null!;" : "")));
        lines.Add("}");

        var body = string.Join(eol, lines.Select(l => indent + l)) + eol;
        var text = ns.IsGlobalNamespace
            ? body
            : fileScoped
                ? $"namespace {ns.ToDisplayString()};{eol}{eol}{body}"
                : $"namespace {ns.ToDisplayString()}{eol}{{{eol}{body}}}{eol}";

        var root = SyntaxFactory.ParseCompilationUnit(text);
        return root.ReplaceNodes(
            root.DescendantNodes().OfType<PropertyDeclarationSyntax>().Select(p => p.Type),
            (_, rewritten) => rewritten.WithAdditionalAnnotations(Simplifier.Annotation, Simplifier.AddImportsAnnotation));
    }

    private static IEnumerable<ITypeSymbol> DescendantTypes(this ITypeSymbol type)
    {
        yield return type;
        var children = type switch
        {
            IArrayTypeSymbol array => new[] { array.ElementType },
            INamedTypeSymbol named => named.TypeArguments.ToArray(),
            _ => Array.Empty<ITypeSymbol>(),
        };
        foreach (var child in children.SelectMany(c => c.DescendantTypes()))
            yield return child;
    }
}
