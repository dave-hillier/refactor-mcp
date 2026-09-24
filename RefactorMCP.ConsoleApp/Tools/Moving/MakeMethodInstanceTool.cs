using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace RefactorMCP.ConsoleApp.Tools.Moving;

[McpServerToolType]
public static class MakeMethodInstanceTool
{
    [McpServerTool, Description("Make a static method that takes an instance of its own type into an instance " +
        "method on that parameter; every call becomes a call on the argument it passed")]
    public static async Task<string> MakeMethodInstance(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the method")] string filePath,
        [Description("Name of the static method")] string methodName,
        [Description("The parameter to become the instance (optional, defaults to the first of the method's own type)")] string? parameterName = null,
        [Description("Line of the method's declaration (1-based, optional), to choose between overloads")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var document = MovingSupport.DocumentOrThrow(solution, filePath);
        var method = (IMethodSymbol)await MovingSupport.FindDeclaredSymbolAsync(
            document, methodName, line, s => s is IMethodSymbol { MethodKind: MethodKind.Ordinary }, "method", cancellationToken);

        var updated = await MakeInstanceAsync(solution, method, parameterName, cancellationToken);
        await MovingSupport.ApplyAsync(solution, updated, cancellationToken);
        return $"Successfully made {methodName} an instance method";
    }

    private static async Task<Solution> MakeInstanceAsync(Solution solution, IMethodSymbol method, string? parameterName, CancellationToken cancellationToken)
    {
        var type = method.ContainingType;
        if (!method.IsStatic)
            throw new McpException($"Error: {method.Name} is not static");

        var parameter = method.Parameters.FirstOrDefault(p =>
            (parameterName is null || p.Name == parameterName)
            && SymbolEqualityComparer.Default.Equals(p.Type.OriginalDefinition, type.OriginalDefinition)
            && !(p.Ordinal == 0 && method.IsExtensionMethod));
        if (parameter is null)
            throw new McpException($"Error: {method.Name} has no parameter of its own type {type.Name}{(parameterName is null ? "" : $" named {parameterName}")} to become the instance");
        if (parameter.RefKind != RefKind.None)
            throw new McpException($"Error: {parameter.Name} is passed by reference, so it cannot become the instance");

        var declaration = (MethodDeclarationSyntax)await method.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken);
        var document = solution.GetDocument(declaration.SyntaxTree)!;
        var model = (await document.GetSemanticModelAsync(cancellationToken))!;
        var edits = new SyntaxEdits();

        foreach (var use in declaration.DescendantNodes().OfType<IdentifierNameSyntax>()
                     .Where(id => SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(id).Symbol, parameter)))
        {
            if (MemberReferences.IsWrittenTo(use))
                throw new McpException($"Error: {method.Name} assigns {parameter.Name}, and this cannot be reassigned");

            if (use.Parent is MemberAccessExpressionSyntax access && access.Expression == use && MemberReferences.CanDropQualifier(access, model, type))
                edits.Replace(document.Id, access, rewritten => ((MemberAccessExpressionSyntax)rewritten).Name.WithTriviaFrom(rewritten));
            else
                edits.Replace(document.Id, use, rewritten => SyntaxFactory.ThisExpression().WithTriviaFrom(rewritten));
        }

        edits.Replace(document.Id, declaration, rewritten =>
        {
            var updated = (MethodDeclarationSyntax)rewritten;
            var parameters = updated.ParameterList.Parameters;
            updated = updated.WithParameterList(updated.ParameterList.WithParameters(parameters.RemoveAt(parameter.Ordinal)));
            var staticKeyword = updated.Modifiers.First(m => m.IsKind(SyntaxKind.StaticKeyword));
            var index = updated.Modifiers.IndexOf(staticKeyword);
            var modifiers = updated.Modifiers.RemoveAt(index);
            if (index == 0 && modifiers.Count > 0)
                modifiers = modifiers.Replace(modifiers[0], modifiers[0].WithLeadingTrivia(staticKeyword.LeadingTrivia));
            else if (modifiers.Count == 0)
                return updated.WithModifiers(modifiers).WithLeadingTrivia(staticKeyword.LeadingTrivia);
            return updated.WithModifiers(modifiers);
        });

        foreach (var reference in await MemberReferences.FindAsync(solution, method, cancellationToken))
        {
            if (reference.Invocation is null)
                throw new McpException($"Error: {method.Name} is used as a method group in {Path.GetFileName(reference.Document.FilePath)}, whose signature would change");
            if (reference.Model.GetOperation(reference.Invocation, cancellationToken) is not IInvocationOperation operation)
                continue;

            var argument = operation.Arguments.FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.Parameter?.OriginalDefinition, parameter.OriginalDefinition));
            if (argument?.Syntax is not ArgumentSyntax argumentSyntax)
                throw new McpException($"Error: A call to {method.Name} does not pass {parameter.Name} explicitly");
            if (argumentSyntax.Expression.IsKind(SyntaxKind.NullLiteralExpression) || argumentSyntax.Expression.IsKind(SyntaxKind.DefaultLiteralExpression))
                throw new McpException($"Error: A call to {method.Name} in {Path.GetFileName(reference.Document.FilePath)} passes null for {parameter.Name}, which as the instance would throw");

            var index = reference.Invocation.ArgumentList.Arguments.IndexOf(argumentSyntax);
            var name = reference.Name.WithoutTrivia();
            edits.Replace(reference.Document.Id, reference.Invocation, rewritten =>
            {
                var call = (InvocationExpressionSyntax)rewritten;
                var receiver = call.ArgumentList.Arguments[index].Expression;
                ExpressionSyntax callee = receiver is ThisExpressionSyntax
                    ? name
                    : SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, ExtensionMethodConversions.Receiver(receiver), name);
                return call
                    .WithExpression(callee.WithTriviaFrom(call.Expression))
                    .WithArgumentList(call.ArgumentList.WithArguments(call.ArgumentList.Arguments.RemoveAt(index)));
            });
        }

        return await edits.ApplyAsync(solution, cancellationToken);
    }
}
