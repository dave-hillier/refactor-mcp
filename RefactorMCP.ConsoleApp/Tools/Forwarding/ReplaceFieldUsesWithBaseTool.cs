using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using RefactorMCP.ConsoleApp.Tools.Moving;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace RefactorMCP.ConsoleApp.Tools.Forwarding;

[McpServerToolType]
public static class ReplaceFieldUsesWithBaseTool
{
    [McpServerTool, Description("For a class holding a new instance of its base class in a private field, reach the field's members through the instance itself " +
        "(the member, this.member or base.member, whichever means the same) and remove the field. Refuses, changing nothing, when the field is assigned or used as a value, " +
        "the class overrides members of the base class, or code already uses the inherited members or converts the class to its base class.")]
    public static async Task<string> ReplaceFieldUsesWithBase(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
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
        var uses = await part.UsesAsync(cancellationToken);
        if (uses.Count > 0)
            throw part.InUse(uses[0]);
        await part.EnsureNotConvertedAsync(cancellationToken);

        var edits = new SyntaxEdits();
        foreach (var reference in await MemberReferences.FindAsync(solution, part.Field, cancellationToken))
        {
            var where = SolutionEdits.Describe(reference.Name.GetLocation());
            if (MemberReferences.IsWrittenTo(reference.Name))
                throw new McpException($"Error: {fieldName} is assigned at {where}, so it is not always the object its initializer creates");

            var field = reference.Receiver is ThisExpressionSyntax ? (ExpressionSyntax)reference.Name.Parent! : reference.Name;
            var callee = field.Parent switch
            {
                MemberAccessExpressionSyntax access when access.Expression == field => access,
                ElementAccessExpressionSyntax element when element.Expression == field => (ExpressionSyntax)element,
                _ => null,
            };
            if (callee is null || reference.Receiver is not (null or ThisExpressionSyntax))
                throw new McpException($"Error: {fieldName} is used other than through its members at {where}, where the instance itself would not be the same object");

            var replacement = ForwardingMembers.OnInstance(callee, reference.Model) ?? ForwardingMembers.Through(callee, BaseExpression());
            edits.Replace(reference.Document.Id, callee, current => replacement.WithTriviaFrom(current));
        }

        var declaration = part.Declaration;
        var holder = (TypeDeclarationSyntax)declaration.Parent!;
        var index = holder.Members.IndexOf(declaration);
        var variable = declaration.Declaration.Variables.First(v => v.Identifier.ValueText == fieldName);
        edits.Replace(solution, holder, current =>
        {
            var type = (TypeDeclarationSyntax)current;
            var fields = (FieldDeclarationSyntax)type.Members[index];
            return fields.Declaration.Variables.Count > 1
                ? type.ReplaceNode(fields, fields.WithDeclaration(fields.Declaration.WithVariables(
                    fields.Declaration.Variables.Remove(fields.Declaration.Variables.First(v => v.Identifier.ValueText == variable.Identifier.ValueText)))))
                : type.WithMembers(MemberLayout.Remove(type.Members, fields));
        });

        var updated = await edits.ApplyAsync(solution, cancellationToken);
        await SolutionEdits.EnsureCompilesAsync(solution, updated, cancellationToken);
        await MovingSupport.ApplyAsync(solution, updated, cancellationToken);
        return $"Successfully made {className} use its base class {part.Base.Name} in place of {fieldName}";
    }
}
