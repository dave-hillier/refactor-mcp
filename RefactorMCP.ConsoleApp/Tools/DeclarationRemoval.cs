using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Removes declarations and using directives the way a person would: a
/// declaration goes with the comments above it, the preprocessor directives
/// around it stay, and the blank lines between what remains are as they were.
/// </summary>
internal static class DeclarationRemoval
{
    /// <summary>
    /// Removes each member (a type member, a type or a namespace), then any
    /// namespace the removals leave empty.
    /// </summary>
    public static CompilationUnitSyntax RemoveMembers(CompilationUnitSyntax root, IReadOnlyCollection<MemberDeclarationSyntax> members)
    {
        var namespaces = members.SelectMany(m => m.Ancestors().OfType<BaseNamespaceDeclarationSyntax>()).Distinct().ToList();
        root = root.TrackNodes(members.Concat<SyntaxNode>(namespaces));
        foreach (var member in members)
        {
            if (root.GetCurrentNode(member) is { } current)
                root = RemoveMember(root, current);
        }

        // Innermost first, so an outer namespace is only checked once its inner ones are gone.
        foreach (var ns in namespaces.OrderByDescending(n => n.Ancestors().Count()))
        {
            if (root.GetCurrentNode(ns) is { Members.Count: 0, Usings.Count: 0, Externs.Count: 0 } emptied)
                root = RemoveMember(root, emptied);
        }

        return root;
    }

    /// <summary>
    /// Whether a file holds nothing that matters once its declarations are
    /// gone: no members, attributes or global usings. Its ordinary usings serve
    /// nothing.
    /// </summary>
    public static bool IsEmpty(CompilationUnitSyntax root) =>
        root.Members.Count == 0
        && root.AttributeLists.Count == 0
        && root.Usings.All(u => u.GlobalKeyword.IsKind(SyntaxKind.None));

    private static CompilationUnitSyntax RemoveMember(CompilationUnitSyntax root, MemberDeclarationSyntax member)
    {
        var next = member.GetLastToken().GetNextToken(includeZeroWidth: true);
        var removedLeading = member.GetLeadingTrivia();
        var directives = Directives(removedLeading);

        // What follows takes over the removed member's spacing: the blank line
        // that separated it, or none when it came first.
        var followedByMember = next.Parent?.AncestorsAndSelf().OfType<MemberDeclarationSyntax>()
            .Any(m => m.GetFirstToken() == next && m.Parent == member.Parent) == true;
        var leading = followedByMember
            ? LeadingBlankLines(removedLeading).Concat(directives).Concat(WithoutLeadingBlankLines(next.LeadingTrivia))
            : directives.Concat(next.LeadingTrivia);

        var marker = new SyntaxAnnotation();
        root = root.ReplaceNode(member, member.WithAdditionalAnnotations(marker));
        next = root.GetAnnotatedNodes(marker).Single().GetLastToken().GetNextToken(includeZeroWidth: true);
        root = root.ReplaceToken(next, next.WithLeadingTrivia(leading));
        return root.RemoveNode(root.GetAnnotatedNodes(marker).Single(), SyntaxRemoveOptions.KeepNoTrivia)!;
    }

    /// <summary>
    /// Removes using directives. A header comment on the first directive stays
    /// at the top, and when every directive of a file or namespace goes, the
    /// blank line that followed them goes too.
    /// </summary>
    public static SyntaxNode RemoveUsings(SyntaxNode root, IReadOnlyCollection<UsingDirectiveSyntax> directives)
    {
        var containers = directives.Select(d => d.Parent!).Distinct().ToList();
        root = root.TrackNodes(containers);
        foreach (var container in containers)
        {
            var removed = directives.Where(d => d.Parent == container).ToHashSet();
            var current = root.GetCurrentNode(container)!;
            root = root.ReplaceNode(current, WithoutUsings(container, removed));
        }

        return root;
    }

