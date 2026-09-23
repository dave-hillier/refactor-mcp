using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using RefactorMCP.ConsoleApp.Tools.Moving;

namespace RefactorMCP.ConsoleApp.Tools.Generators;

[McpServerToolType]
public static class ConvertToNullableAwareTool
{
    /// <summary>More rounds than any chain of declarations a null flows through in practice.</summary>
    private const int MaxRounds = 50;

    [McpServerTool, Description("Enable nullable reference types in one file with #nullable enable, and annotate as nullable " +
        "the fields, properties, parameters, locals and return types that the compiler shows can hold null, until the file " +
        "compiles without new warnings. Refuses when a warning remains that no annotation fixes, such as a possible null dereference")]
    public static async Task<string> ConvertToNullableAware(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file to convert")] string filePath,
        CancellationToken cancellationToken = default)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var document = MovingSupport.DocumentOrThrow(solution, filePath);
        var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
        if (document.Project.CompilationOptions?.NullableContextOptions == NullableContextOptions.Enable)
            throw new McpException($"Error: {document.Name} is already nullable-aware: its project enables nullable reference types");
        if (root.DescendantTrivia().Any(t => t.IsKind(SyntaxKind.NullableDirectiveTrivia)))
            throw new McpException($"Error: {document.Name} is already nullable-aware: it sets its own nullable context");

        var text = await document.GetTextAsync(cancellationToken);
        var eol = TypeRefactoringHelpers.EndOfLine(root).ToString();
        document = document.WithText(SourceText.From($"#nullable enable{eol}{eol}{text}", text.Encoding));

        var annotated = 0;
        for (var round = 0; ; round++)
        {
            var warnings = await SolutionEdits.NewDiagnosticsAsync(
                solution, document.Project.Solution, d => d.Severity >= DiagnosticSeverity.Warning, cancellationToken);
            if (warnings.Count == 0)
                break;

            var model = (await document.GetSemanticModelAsync(cancellationToken))!;
            var types = warnings
                .Where(w => w.Location.SourceTree == model.SyntaxTree)
                .Select(w => TypeToAnnotate(w, model, cancellationToken))
                .OfType<TypeSyntax>()
                .Distinct()
                .ToList();
            if (types.Count == 0 || round == MaxRounds)
            {
                var warning = warnings.FirstOrDefault(w => TypeToAnnotate(w, model, cancellationToken) is null) ?? warnings[0];
                throw new McpException(
                    $"Error: Enabling nullable in {document.Name} leaves a warning no annotation fixes: {SolutionEdits.Describe(warning)}");
            }

            var current = (await document.GetSyntaxRootAsync(cancellationToken))!;
            document = document.WithSyntaxRoot(current.ReplaceNodes(types, (_, type) =>
                SyntaxFactory.NullableType(type.WithoutTrailingTrivia()).WithTrailingTrivia(type.GetTrailingTrivia())));
            annotated += types.Count;
        }

