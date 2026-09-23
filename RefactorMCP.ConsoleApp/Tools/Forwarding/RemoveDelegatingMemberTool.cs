using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using RefactorMCP.ConsoleApp.Tools.Moving;

namespace RefactorMCP.ConsoleApp.Tools.Forwarding;

[McpServerToolType]
public static class RemoveDelegatingMemberTool
{
    [McpServerTool, Description("Remove a member that only forwards, through base, to the inherited member it hides or overrides, " +
        "so its callers reach the inherited member. Uses of that member written with base only because of the removed one become plain uses. " +
        "Refuses, changing nothing, when the member does anything else, differs in signature, is more accessible, or is overridden.")]
    public static async Task<string> RemoveDelegatingMember(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file declaring the member")] string filePath,
        [Description("Name of the class declaring the member")] string className,
        [Description("Name of the member; 'this' for an indexer")] string memberName,
        [Description("A line of the member's declaration, to choose between overloads (optional)")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var document = MovingSupport.DocumentOrThrow(solution, filePath);
        var name = memberName == "this" ? WellKnownMemberNames.Indexer : memberName;
        var member = await MovingSupport.FindDeclaredSymbolAsync(
            document, name, line, s => s is IMethodSymbol or IPropertySymbol && s.ContainingType?.Name == className, "member", cancellationToken);

        var declaration = (MemberDeclarationSyntax)await member.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken);
        var model = (await solution.GetDocument(declaration.SyntaxTree)!.GetSemanticModelAsync(cancellationToken))!;
        var inherited = ForwardingMembers.Forwarded(declaration, model, e => e is BaseExpressionSyntax);
        if (inherited is null || inherited.Name != member.Name || inherited.Kind != member.Kind)
            throw new McpException($"Error: {Display(member)} does not only forward, through base, to the inherited member of the same name");
        if (!ForwardingMembers.SameSignature(member, inherited))
            throw new McpException($"Error: {Display(member)}'s signature differs from that of the inherited {Display(inherited)}, which its callers would then reach");
        if (!AtLeastAsAccessible(inherited, member))
            throw new McpException($"Error: The inherited {Display(inherited)} is less accessible than {Display(member)}, so some callers could not reach it");

        if (!member.IsOverride)
        {
            var overriding = (await SymbolFinder.FindOverridesAsync(member, solution, cancellationToken: cancellationToken)).FirstOrDefault();
            if (overriding is not null)
                throw new McpException($"Error: {Display(member)} is overridden by {overriding.ContainingType.Name}, which would be left overriding the inherited member instead");
        }

        var edits = new SyntaxEdits();
        edits.Replace(solution, declaration.Parent!, current =>
        {
            var type = (TypeDeclarationSyntax)current;
            return type.WithMembers(MemberLayout.Remove(type.Members, type.Members[((TypeDeclarationSyntax)declaration.Parent!).Members.IndexOf(declaration)]));
        });
        var removed = await edits.ApplyAsync(solution, cancellationToken);
        var updated = await WithoutNeedlessBaseAsync(solution, removed, member.ContainingType, inherited, cancellationToken);

        await SolutionEdits.EnsureCompilesAsync(solution, updated, cancellationToken);
        await MovingSupport.ApplyAsync(solution, updated, cancellationToken);
        return $"Successfully removed {Display(member)}; its callers reach the inherited {Display(inherited)}";
    }

    private static string Display(ISymbol member) =>
        $"{member.ContainingType.Name}.{(member is IPropertySymbol { IsIndexer: true } ? "this[]" : member.Name)}";

    private static bool AtLeastAsAccessible(ISymbol inherited, ISymbol member)
    {
        if (!AtLeastAsAccessible(inherited.DeclaredAccessibility, member.DeclaredAccessibility))
            return false;
        if (member is IPropertySymbol { SetMethod: { } setter })
            return inherited is IPropertySymbol { SetMethod: { } inheritedSetter }
                && AtLeastAsAccessible(inheritedSetter.DeclaredAccessibility, setter.DeclaredAccessibility);
        return true;
    }

    private static bool AtLeastAsAccessible(Accessibility first, Accessibility second) => first == second || (first, second) switch
    {
        (Accessibility.Public, _) => true,
        (Accessibility.ProtectedOrInternal, not Accessibility.Public) => true,
        (Accessibility.Protected or Accessibility.Internal, Accessibility.ProtectedAndInternal or Accessibility.Private) => true,
        (Accessibility.ProtectedAndInternal, Accessibility.Private) => true,
        _ => false,
    };

    /// <summary>
    /// Inside the class, <c>base.M</c> for the inherited member becomes
    /// <c>M</c> where that now binds to it and no override in the class or
    /// its subclasses would be reached instead.
    /// </summary>
    private static async Task<Solution> WithoutNeedlessBaseAsync(
        Solution original,
        Solution solution,
        INamedTypeSymbol type,
        ISymbol inherited,
        CancellationToken cancellationToken)
    {
        var project = SolutionEdits.ProjectOf(original, type);
        var resolvedType = await SolutionEdits.ResolveAsync(solution, project, type, cancellationToken);
        var meaning = inherited.OriginalDefinition.GetDocumentationCommentId();
        if (inherited.IsVirtual || inherited.IsAbstract || inherited.IsOverride)
        {
            var overrides = await SymbolFinder.FindOverridesAsync(inherited.OriginalDefinition, original, cancellationToken: cancellationToken);
            if (overrides.Any(o => o.ContainingType is { } owner && MemberReferences.InheritsFrom(owner, type)
                && !SymbolEqualityComparer.Default.Equals(owner, type)))
                return solution;
        }

        var edits = new SyntaxEdits();
        foreach (var reference in resolvedType.DeclaringSyntaxReferences)
        {
            var declaration = await reference.GetSyntaxAsync(cancellationToken);
            var document = solution.GetDocument(declaration.SyntaxTree)!;
            var model = (await document.GetSemanticModelAsync(cancellationToken))!;
            var callees = declaration.DescendantNodes().OfType<ExpressionSyntax>()
                .Where(e => e is MemberAccessExpressionSyntax { Expression: BaseExpressionSyntax } or ElementAccessExpressionSyntax { Expression: BaseExpressionSyntax });
            foreach (var callee in callees)
            {
                var bound = callee.Parent is InvocationExpressionSyntax call && call.Expression == callee ? (ExpressionSyntax)call : callee;
                if (model.GetSymbolInfo(bound, cancellationToken).Symbol?.OriginalDefinition.GetDocumentationCommentId() != meaning)
                    continue;

                if (ForwardingMembers.OnInstance(callee, model) is { } plain)
                    edits.Replace(document.Id, callee, current => plain.WithTriviaFrom(current));
            }
        }

        return await edits.ApplyAsync(solution, cancellationToken);
    }
}
