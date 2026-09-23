using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using RefactorMCP.ConsoleApp.Tools.Moving;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace RefactorMCP.ConsoleApp.Tools.Forwarding;

[McpServerToolType]
public static class ReplaceBaseUsesWithFieldTool
{
    [McpServerTool, Description("Make a class use a private field holding a new instance of its base class wherever it used the members it inherits, " +
        "so its base class part is no longer used. Refuses, changing nothing, when code elsewhere uses the inherited members or converts the class to its base class, " +
        "the class overrides or uses protected members of the base class, or the field is already used.")]
    public static async Task<string> ReplaceBaseUsesWithField(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file declaring the class")] string filePath,
        [Description("Name of the class")] string className,
        [Description("Name of the private field of the base class's type, initialised with a new instance")] string fieldName,
        CancellationToken cancellationToken = default)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var document = MovingSupport.DocumentOrThrow(solution, filePath);
        var type = (INamedTypeSymbol)await MovingSupport.FindDeclaredSymbolAsync(
            document, className, null, s => s is INamedTypeSymbol, "type", cancellationToken);

        var part = await InheritedPart.FindAsync(solution, type, fieldName, cancellationToken);
        var used = (await SymbolFinder.FindReferencesAsync(part.Field, solution, cancellationToken))
            .SelectMany(r => r.Locations)
            .FirstOrDefault(l => l.Location.IsInSource);
        if (used.Location is not null)
            throw new McpException($"Error: {fieldName} is already used at {SolutionEdits.Describe(used.Location)}, so it may not hold what the base class part holds");

        var edits = new SyntaxEdits();
        foreach (var use in await part.UsesAsync(cancellationToken))
        {
            if (!use.InsideOnThis)
                throw part.InUse(use);

            var compilation = use.Model.Compilation;
            if (!compilation.IsSymbolAccessibleWithin(use.Member, type, part.Base))
                throw new McpException($"Error: {className} uses the protected member {use.Member.Name} at {use.Where}, which the field cannot reach");

            var hidden = use.Model.LookupSymbols(use.Callee!.SpanStart, name: fieldName)
                .Any(s => s is ILocalSymbol or IParameterSymbol or IRangeVariableSymbol);
            var field = hidden
                ? MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, ThisExpression(), IdentifierName(fieldName))
                : (ExpressionSyntax)IdentifierName(fieldName);
            edits.Replace(use.Document.Id, use.Callee, current => ForwardingMembers.Through((ExpressionSyntax)current, field));
        }

        await part.EnsureNotConvertedAsync(cancellationToken);

        var updated = await edits.ApplyAsync(solution, cancellationToken);
        await SolutionEdits.EnsureCompilesAsync(solution, updated, cancellationToken);
        await MovingSupport.ApplyAsync(solution, updated, cancellationToken);
        return $"Successfully made {className} use {fieldName} in place of the members it inherits from {part.Base.Name}";
    }
}
