using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;

[McpServerToolType]
public static class InitializeFieldFromConstructorParameterTool
{
    [McpServerTool, Description("Assign a field that nothing uses yet from a constructor parameter, at the end of the constructor")]
    public static async Task<string> InitializeFieldFromConstructorParameter(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the constructor")] string filePath,
        [Description("Name of the class whose constructor assigns the field")] string className,
        [Description("Name of the field to assign")] string fieldName,
        [Description("Name of the constructor parameter to assign it from")] string parameterName,
        [Description("A line of the constructor's declaration (1-based), to choose between constructors")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var constructor = await SolutionEdits.FindMethodAsync(solution, filePath, className, line, cancellationToken);
            if (constructor is not { MethodKind: MethodKind.Constructor, IsStatic: false }
                || constructor.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(cancellationToken) is not ConstructorDeclarationSyntax declaration
                || (declaration.Body == null && declaration.ExpressionBody == null))
                throw new McpException($"Error: '{className}' at the given line is not an instance constructor with a body");

            var parameter = SolutionEdits.FindParameter(constructor, parameterName);
            var type = constructor.ContainingType;
            var field = type.GetMembers(fieldName).OfType<IFieldSymbol>().FirstOrDefault(f => !f.IsStatic && !f.IsConst)
                ?? throw new McpException($"Error: '{type.Name}' has no instance field named '{fieldName}'");

            var document = solution.GetDocument(declaration.SyntaxTree)!;
            var compilation = (await document.Project.GetCompilationAsync(cancellationToken))!;
            if (!compilation.ClassifyConversion(parameter.Type, field.Type).IsImplicit)
                throw new McpException($"Error: '{fieldName}' of type '{field.Type.ToDisplayString()}' cannot hold '{parameterName}' of type '{parameter.Type.ToDisplayString()}'");

            // Only a field nothing reads or writes can take a value without changing what the code does.
            var initialized = field.DeclaringSyntaxReferences.Select(r => r.GetSyntax(cancellationToken)).OfType<VariableDeclaratorSyntax>().Any(v => v.Initializer != null);
            var references = await SymbolFinder.FindReferencesAsync(field, solution, cancellationToken);
            if (initialized || references.SelectMany(r => r.Locations).Any())
                throw new McpException($"Error: '{fieldName}' is already used, so assigning it could change what the code does");

            if (declaration.Body?.DescendantNodes(n => n is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax)
                    .OfType<ReturnStatementSyntax>().Any() == true)
                throw new McpException("Error: The constructor returns before its end, where the assignment would not run");

            var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
            document = document.WithSyntaxRoot(root.ReplaceNode(declaration, WithAssignment(declaration, field.Name, parameter.Name)));
            document = await Simplifier.ReduceAsync(document, Simplifier.Annotation, cancellationToken: cancellationToken);
            document = await Formatter.FormatAsync(document, Formatter.Annotation, cancellationToken: cancellationToken);

            var changed = document.Project.Solution;
            await SolutionEdits.EnsureCompilesAsync(solution, changed, cancellationToken);
            await SolutionEdits.WriteAsync(solution, changed, cancellationToken);
            return $"Successfully assigned '{fieldName}' from '{parameterName}' in the constructor of '{type.Name}'";
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error initializing field from constructor parameter: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// The constructor ending with <c>this.field = parameter;</c>, the <c>this.</c> left for
    /// the simplifier to drop where nothing hides the field. An expression body becomes a
    /// block holding its expression first.
    /// </summary>
    private static ConstructorDeclarationSyntax WithAssignment(ConstructorDeclarationSyntax constructor, string field, string parameter)
    {
        var assignment = SyntaxFactory.ExpressionStatement(SyntaxFactory.AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.ThisExpression(),
                        SyntaxFactory.IdentifierName(field))
                    .WithAdditionalAnnotations(Simplifier.Annotation),
                SyntaxFactory.IdentifierName(parameter)))
            .WithAdditionalAnnotations(Formatter.Annotation);

        if (constructor.Body != null)
            return constructor.WithBody(constructor.Body.AddStatements(assignment));

        var body = SyntaxFactory.Block(SyntaxFactory.ExpressionStatement(constructor.ExpressionBody!.Expression.WithoutTrivia()), assignment)
            .WithTrailingTrivia(constructor.SemicolonToken.TrailingTrivia);
        return constructor
            .WithParameterList(constructor.ParameterList.WithoutTrailingTrivia())
            .WithExpressionBody(null)
            .WithSemicolonToken(default)
            .WithBody(body)
            .WithAdditionalAnnotations(Formatter.Annotation);
    }
}
