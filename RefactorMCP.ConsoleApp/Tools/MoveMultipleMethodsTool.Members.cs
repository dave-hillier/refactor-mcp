using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using RefactorMCP.ConsoleApp.Tools.Composites;
using RefactorMCP.ConsoleApp.Tools.Moving;

public static partial class MoveMultipleMethodsTool
{
    [McpServerTool, Description("Move several methods of a class to another type, one Move Instance Method or Move Static Method at a time, " +
        "methods before the ones that call them. Instance methods move through a field, property or parameter (via, or the one of targetType); " +
        "static methods move to the target type. Refuses, changing nothing, if any move would.")]
    public static async Task<string> MoveMultipleMethods(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file declaring the class")] string filePath,
        [Description("Name of the class declaring the methods")] string className,
        [Description("Names of the methods to move; a name moves every overload")] string[] methodNames,
        [Description("The field or property to move instance methods through (optional when targetType is given)")] string? via = null,
        [Description("The type to move to: required when no via is given, and for static methods when the via's type is not meant")] string? targetType = null,
        [Description("Leave a delegating stub for each moved method (default true) instead of updating its callers")] bool keepStubs = true,
        CancellationToken cancellationToken = default)
    {
        if (methodNames.Length == 0)
            throw new McpException("Error: No method names provided");
        if (via is null && targetType is null)
            throw new McpException("Error: Name the member to move through (via) or the type to move to (targetType)");

        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var document = MovingSupport.DocumentOrThrow(solution, filePath);
        var type = (INamedTypeSymbol)await MovingSupport.FindDeclaredSymbolAsync(
            document, className, null, s => s is INamedTypeSymbol, "type", cancellationToken);

        var methods = new List<IMethodSymbol>();
        foreach (var name in methodNames.Distinct())
        {
            var overloads = type.GetMembers(name).OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary).ToList();
            if (overloads.Count == 0)
                throw new McpException($"Error: {className} has no method named '{name}'");
            methods.AddRange(overloads);
        }

        var staticTarget = targetType ?? ViaType(type, via!);
        var ordered = await CalleesFirstAsync(solution, methods, cancellationToken);

        await CompositeRecipe.RunAsync(solutionPath, async recipe =>
        {
            foreach (var id in ordered)
            {
                var method = await recipe.FindAsync(id, cancellationToken);
                var isStatic = method.Symbol.IsStatic;
                await recipe.StepAsync(isStatic ? "move-static-method" : "move-instance-method", () => MoveMemberTool.MoveMember(
                    solutionPath,
                    method.FilePath,
                    method.Symbol.Name,
                    via: isStatic ? null : via,
                    targetType: isStatic ? staticTarget : via is null ? targetType : null,
                    keepStub: keepStubs,
                    line: method.Line,
                    cancellationToken: cancellationToken));
            }
        }, cancellationToken);

        return $"Successfully moved {string.Join(", ", methodNames.Distinct())} from {className}";
    }

    /// <summary>The type of the field or property that instance methods move through, which static methods move to.</summary>
    private static string ViaType(INamedTypeSymbol type, string via)
    {
        var member = type.GetMembers(via).FirstOrDefault(m => m is IFieldSymbol or IPropertySymbol)
            ?? throw new McpException($"Error: {type.Name} has no field or property named '{via}' to move through");
        var viaType = member is IFieldSymbol field ? field.Type : ((IPropertySymbol)member).Type;
        return viaType.WithNullableAnnotation(NullableAnnotation.None).ToDisplayString();
    }

    /// <summary>
    /// The methods' documentation ids, each method after the ones it calls.
    /// Without stubs this is what lets a caller follow its callee: the
    /// callee's move rewrites the call to go through the via, which the
    /// caller's move then turns into a call on <c>this</c>. Methods calling
    /// each other in a cycle move in the order they were named, as far as the
    /// cycle allows.
    /// </summary>
    private static async Task<List<string>> CalleesFirstAsync(Solution solution, IReadOnlyList<IMethodSymbol> methods, CancellationToken cancellationToken)
    {
        var calls = new Dictionary<IMethodSymbol, HashSet<IMethodSymbol>>(SymbolEqualityComparer.Default);
        foreach (var method in methods)
        {
            var called = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            foreach (var reference in method.DeclaringSyntaxReferences)
            {
                var node = await reference.GetSyntaxAsync(cancellationToken);
                var model = (await solution.GetDocument(node.SyntaxTree)!.GetSemanticModelAsync(cancellationToken))!;
                foreach (var name in node.DescendantNodes().OfType<SimpleNameSyntax>())
                {
                    if (model.GetSymbolInfo(name, cancellationToken).Symbol is IMethodSymbol target
                        && !SymbolEqualityComparer.Default.Equals(target, method)
                        && methods.Contains(target, SymbolEqualityComparer.Default))
                        called.Add(target);
                }
            }

            calls[method] = called;
        }

        var ordered = new List<IMethodSymbol>();
        var visiting = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
        void Visit(IMethodSymbol method)
        {
            if (ordered.Contains(method, SymbolEqualityComparer.Default) || !visiting.Add(method))
                return;
            foreach (var callee in calls[method])
                Visit(callee);
            ordered.Add(method);
        }

        foreach (var method in methods)
            Visit(method);

        return ordered.Select(m => m.GetDocumentationCommentId()!).ToList();
    }
}
