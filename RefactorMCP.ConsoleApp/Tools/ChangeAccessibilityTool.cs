using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

[McpServerToolType]
public static class ChangeAccessibilityTool
{
    private static readonly Dictionary<string, SyntaxKind[]> Accessibilities = new(StringComparer.Ordinal)
    {
        ["public"] = new[] { SyntaxKind.PublicKeyword },
        ["internal"] = new[] { SyntaxKind.InternalKeyword },
        ["protected"] = new[] { SyntaxKind.ProtectedKeyword },
        ["private"] = new[] { SyntaxKind.PrivateKeyword },
        ["protected internal"] = new[] { SyntaxKind.ProtectedKeyword, SyntaxKind.InternalKeyword },
        ["private protected"] = new[] { SyntaxKind.PrivateKeyword, SyntaxKind.ProtectedKeyword },
    };

    /// <summary>Compiler errors that name the reason a new accessibility does not fit.</summary>
    private static readonly Dictionary<string, string> Reasons = new(StringComparer.Ordinal)
    {
        ["CS0122"] = "it would be inaccessible where it is used",
        ["CS0507"] = "an override must keep the accessibility of the member it overrides",
        ["CS0737"] = "a member implementing an interface member implicitly must be public",
        ["CS0050"] = "it has inconsistent accessibility with a member that uses it",
        ["CS0051"] = "it has inconsistent accessibility with a member that uses it",
        ["CS0052"] = "it has inconsistent accessibility with a member that uses it",
        ["CS0053"] = "it has inconsistent accessibility with a member that uses it",
        ["CS0054"] = "it has inconsistent accessibility with a member that uses it",
        ["CS0060"] = "it has inconsistent accessibility with a member that uses it",
        ["CS0061"] = "it has inconsistent accessibility with a member that uses it",
        ["CS0106"] = "the accessibility is not valid for this declaration",
        ["CS1527"] = "the accessibility is not valid for this declaration",
    };

