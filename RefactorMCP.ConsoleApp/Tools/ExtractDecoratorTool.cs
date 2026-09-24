using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[McpServerToolType]
public static class ExtractDecoratorTool
{
    [McpServerTool, Description("Generate a decorator for an interface, or for the one interface a class implements: " +
        "a class implementing the interface that wraps an instance of it and forwards every member to it, in a new file")]
    public static async Task<string> ExtractDecorator(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the interface or class")] string filePath,
        [Description("Name of the interface to decorate, or of a class implementing exactly one interface")] string typeName,
        [Description("Name of the decorator class (optional; defaults to the interface name without its I, followed by Decorator)")] string? decoratorName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (solution, document) = await TypeRefactoringHelpers.LoadDocumentAsync(solutionPath, filePath, cancellationToken);
            var (type, _) = await TypeRefactoringHelpers.FindTypeAsync(document, typeName, cancellationToken);
            var @interface = InterfaceToDecorate(type);

            decoratorName ??= DefaultName(@interface);
            var path = InterfaceImplementation.PathFor(document, decoratorName);
            InterfaceImplementation.EnsureNameIsFree(type.ContainingNamespace, decoratorName, path);

            var inner = SyntaxFactory.IdentifierName("_inner");
            var decorator = InterfaceImplementation.WrapperClass(
                decoratorName,
                @interface,
                @interface,
                "_inner",
                "inner",
                TypeRefactoringHelpers.EndOfLine((await document.GetSyntaxRootAsync(cancellationToken))!),
                (member, isExplicit, part) => InterfaceImplementation.Forward(
                    isExplicit ? Cast(member.ContainingType, inner) : inner,
                    member.Name,
                    member,
                    part));

            var updated = await InterfaceImplementation.AddTypeFileAsync(document, type.ContainingNamespace, decorator, cancellationToken);
            await TypeRefactoringHelpers.ApplyIfCompilesAsync(
                solution,
                updated,
                errors => $"Error: The decorator {decoratorName} would not compile: {TypeRefactoringHelpers.Describe(errors)}",
                cancellationToken);

            return $"Created decorator {decoratorName} for {@interface.Name} in {path}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error extracting decorator: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// The interface itself, or the one interface a class or struct declares
    /// it implements.
    /// </summary>
    private static INamedTypeSymbol InterfaceToDecorate(INamedTypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Interface)
            return type;

        return type.Interfaces.Length switch
        {
            0 => throw new McpException($"Error: {type.Name} implements no interface, so there is nothing for a decorator to implement"),
            1 => type.Interfaces[0],
            _ => throw new McpException(
                $"Error: {type.Name} implements several interfaces ({string.Join(", ", type.Interfaces.Select(i => i.ToDisplayString()))}); decorate one of them by name"),
        };
    }

    /// <summary><c>IGreeter</c> gives <c>GreeterDecorator</c>.</summary>
    private static string DefaultName(INamedTypeSymbol @interface)
    {
        var name = @interface.Name;
        var stem = name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1]) ? name[1..] : name;
        return stem + "Decorator";
    }

    private static ExpressionSyntax Cast(INamedTypeSymbol type, ExpressionSyntax expression) =>
        SyntaxFactory.ParenthesizedExpression(SyntaxFactory.CastExpression(InterfaceImplementation.TypeFor(type), expression));
}
