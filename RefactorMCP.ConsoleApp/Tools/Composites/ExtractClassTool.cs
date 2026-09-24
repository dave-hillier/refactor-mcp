using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using RefactorMCP.ConsoleApp.Tools.Moving;

namespace RefactorMCP.ConsoleApp.Tools.Composites;

[McpServerToolType]
public static class ExtractClassTool
{
    [McpServerTool, Description("Extract Class: create a new class, give the class a field holding an instance of it, " +
        "and move the named fields, properties and methods into it through that field. " +
        "Moved methods leave delegating stubs unless keepStubs is false. Refuses, changing nothing, if any step would.")]
    public static async Task<string> ExtractClass(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the class")] string filePath,
        [Description("Name of the class to extract from")] string className,
        [Description("Name of the new class")] string newClassName,
        [Description("Names of the fields, properties and methods to move; a method name moves every overload")] string[] memberNames,
        [Description("Name of the field that holds the new class (optional, defaults to _ and the class name in camel case)")] string? fieldName = null,
        [Description("File for the new class (optional, defaults to <newClassName>.cs beside the class)")] string? targetFilePath = null,
        [Description("Leave a delegating stub for each moved method (default true) instead of updating its callers")] bool keepStubs = true,
        CancellationToken cancellationToken = default)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var document = MovingSupport.DocumentOrThrow(solution, filePath);
        var type = (INamedTypeSymbol)await MovingSupport.FindDeclaredSymbolAsync(
            document, className, null, s => s is INamedTypeSymbol, "type", cancellationToken);
        if (type.TypeKind != TypeKind.Class)
            throw new McpException($"Error: {className} is not a class, so it cannot hold the extracted class in a field");

        var members = MembersToMove(type, memberNames);
        var field = fieldName ?? "_" + MovingSupport.CamelCase(newClassName).TrimStart('@');
        var path = targetFilePath ?? Path.Combine(Path.GetDirectoryName(document.FilePath!)!, newClassName + ".cs");
        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        var typeId = type.GetDocumentationCommentId()!;

        await CompositeRecipe.RunAsync(solutionPath, async recipe =>
        {
            await recipe.StepAsync("create-type", () =>
                CreateTypeTool.CreateType(solutionPath, path, newClassName, namespaceName: ns, cancellationToken: cancellationToken));

            var source = await recipe.FindAsync(typeId, cancellationToken);
            await recipe.StepAsync("introduce-field", () =>
                IntroduceFieldTool.IntroduceField(solutionPath, source.FilePath, source.NameRange(), field, "private", newClassName));

            foreach (var id in members)
                await MoveAsync(recipe, id, field, newClassName, keepStubs, cancellationToken);
        }, cancellationToken);

        return $"Successfully extracted {newClassName} from {className}";
    }

    /// <summary>
    /// The documentation ids of the members to move, fields and properties
    /// first so that methods moving after them find them already in the new
    /// class and use them there.
    /// </summary>
    private static List<string> MembersToMove(INamedTypeSymbol type, string[] memberNames)
    {
        if (memberNames.Length == 0)
            throw new McpException("Error: No members were named to move into the new class");

        var members = new List<ISymbol>();
        foreach (var name in memberNames.Distinct())
        {
            var declared = type.GetMembers(name).Where(m => !m.IsImplicitlyDeclared).ToList();
            if (declared.Count == 0)
                throw new McpException($"Error: {type.Name} has no member named '{name}'");

            var movable = declared.Where(IsMovable).ToList();
            if (movable.Count != declared.Count)
                throw new McpException($"Error: '{name}' is not a field, property or ordinary method, so it cannot move to the new class");

            members.AddRange(movable);
        }

        return members
            .OrderBy(m => m is IMethodSymbol ? 1 : 0)
            .Select(m => m.GetDocumentationCommentId()!)
            .ToList();
    }

    private static bool IsMovable(ISymbol member) => member switch
    {
        IFieldSymbol field => !field.IsImplicitlyDeclared,
        IPropertySymbol property => !property.IsIndexer,
        IMethodSymbol method => method.MethodKind == MethodKind.Ordinary,
        _ => false,
    };

    /// <summary>
    /// Moves one member: an instance member through the new field, a static
    /// one to the new class by name.
    /// </summary>
    private static async Task MoveAsync(
        CompositeRecipe recipe,
        string memberId,
        string via,
        string targetType,
        bool keepStub,
        CancellationToken cancellationToken)
    {
        var member = await recipe.FindAsync(memberId, cancellationToken);
        var symbol = member.Symbol;
        var refactoring = symbol switch
        {
            IMethodSymbol { IsStatic: true } => "move-static-method",
            IMethodSymbol => "move-instance-method",
            IFieldSymbol => "move-field",
            _ => "move-property",
        };

        await recipe.StepAsync(refactoring, () => MoveMemberTool.MoveMember(
            recipe.SolutionPath,
            member.FilePath,
            symbol.Name,
            via: symbol.IsStatic ? null : via,
            targetType: symbol.IsStatic ? targetType : null,
            keepStub: keepStub,
            line: member.Line,
            cancellationToken: cancellationToken));
    }
}
