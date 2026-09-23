using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactorMCP.ConsoleApp.Tools.Moving;

/// <summary>
/// Keeps blank lines and comments where a reader expects them when members
/// are taken out of, or put into, a type, namespace or file.
/// </summary>
internal static class MemberLayout
{
    /// <summary>The whole blank lines at the start of a trivia list.</summary>
    public static SyntaxTriviaList LeadingBlankLines(SyntaxTriviaList trivia)
    {
        var count = BlankLinePrefixLength(trivia);
        return SyntaxFactory.TriviaList(trivia.Take(count));
    }

    public static SyntaxTriviaList WithoutLeadingBlankLines(SyntaxTriviaList trivia) =>
        SyntaxFactory.TriviaList(trivia.Skip(BlankLinePrefixLength(trivia)));

    public static TNode WithoutLeadingBlankLines<TNode>(TNode node) where TNode : SyntaxNode =>
        node.WithLeadingTrivia(WithoutLeadingBlankLines(node.GetLeadingTrivia()));

    private static int BlankLinePrefixLength(SyntaxTriviaList trivia)
    {
        var prefix = 0;
        for (var i = 0; i < trivia.Count; i++)
        {
            if (trivia[i].IsKind(SyntaxKind.EndOfLineTrivia))
                prefix = i + 1;
            else if (!trivia[i].IsKind(SyntaxKind.WhitespaceTrivia))
                break;
        }

        return prefix;
    }

    /// <summary>
    /// Removes a member with the comments above it. When it was the first
    /// member, the next one takes its place, blank lines included, so a
    /// file-scoped namespace keeps its blank line and a block keeps none.
    /// </summary>
    public static SyntaxList<MemberDeclarationSyntax> Remove(
        SyntaxList<MemberDeclarationSyntax> members,
        MemberDeclarationSyntax member)
    {
        var index = members.IndexOf(member);
        var (directives, _) = SplitAtLastDirective(member.GetLeadingTrivia());
        if (index + 1 < members.Count)
        {
            var next = members[index + 1];
            var nextLeading = index == 0 && directives.Count == 0
                ? LeadingBlankLines(member.GetLeadingTrivia()).AddRange(WithoutLeadingBlankLines(next.GetLeadingTrivia()))
                : directives.AddRange(next.GetLeadingTrivia());
            members = members.Replace(next, next.WithLeadingTrivia(nextLeading));
        }

        return members.RemoveAt(index);
    }

    /// <summary>
    /// Keeps one member, laid out as the first member of its container.
    /// Region and other directives above it stay with the members they
    /// enclose, in the original file, unless they are the file's header.
    /// </summary>
    public static SyntaxList<MemberDeclarationSyntax> KeepOnly(
        SyntaxList<MemberDeclarationSyntax> members,
        MemberDeclarationSyntax member,
        bool keepDirectives = false)
    {
        var afterDirectives = keepDirectives
            ? member.GetLeadingTrivia()
            : SplitAtLastDirective(member.GetLeadingTrivia()).Following;
        var leading = LeadingBlankLines(members[0].GetLeadingTrivia())
            .AddRange(WithoutLeadingBlankLines(afterDirectives));
        return SyntaxFactory.SingletonList(member.WithLeadingTrivia(leading));
    }

    /// <summary>
    /// A closing token without the directives in front of it, which close
    /// regions opened around members that were not kept.
    /// </summary>
    public static SyntaxToken WithoutDirectives(SyntaxToken token)
    {
        var (directives, rest) = SplitAtLastDirective(token.LeadingTrivia);
        return directives.Count == 0 ? token : token.WithLeadingTrivia(rest);
    }

    /// <summary>
    /// Splits trivia after its last directive, so the directives and the
    /// lines before them can stay behind while what follows moves.
    /// </summary>
    private static (SyntaxTriviaList Directives, SyntaxTriviaList Following) SplitAtLastDirective(SyntaxTriviaList trivia)
    {
        var last = -1;
        for (var i = 0; i < trivia.Count; i++)
        {
            if (trivia[i].IsDirective)
                last = i;
        }

        return last < 0
            ? (SyntaxFactory.TriviaList(), trivia)
            : (SyntaxFactory.TriviaList(trivia.Take(last + 1)), SyntaxFactory.TriviaList(trivia.Skip(last + 1)));
    }

    /// <summary>
    /// Appends a member to a type, separated from the one before it by a
    /// blank line, and marks it for formatting.
    /// </summary>
    public static TypeDeclarationSyntax Append(TypeDeclarationSyntax type, MemberDeclarationSyntax member)
    {
        var leading = WithoutLeadingBlankLines(member.GetLeadingTrivia());
        if (type.Members.Count > 0)
            leading = leading.Insert(0, SyntaxFactory.EndOfLine("\n"));

        return type.AddMembers(member
            .WithLeadingTrivia(leading)
            .WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation));
    }

    /// <summary>Drops the blank lines a file would otherwise start with.</summary>
    public static CompilationUnitSyntax WithoutLeadingBlankLines(CompilationUnitSyntax root)
    {
        var first = root.GetFirstToken(includeZeroWidth: true);
        return root.ReplaceToken(first, first.WithLeadingTrivia(WithoutLeadingBlankLines(first.LeadingTrivia)));
    }

    /// <summary>
    /// The using directives in <paramref name="document"/> that the compiler
    /// reports as unnecessary.
    /// </summary>
    public static async Task<IReadOnlyList<UsingDirectiveSyntax>> UnnecessaryUsingsAsync(
        Document document,
        CancellationToken cancellationToken)
    {
        var model = await document.GetSemanticModelAsync(cancellationToken);
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        if (model is null || root is null)
            return new List<UsingDirectiveSyntax>();

        return model.GetDiagnostics(cancellationToken: cancellationToken)
            .Where(d => d.Id == "CS8019")
            .Select(d => root.FindNode(d.Location.SourceSpan))
            .Select(node => node.AncestorsAndSelf().OfType<UsingDirectiveSyntax>().FirstOrDefault())
            .OfType<UsingDirectiveSyntax>()
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Removes using directives. A file's header, the comments and directives
    /// above its first using, passes to whatever follows, and neither the
    /// file nor a namespace block is left starting with a blank line.
    /// </summary>
    public static CompilationUnitSyntax RemoveUsings(CompilationUnitSyntax root, IEnumerable<UsingDirectiveSyntax> usings)
    {
        var removed = usings.ToList();
        if (removed.Count == 0)
            return root;

        var mark = new SyntaxAnnotation();
        root = root.ReplaceNodes(removed, (original, _) => original.WithAdditionalAnnotations(mark));

        var namespaces = root.GetAnnotatedNodes(mark)
            .Select(node => node.Parent)
            .OfType<BaseNamespaceDeclarationSyntax>()
            .Distinct()
            .ToList();
        root = root.ReplaceNodes(namespaces, (original, _) =>
        {
            var remaining = original.Usings.Where(u => !u.HasAnnotation(mark)).ToList();
            var updated = original.WithUsings(SyntaxFactory.List(remaining));
            if (remaining.Count == 0 && updated.Members.Count > 0)
                updated = updated.WithMembers(updated.Members.Replace(updated.Members[0], WithoutLeadingBlankLines(updated.Members[0])));
            return updated;
        });

        var atFileLevel = root.Usings.Where(u => u.HasAnnotation(mark)).ToList();
        root = root.RemoveNodes(atFileLevel, SyntaxRemoveOptions.KeepLeadingTrivia | SyntaxRemoveOptions.KeepUnbalancedDirectives)!;
        return WithoutLeadingBlankLines(root);
    }
}