        await SolutionEdits.EnsureCompilesAsync(solution, document.Project.Solution, cancellationToken);
        await MovingSupport.ApplyAsync(solution, document.Project.Solution, cancellationToken);
        return $"Enabled nullable reference types in {document.Name} and annotated {annotated} declaration(s)";
    }

    /// <summary>
    /// The declared type whose annotation would answer a nullable warning:
    /// the target a null flows into, the return type that returns it, or the
    /// member a constructor leaves unset. Null when the warning is not one an
    /// annotation in this file fixes.
    /// </summary>
    private static TypeSyntax? TypeToAnnotate(Diagnostic warning, SemanticModel model, CancellationToken cancellationToken)
    {
        var root = model.SyntaxTree.GetRoot(cancellationToken);
        var node = root.FindNode(warning.Location.SourceSpan, getInnermostNodeForTie: true);
        var symbol = warning.Id switch
        {
            "CS8600" or "CS8601" or "CS8625" or "CS8604" => FlowTarget(node, model, cancellationToken),
            "CS8603" => ReturningMember(node, model, cancellationToken),
            "CS8618" => UnsetMember(node, warning, model, cancellationToken),
            "CS8765" or "CS8767" => MismatchedParameter(node, warning, model, cancellationToken),
            _ => null,
        };

        return symbol is null ? null : DeclaredType(symbol, model.SyntaxTree, cancellationToken);
    }

    /// <summary>What a possibly null value is assigned, initialised or passed to.</summary>
    private static ISymbol? FlowTarget(SyntaxNode node, SemanticModel model, CancellationToken cancellationToken)
    {
        foreach (var ancestor in node.AncestorsAndSelf())
        {
            switch (ancestor)
            {
                case ArgumentSyntax argument:
                    return (model.GetOperation(argument, cancellationToken) as IArgumentOperation)?.Parameter;
                case EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator }:
                    return model.GetDeclaredSymbol(declarator, cancellationToken);
                case EqualsValueClauseSyntax { Parent: ParameterSyntax parameter }:
                    return model.GetDeclaredSymbol(parameter, cancellationToken);
                case EqualsValueClauseSyntax { Parent: PropertyDeclarationSyntax property }:
                    return model.GetDeclaredSymbol(property, cancellationToken);
                case AssignmentExpressionSyntax assignment when assignment.Right.Span.Contains(node.Span):
                    return model.GetSymbolInfo(assignment.Left, cancellationToken).Symbol;
                case StatementSyntax or MemberDeclarationSyntax:
                    return null;
            }
        }

        return null;
    }

    /// <summary>The method, property or local function a possibly null value is returned from.</summary>
    private static ISymbol? ReturningMember(SyntaxNode node, SemanticModel model, CancellationToken cancellationToken)
    {
        foreach (var ancestor in node.AncestorsAndSelf())
        {
            switch (ancestor)
            {
                case AnonymousFunctionExpressionSyntax:
                    return null;
                case LocalFunctionStatementSyntax or MethodDeclarationSyntax or PropertyDeclarationSyntax or IndexerDeclarationSyntax:
                    return model.GetDeclaredSymbol(ancestor, cancellationToken);
            }
        }

        return null;
    }

    /// <summary>
    /// The field, property or event a constructor leaves unset. The warning
    /// sits on the member itself when the type has no constructor, and on the
    /// constructor, naming the member, when it has one.
    /// </summary>
    private static ISymbol? UnsetMember(SyntaxNode node, Diagnostic warning, SemanticModel model, CancellationToken cancellationToken)
    {
        if (node is VariableDeclaratorSyntax or PropertyDeclarationSyntax)
            return model.GetDeclaredSymbol(node, cancellationToken);

        var declaration = node.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        var type = declaration is null ? null : model.GetDeclaredSymbol(declaration, cancellationToken);
        var name = QuotedName(warning);
        return name is null ? null : type?.GetMembers(name).FirstOrDefault();
    }

    /// <summary>A parameter whose nullability differs from the member it overrides or implements.</summary>
    private static ISymbol? MismatchedParameter(SyntaxNode node, Diagnostic warning, SemanticModel model, CancellationToken cancellationToken)
    {
        if (node is ParameterSyntax parameter)
            return model.GetDeclaredSymbol(parameter, cancellationToken);

        var method = node.AncestorsAndSelf().OfType<BaseMethodDeclarationSyntax>().FirstOrDefault();
        var name = QuotedName(warning);
        return method is null || name is null
            ? null
            : (model.GetDeclaredSymbol(method, cancellationToken) as IMethodSymbol)?.Parameters.FirstOrDefault(p => p.Name == name);
    }

    private static string? QuotedName(Diagnostic warning)
    {
        var match = Regex.Match(warning.GetMessage(System.Globalization.CultureInfo.InvariantCulture), "'([^']+)'");
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// The type syntax declaring a symbol in this file, when annotating it
    /// with <c>?</c> makes it nullable: a reference type or an unconstrained
    /// type parameter, written explicitly and not already annotated.
    /// </summary>
    private static TypeSyntax? DeclaredType(ISymbol symbol, SyntaxTree tree, CancellationToken cancellationToken)
    {
        var (type, declared) = symbol switch
        {
            IFieldSymbol field => (field.Type, Declaration(field)),
            IEventSymbol @event => (@event.Type, Declaration(@event)),
            ILocalSymbol local => (local.Type, Declaration(local)),
            IPropertySymbol property => (property.Type, Declaration(property)),
            IParameterSymbol parameter => (parameter.Type, Declaration(parameter)),
            IMethodSymbol method => (method.ReturnType, Declaration(method)),
            _ => (null, null),
        };

        if (type is null || declared is null || declared.SyntaxTree != tree
            || declared is IdentifierNameSyntax { IsVar: true } or NullableTypeSyntax)
            return null;

        return type.IsReferenceType || type is ITypeParameterSymbol { HasValueTypeConstraint: false }
            ? declared
            : null;

        TypeSyntax? Declaration(ISymbol declaredSymbol)
        {
            var syntax = declaredSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(cancellationToken);
            return syntax switch
            {
                VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration } => declaration.Type,
                PropertyDeclarationSyntax property => property.Type,
                IndexerDeclarationSyntax indexer => indexer.Type,
                ParameterSyntax parameter => parameter.Type,
                MethodDeclarationSyntax method => method.ReturnType,
                LocalFunctionStatementSyntax local => local.ReturnType,
                _ => null,
            };
        }
    }
}
