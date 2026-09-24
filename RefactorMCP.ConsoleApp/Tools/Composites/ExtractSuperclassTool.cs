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
using RefactorMCP.ConsoleApp.Tools.Moving;

namespace RefactorMCP.ConsoleApp.Tools.Composites;

[McpServerToolType]
public static class ExtractSuperclassTool
{
    [McpServerTool, Description("Extract Superclass: create a new class between a class and its base class, " +
        "make the class derive from it, and pull the named fields and methods up into it. " +
        "Refuses, changing nothing, if any step would.")]
    public static async Task<string> ExtractSuperclass(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the class")] string filePath,
        [Description("Name of the class to extract from")] string className,
        [Description("Name of the new superclass")] string superclassName,
        [Description("Names of the fields and methods to pull up; a method name pulls up every overload (optional)")] string[]? memberNames = null,
        [Description("File for the new superclass (optional, defaults to <superclassName>.cs beside the class)")] string? targetFilePath = null,
        CancellationToken cancellationToken = default)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var document = MovingSupport.DocumentOrThrow(solution, filePath);
        var type = (INamedTypeSymbol)await MovingSupport.FindDeclaredSymbolAsync(
            document, className, null, s => s is INamedTypeSymbol, "type", cancellationToken);
        if (type.TypeKind != TypeKind.Class || type.IsStatic || type.IsRecord)
            throw new McpException($"Error: {className} is not a class that can derive from another, so it cannot be given a superclass");

        var members = MembersToPullUp(type, memberNames ?? Array.Empty<string>());
        var baseType = await BaseTypeAsWrittenAsync(solution, type, cancellationToken);
        var path = targetFilePath ?? Path.Combine(Path.GetDirectoryName(document.FilePath!)!, superclassName + ".cs");
        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        var typeId = type.GetDocumentationCommentId()!;

        await CompositeRecipe.RunAsync(solutionPath, async recipe =>
        {
            await recipe.StepAsync("create-type", () =>
                CreateTypeTool.CreateType(solutionPath, path, superclassName, namespaceName: ns, baseType: baseType, cancellationToken: cancellationToken));

            var subclass = await recipe.FindAsync(typeId, cancellationToken);
            await recipe.StepAsync("change-base-type", () =>
                ChangeBaseTypeTool.ChangeBaseType(solutionPath, subclass.FilePath, className, superclassName, cancellationToken));

            foreach (var id in members)
            {
                var member = await recipe.FindAsync(id, cancellationToken);
                if (member.Symbol is IFieldSymbol)
                {
                    await recipe.StepAsync("pull-up-field", () =>
                        PullUpTool.PullUpField(solutionPath, member.FilePath, className, member.Symbol.Name, cancellationToken));
                }
                else
                {
                    await recipe.StepAsync("pull-up-method", () =>
                        PullUpTool.PullUpMethod(solutionPath, member.FilePath, className, member.Symbol.Name, line: member.Line, cancellationToken: cancellationToken));
                }
            }
        }, cancellationToken);

        return $"Successfully extracted {superclassName} from {className}";
    }

    /// <summary>
    /// The documentation ids of the members to pull up, fields first so that
    /// methods using them find them in the superclass.
    /// </summary>
    private static List<string> MembersToPullUp(INamedTypeSymbol type, string[] memberNames)
    {
        var members = new List<ISymbol>();
        foreach (var name in memberNames.Distinct())
        {
            var declared = type.GetMembers(name).Where(m => !m.IsImplicitlyDeclared).ToList();
            if (declared.Count == 0)
                throw new McpException($"Error: {type.Name} has no member named '{name}'");
            if (!declared.All(m => m is IFieldSymbol or IMethodSymbol { MethodKind: MethodKind.Ordinary }))
                throw new McpException($"Error: '{name}' is not a field or ordinary method, so it cannot be pulled up");

            members.AddRange(declared);
        }

        return members
            .OrderBy(m => m is IMethodSymbol ? 1 : 0)
            .Select(m => m.GetDocumentationCommentId()!)
            .ToList();
    }

    /// <summary>
    /// The class's current base class as its declaration writes it, which the
    /// new superclass takes over; null when it derives from object.
    /// </summary>
    private static async Task<string?> BaseTypeAsWrittenAsync(Solution solution, INamedTypeSymbol type, CancellationToken cancellationToken)
    {
        if (type.BaseType is null || type.BaseType.SpecialType == SpecialType.System_Object)
            return null;

        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            var declaration = (TypeDeclarationSyntax)await reference.GetSyntaxAsync(cancellationToken);
            var model = await solution.GetDocument(declaration.SyntaxTree)!.GetSemanticModelAsync(cancellationToken);
            var written = declaration.BaseList?.Types
                .FirstOrDefault(t => SymbolEqualityComparer.Default.Equals(model!.GetTypeInfo(t.Type, cancellationToken).Type, type.BaseType));
            if (written is not null)
                return written.Type.ToString();
        }

        return type.BaseType.ToDisplayString();
    }
}
