using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Deleting a member a composite has redirected every use of.</summary>
internal static class MemberRemoval
{
    /// <summary>
    /// The root without the marked member and its comments. When it was the first
    /// member, the blank line that set the next member apart goes too.
    /// </summary>
    public static SyntaxNode Remove(SyntaxNode root, SyntaxAnnotation mark)
    {
        var member = (MemberDeclarationSyntax)root.GetAnnotatedNodes(mark).Single();
        var type = (TypeDeclarationSyntax)member.Parent!;
        if (type.Members.IndexOf(member) == 0 && type.Members.Count > 1 &&
            type.Members[1].GetLeadingTrivia().FirstOrDefault().IsKind(SyntaxKind.EndOfLineTrivia))
        {
            var next = type.Members[1];
            root = root.ReplaceNode(next, next.WithLeadingTrivia(next.GetLeadingTrivia().RemoveAt(0)));
            member = (MemberDeclarationSyntax)root.GetAnnotatedNodes(mark).Single();
        }

        return root.RemoveNode(member, SyntaxRemoveOptions.KeepNoTrivia)!;
    }
}
