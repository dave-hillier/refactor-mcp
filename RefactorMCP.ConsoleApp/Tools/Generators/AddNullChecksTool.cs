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
public static class AddNullChecksTool
{
    [McpServerTool, Description("Add ArgumentNullException.ThrowIfNull guards at the start of a method or constructor " +
        "for each reference-type parameter that does not accept null and is not already guarded")]
    public static async Task<string> AddNullChecks(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file declaring the method")] string filePath,
        [Description("Name of the method, or of the type for a constructor")] string methodName,
        [Description("Line of the method's declaration (1-based, optional), to choose between overloads")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var method = await SolutionEdits.FindMethodAsync(solution, filePath, methodName, line, cancellationToken);
        if (await method.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken) is not BaseMethodDeclarationSyntax declaration
            || (declaration.Body is null && declaration.ExpressionBody is null))
            throw new McpException($"Error: {methodName} has no body to guard");

        var document = solution.GetDocument(declaration.SyntaxTree)!;
        var model = (await document.GetSemanticModelAsync(cancellationToken))!;
        var body = (SyntaxNode?)declaration.Body ?? declaration.ExpressionBody!;
        var guarded = GuardedParameters(body, model);
        var parameters = method.Parameters.Where(p => RejectsNull(p) && !guarded.Contains(p)).ToList();
        if (parameters.Count == 0)
            throw new McpException($"Error: {methodName} has no reference-type parameter left to guard");

        var original = solution;
        if (declaration.Body is null)
            (document, declaration) = await BlockBodies.ConvertAsync(document, declaration, method, cancellationToken);

        var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
        var updated = root.ReplaceNode(declaration, WithGuards(declaration, parameters));
        if (!BindsArgumentNullException(model, body.SpanStart))
            updated = TypeRefactoringHelpers.AddUsings((CompilationUnitSyntax)updated, new[] { "System" });

        var changed = document.WithSyntaxRoot(updated).Project.Solution;
        await SolutionEdits.EnsureCompilesAsync(original, changed, cancellationToken);
        await MovingSupport.ApplyAsync(original, changed, cancellationToken);
        return $"Added {parameters.Count} null check(s) to {methodName}";
    }

    /// <summary>
    /// A parameter that is meant never to be null: a reference type that is
    /// not annotated as nullable, not <c>out</c>, and does not default to null.
    /// </summary>
    private static bool RejectsNull(IParameterSymbol parameter) =>
        parameter.RefKind != RefKind.Out
        && parameter.Type.IsReferenceType
        && parameter.NullableAnnotation != NullableAnnotation.Annotated
        && !(parameter.HasExplicitDefaultValue && parameter.ExplicitDefaultValue is null);

    /// <summary>
    /// Parameters the body already guards: by <c>ThrowIfNull</c>, by an
    /// <c>if</c> that throws when the parameter is null, or by <c>?? throw</c>.
    /// </summary>
    private static HashSet<IParameterSymbol> GuardedParameters(SyntaxNode body, SemanticModel model)
    {
        var guarded = new HashSet<IParameterSymbol>(SymbolEqualityComparer.Default);
        void Add(ExpressionSyntax? expression)
        {
            if (expression is not null && model.GetSymbolInfo(expression).Symbol is IParameterSymbol parameter)
                guarded.Add(parameter);
        }

        foreach (var node in body.DescendantNodesAndSelf())
        {
            switch (node)
            {
                case InvocationExpressionSyntax { ArgumentList.Arguments: [var argument, ..] } invocation
                    when model.GetSymbolInfo(invocation).Symbol is IMethodSymbol { Name: "ThrowIfNull", ContainingType.Name: "ArgumentNullException" }:
                    Add(argument.Expression);
                    break;
                case IfStatementSyntax ifStatement when Throws(ifStatement.Statement):
                    Add(NullTested(ifStatement.Condition));
                    break;
                case BinaryExpressionSyntax { RawKind: (int)SyntaxKind.CoalesceExpression, Right: ThrowExpressionSyntax } coalesce:
                    Add(coalesce.Left);
                    break;
            }
        }

        return guarded;
    }

    private static bool Throws(StatementSyntax statement) =>
        statement is ThrowStatementSyntax || statement is BlockSyntax { Statements: [ThrowStatementSyntax] };

    /// <summary>The expression a condition compares with null: <c>x == null</c>, <c>null == x</c> or <c>x is null</c>.</summary>
    private static ExpressionSyntax? NullTested(ExpressionSyntax condition) => condition switch
    {
        BinaryExpressionSyntax { RawKind: (int)SyntaxKind.EqualsExpression } binary when IsNull(binary.Right) => binary.Left,
        BinaryExpressionSyntax { RawKind: (int)SyntaxKind.EqualsExpression } binary when IsNull(binary.Left) => binary.Right,
        IsPatternExpressionSyntax { Pattern: ConstantPatternSyntax { Expression: var constant } } pattern when IsNull(constant) => pattern.Expression,
        ParenthesizedExpressionSyntax parenthesized => NullTested(parenthesized.Expression),
        _ => null,
    };

    private static bool IsNull(ExpressionSyntax expression) => expression.IsKind(SyntaxKind.NullLiteralExpression);

    private static bool BindsArgumentNullException(SemanticModel model, int position) =>
        model.GetSpeculativeTypeInfo(position, SyntaxFactory.ParseTypeName("ArgumentNullException"), SpeculativeBindingOption.BindAsTypeOrNamespace)
            .Type is { TypeKind: not TypeKind.Error };

    /// <summary>
    /// The declaration with a guard per parameter at the start of its body,
    /// then a blank line before what followed.
    /// </summary>
    private static BaseMethodDeclarationSyntax WithGuards(
        BaseMethodDeclarationSyntax declaration,
        IReadOnlyList<IParameterSymbol> parameters)
    {
        var eol = TypeRefactoringHelpers.EndOfLine(declaration.SyntaxTree.GetRoot());
        var body = declaration.Body!;
        var indentation = body.Statements.Count > 0
            ? BlockBodies.IndentationOf(body.Statements[0].GetFirstToken())
            : BlockBodies.IndentationOf(body.OpenBraceToken) + "    ";
        var guards = parameters
            .Select(p => SyntaxFactory.ParseStatement($"ArgumentNullException.ThrowIfNull({Escape(p.Name)});")
                .WithLeadingTrivia(SyntaxFactory.Whitespace(indentation))
                .WithTrailingTrivia(eol))
            .ToList();

        var statements = body.Statements;
        if (statements.Count > 0)
            statements = statements.Replace(statements[0], statements[0].WithLeadingTrivia(statements[0].GetLeadingTrivia().Insert(0, eol)));

        return declaration.WithBody(body.WithStatements(statements.InsertRange(0, guards)));
    }

    private static string Escape(string name) =>
        SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;
}
