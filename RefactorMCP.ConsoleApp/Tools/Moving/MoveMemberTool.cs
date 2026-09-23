using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Server;

namespace RefactorMCP.ConsoleApp.Tools.Moving;

[McpServerToolType]
public static class MoveMemberTool
{
    [McpServerTool, Description("Move a method, field or property to another type, updating its uses across the solution. " +
        "An instance member moves through a field, property or parameter of the target type (via), which becomes 'this' in the target; " +
        "members of the old type it still uses are reached through a parameter of that type. " +
        "An instance member can instead move into the class that holds its class in a field or property (into), its uses through that holder becoming direct. " +
        "A static member moves to the named target type, which is created as a static class if it does not exist. " +
        "A moved method can leave a delegating stub behind instead of its callers being updated.")]
    public static async Task<string> MoveMember(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file declaring the member")] string filePath,
        [Description("Name of the method, field or property to move")] string memberName,
        [Description("For an instance member: the field, property or parameter whose type to move the member to")] string? via = null,
        [Description("The type to move to: required for a static member; for an instance member, finds the one field, property or parameter of that type")] string? targetType = null,
        [Description("For a method: leave a stub that delegates to the moved method (default true) instead of updating callers")] bool keepStub = true,
        [Description("For a static member moving to a type that does not exist: the file to create it in (optional)")] string? targetFilePath = null,
        [Description("Line of the member's declaration (1-based, optional), to choose between overloads")] int? line = null,
        [Description("The kind of member expected, refusing any other: instance-method, static-method, field or property (optional)")] string? kind = null,
        [Description("For an instance member, instead of via: the class holding the member's class in its one field or property of that type; the member moves into it and uses through that holder become direct")] string? into = null,
        CancellationToken cancellationToken = default)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var document = MovingSupport.DocumentOrThrow(solution, filePath);
        var member = await MovingSupport.FindDeclaredSymbolAsync(
            document,
            memberName,
            line,
            s => s is IMethodSymbol { MethodKind: MethodKind.Ordinary } or IFieldSymbol or IPropertySymbol,
            "method, field or property",
            cancellationToken);

        var updated = await MemberMover.MoveAsync(solution, member, via, targetType, keepStub, targetFilePath, kind, into, cancellationToken);
        await MovingSupport.ApplyAsync(solution, updated, cancellationToken);
        return $"Successfully moved {member.ContainingType.Name}.{memberName}";
    }
}
