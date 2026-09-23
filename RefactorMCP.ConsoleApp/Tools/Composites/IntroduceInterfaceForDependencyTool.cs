using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace RefactorMCP.ConsoleApp.Tools.Composites;

[McpServerToolType]
public static class IntroduceInterfaceForDependencyTool
{
    [McpServerTool, Description("Introduce Interface for Dependency: extract an interface from the class a field, property or parameter holds, " +
        "and declare that dependency as the interface. For a field or property, constructor parameters assigned to it change too. " +
        "Refuses, changing nothing, if any step would.")]
    public static async Task<string> IntroduceInterfaceForDependency(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file declaring the dependency")] string filePath,
        [Description("Name of the field or property; for a parameter, the name of its method, or of its class for a constructor")] string name,
        [Description("Name of the interface to extract")] string interfaceName,
        [Description("Name of the parameter of method 'name' that holds the dependency (optional)")] string? parameterName = null,
        [Description("Names of the members the interface declares (optional, defaults to every public instance member)")] string[]? memberNames = null,
        [Description("File for the interface (optional, defaults to <interfaceName>.cs beside the class)")] string? interfaceFilePath = null,
        [Description("Line of the declaration, to tell same-named declarations apart (optional)")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var declared = await SolutionEdits.FindMemberAsync(solution, filePath, name, line, cancellationToken);
        var dependency = parameterName is null
            ? declared
            : SolutionEdits.FindParameter(declared as IMethodSymbol
                ?? throw new McpException($"Error: {name} is not a method, so it has no parameter {parameterName}"), parameterName);

        var type = DeclaredType(dependency);
        if (type is not INamedTypeSymbol { TypeKind: TypeKind.Class } implementation || !implementation.Locations.Any(l => l.IsInSource))
            throw new McpException($"Error: {dependency.Name} is of type {type.ToDisplayString()}, which is not a class declared in the solution, so no interface can be extracted from it");

        var classLocation = implementation.Locations.First(l => l.IsInSource);
        var classFile = classLocation.SourceTree!.FilePath;
        var path = interfaceFilePath ?? Path.Combine(Path.GetDirectoryName(classFile)!, interfaceName + ".cs");
        var targets = new List<string> { Id(dependency) };
        if (dependency is IFieldSymbol or IPropertySymbol)
            targets.AddRange((await ConstructorParametersAssignedToAsync(dependency, cancellationToken)).Select(Id));

        await CompositeRecipe.RunAsync(solutionPath, async recipe =>
        {
            await recipe.StepAsync("extract-interface", () => ExtractInterfaceTool.ExtractInterface(
                solutionPath,
                classFile,
                implementation.Name,
                string.Join(",", memberNames ?? Array.Empty<string>()),
                path,
                interfaceName,
                cancellationToken));

            foreach (var target in targets)
                await ChangeTypeAsync(recipe, target, interfaceName, cancellationToken);
        }, cancellationToken);

        return $"Successfully introduced {interfaceName} for {dependency.Name}";
    }

    private static ITypeSymbol DeclaredType(ISymbol symbol) => symbol switch
    {
        IFieldSymbol field => field.Type,
        IPropertySymbol property => property.Type,
        IParameterSymbol parameter => parameter.Type,
        _ => throw new McpException($"Error: {symbol.Name} is not a field, property or parameter"),
    };

    /// <summary>
    /// A parameter is identified by its method's documentation id and its
    /// name, since parameters have no id of their own.
    /// </summary>
    private static string Id(ISymbol symbol) => symbol is IParameterSymbol parameter
        ? $"{parameter.ContainingSymbol.GetDocumentationCommentId()}|{parameter.Name}"
        : symbol.GetDocumentationCommentId()!;

    /// <summary>
    /// Constructor parameters of the member's type that the constructor stores
    /// straight into it (<c>_writer = writer;</c>), which are how the
    /// dependency arrives and so change with it.
    /// </summary>
    private static async Task<IReadOnlyList<IParameterSymbol>> ConstructorParametersAssignedToAsync(ISymbol member, CancellationToken cancellationToken)
    {
        var memberType = DeclaredType(member);
        var parameters = new List<IParameterSymbol>();
        foreach (var constructor in member.ContainingType.InstanceConstructors.Where(c => !c.IsImplicitlyDeclared))
        {
            foreach (var reference in constructor.DeclaringSyntaxReferences)
            {
                var declaration = (ConstructorDeclarationSyntax)await reference.GetSyntaxAsync(cancellationToken);
                var assigned = declaration.DescendantNodes()
                    .OfType<AssignmentExpressionSyntax>()
                    .Where(a => AssignedName(a.Left) == member.Name && a.Right is IdentifierNameSyntax)
                    .Select(a => ((IdentifierNameSyntax)a.Right).Identifier.ValueText);
                parameters.AddRange(constructor.Parameters.Where(p =>
                    assigned.Contains(p.Name) && SymbolEqualityComparer.Default.Equals(p.Type.WithNullableAnnotation(NullableAnnotation.None), memberType.WithNullableAnnotation(NullableAnnotation.None))));
            }
        }

        return parameters;
    }

    private static string? AssignedName(ExpressionSyntax left) => left switch
    {
        IdentifierNameSyntax name => name.Identifier.ValueText,
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } access => access.Name.Identifier.ValueText,
        _ => null,
    };

    private static async Task ChangeTypeAsync(CompositeRecipe recipe, string target, string interfaceName, CancellationToken cancellationToken)
    {
        var parts = target.Split('|');
        var declaration = await recipe.FindAsync(parts[0], cancellationToken);
        var span = declaration.Location.GetLineSpan().StartLinePosition;
        await recipe.StepAsync("change-type", () => ChangeTypeTool.ChangeType(
            recipe.SolutionPath,
            declaration.FilePath,
            declaration.Symbol.Name,
            interfaceName,
            parts.Length > 1 ? parts[1] : null,
            span.Line + 1,
            span.Character + 1,
            cancellationToken));
    }
}