    private static SyntaxNode WithoutUsings(SyntaxNode container, IReadOnlySet<UsingDirectiveSyntax> removed)
    {
        var usings = Usings(container);
        var remaining = usings.Where(u => !removed.Contains(u)).ToList();
        if (remaining.Count == usings.Count)
            return container;

        var first = usings[0];
        var kept = removed.SelectMany(u => Directives(u.GetLeadingTrivia())).ToList();
        if (remaining.Count > 0)
        {
            if (!remaining.Contains(first))
                remaining[0] = remaining[0].WithLeadingTrivia(first.GetLeadingTrivia());
            return WithUsings(container, remaining);
        }

        // The header stays above whatever came after the directives, without
        // the blank line that separated them.
        var next = usings.Last().GetLastToken().GetNextToken(includeZeroWidth: true);
        var header = WithoutTrailingWhitespace(first.GetLeadingTrivia().Where(t => !t.IsDirective)).Concat(kept);
        var updated = container.ReplaceToken(next, next.WithLeadingTrivia(header.Concat(WithoutLeadingBlankLines(next.LeadingTrivia))));
        return WithUsings(updated, Array.Empty<UsingDirectiveSyntax>());
    }

    private static SyntaxList<UsingDirectiveSyntax> Usings(SyntaxNode container) => container switch
    {
        CompilationUnitSyntax unit => unit.Usings,
        BaseNamespaceDeclarationSyntax ns => ns.Usings,
        _ => throw new InvalidOperationException($"Using directives cannot appear in {container.Kind()}"),
    };

    private static SyntaxNode WithUsings(SyntaxNode container, IEnumerable<UsingDirectiveSyntax> usings) => container switch
    {
        CompilationUnitSyntax unit => unit.WithUsings(SyntaxFactory.List(usings)),
        BaseNamespaceDeclarationSyntax ns => ns.WithUsings(SyntaxFactory.List(usings)),
        _ => throw new InvalidOperationException($"Using directives cannot appear in {container.Kind()}"),
    };

    /// <summary>The preprocessor directives in a run of trivia, each with the indentation before it.</summary>
    private static List<SyntaxTrivia> Directives(SyntaxTriviaList trivia)
    {
        var kept = new List<SyntaxTrivia>();
        for (var i = 0; i < trivia.Count; i++)
        {
            if (!trivia[i].IsDirective && !trivia[i].IsKind(SyntaxKind.DisabledTextTrivia))
                continue;

            if (i > 0 && trivia[i - 1].IsKind(SyntaxKind.WhitespaceTrivia))
                kept.Add(trivia[i - 1]);
            kept.Add(trivia[i]);
        }

        return kept;
    }

    /// <summary>The blank lines a run of leading trivia starts with.</summary>
    private static IEnumerable<SyntaxTrivia> LeadingBlankLines(SyntaxTriviaList trivia)
    {
        var count = BlankLineTriviaCount(trivia);
        return trivia.Take(count);
    }

    private static IEnumerable<SyntaxTrivia> WithoutLeadingBlankLines(SyntaxTriviaList trivia) =>
        trivia.Skip(BlankLineTriviaCount(trivia));

    private static int BlankLineTriviaCount(SyntaxTriviaList trivia)
    {
        var count = 0;
        var i = 0;
        while (i < trivia.Count)
        {
            var line = trivia[i].IsKind(SyntaxKind.WhitespaceTrivia) ? 1 : 0;
            if (i + line < trivia.Count && trivia[i + line].IsKind(SyntaxKind.EndOfLineTrivia))
            {
                i += line + 1;
                count = i;
            }
            else
            {
                break;
            }
        }

        return count;
    }

    private static IEnumerable<SyntaxTrivia> WithoutTrailingWhitespace(IEnumerable<SyntaxTrivia> trivia)
    {
        var list = trivia.ToList();
        while (list.Count > 0 && list[^1].IsKind(SyntaxKind.WhitespaceTrivia))
            list.RemoveAt(list.Count - 1);
        return list;
    }
}