    [McpServerTool, Description("Change the accessibility of a type or member, on every partial declaration, when every reference and override still compiles and binds as before")]
    public static async Task<string> ChangeAccessibility(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file declaring the type or member")] string filePath,
        [Description("Name of the type or member, or of the type for a constructor")] string memberName,
        [Description("public, internal, protected, private, protected internal or private protected")] string accessibility,
        [Description("A line of the declaration (1-based), to choose between overloads")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Accessibilities.TryGetValue(accessibility.Trim(), out var keywords))
                throw new McpException(
                    $"Error: '{accessibility}' is not an accessibility; use {string.Join(", ", Accessibilities.Keys)}");

            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var symbol = await SolutionEdits.FindMemberAsync(solution, filePath, memberName, line, cancellationToken);

            var declarations = new List<MemberDeclarationSyntax>();
            foreach (var reference in symbol.DeclaringSyntaxReferences)
                declarations.Add(DeclarationOf(await reference.GetSyntaxAsync(cancellationToken), memberName));

            // Parts of a partial type that state no accessibility take the one the others state.
            var stated = declarations.Where(d => d.Modifiers.Any(IsAccessibility)).ToList();
            var targets = stated.Count > 0 ? stated : declarations.Take(1).ToList();

            var changed = solution;
            foreach (var group in targets.GroupBy(d => d.SyntaxTree))
            {
                var document = solution.GetDocument(group.Key)!;
                var root = await group.Key.GetRootAsync(cancellationToken);
                changed = changed.WithDocumentSyntaxRoot(
                    document.Id,
                    root.ReplaceNodes(group, (_, current) => WithAccessibility(current, keywords)));
            }

            await EnsureCompilesAsync(solution, changed, cancellationToken);
            await EnsureReferencesBindAsBeforeAsync(solution, changed, symbol, cancellationToken);
            await SolutionEdits.WriteAsync(solution, changed, cancellationToken);

            return $"Successfully made '{memberName}' {accessibility.Trim()}";
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error changing accessibility: {ex.Message}", ex);
        }
    }

    /// <summary>The declaration carrying the modifiers; a field must be the only one it declares.</summary>
    private static MemberDeclarationSyntax DeclarationOf(SyntaxNode node, string name) => node switch
    {
        VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax { Parent: BaseFieldDeclarationSyntax field } declaration } =>
            declaration.Variables.Count == 1
                ? field
                : throw new McpException(
                    $"Error: '{name}' is declared together with {string.Join(", ", declaration.Variables.Where(v => v != node).Select(v => $"'{v.Identifier.ValueText}'"))}; split the declaration first"),
        MemberDeclarationSyntax member => member,
        _ => throw new McpException($"Error: '{name}' has no declaration whose accessibility can change"),
    };

    private static bool IsAccessibility(SyntaxToken token) =>
        token.IsKind(SyntaxKind.PublicKeyword) || token.IsKind(SyntaxKind.InternalKeyword)
        || token.IsKind(SyntaxKind.ProtectedKeyword) || token.IsKind(SyntaxKind.PrivateKeyword);

    /// <summary>
    /// Replaces the accessibility keywords where they stood, keeping the other
    /// modifiers and the trivia around them. With no accessibility written,
    /// the new one goes first, taking the leading trivia of the declaration's
    /// first token after its attributes.
    /// </summary>
    private static MemberDeclarationSyntax WithAccessibility(MemberDeclarationSyntax member, SyntaxKind[] keywords)
    {
        var modifiers = member.Modifiers;
        var tokens = keywords.Select(k => SyntaxFactory.Token(k).WithTrailingTrivia(SyntaxFactory.Space)).ToList();

        var first = modifiers.IndexOf(modifiers.FirstOrDefault(IsAccessibility));
        if (first >= 0)
        {
            var last = modifiers.IndexOf(modifiers.Last(IsAccessibility));
            tokens[0] = tokens[0].WithLeadingTrivia(modifiers[first].LeadingTrivia);
            tokens[^1] = tokens[^1].WithTrailingTrivia(modifiers[last].TrailingTrivia);

            var kept = modifiers.Where(m => !IsAccessibility(m)).ToList();
            var position = modifiers.Take(first).Count(m => !IsAccessibility(m));
            kept.InsertRange(position, tokens);
            return member.WithModifiers(SyntaxFactory.TokenList(kept));
        }

        var start = member.AttributeLists.Count > 0
            ? member.AttributeLists.Last().GetLastToken().GetNextToken()
            : member.GetFirstToken();
        tokens[0] = tokens[0].WithLeadingTrivia(start.LeadingTrivia);
        var stripped = member.ReplaceToken(start, start.WithLeadingTrivia());
        return stripped.WithModifiers(SyntaxFactory.TokenList(tokens.Concat(stripped.Modifiers)));
    }

    private static async Task EnsureCompilesAsync(Solution solution, Solution changed, CancellationToken cancellationToken)
    {
        var errors = await SolutionEdits.NewDiagnosticsAsync(
            solution,
            changed,
            d => d.Severity == DiagnosticSeverity.Error,
            cancellationToken);
        if (errors.Count == 0)
            return;

        var explained = errors.FirstOrDefault(e => Reasons.ContainsKey(e.Id)) ?? errors[0];
        var reason = Reasons.TryGetValue(explained.Id, out var known) ? known : "the result would not compile";
        throw new McpException($"Error: The accessibility cannot change because {reason}: {SolutionEdits.Describe(explained)}");
    }

    /// <summary>
    /// Accessibility takes part in overload resolution, so a change can make
    /// a call quietly bind to another member, or make this one win calls it
    /// did not before. The member must be referenced exactly as often as it was.
    /// </summary>
    private static async Task EnsureReferencesBindAsBeforeAsync(
        Solution solution,
        Solution changed,
        ISymbol symbol,
        CancellationToken cancellationToken)
    {
        var project = SolutionEdits.ProjectOf(solution, symbol);
        var changedSymbol = await SolutionEdits.ResolveAsync(changed, project, symbol, cancellationToken);
        var before = await ReferenceCountAsync(symbol, solution, cancellationToken);
        var after = await ReferenceCountAsync(changedSymbol, changed, cancellationToken);
        if (before != after)
            throw new McpException(
                $"Error: '{symbol.Name}' would be referenced {after} time(s) instead of {before}, so some calls would bind to a different member");
    }

    private static async Task<int> ReferenceCountAsync(ISymbol symbol, Solution solution, CancellationToken cancellationToken)
    {
        var references = await SymbolFinder.FindReferencesAsync(symbol, solution, cancellationToken);
        return references
            .Where(r => SymbolEqualityComparer.Default.Equals(r.Definition.OriginalDefinition, symbol.OriginalDefinition))
            .SelectMany(r => r.Locations)
            .Count();
    }
}
