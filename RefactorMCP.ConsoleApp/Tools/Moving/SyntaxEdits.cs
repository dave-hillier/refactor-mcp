using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactorMCP.ConsoleApp.Tools.Moving;

/// <summary>
/// Replacements gathered across a solution from its original syntax trees,
/// applied in one pass per document. Each edit receives its node with the
/// edits inside it already made, so nested edits compose.
/// </summary>
internal sealed class SyntaxEdits
{
    private readonly Dictionary<DocumentId, Dictionary<SyntaxNode, Func<SyntaxNode, SyntaxNode>>> _edits = new();

    public void Replace(DocumentId document, SyntaxNode node, Func<SyntaxNode, SyntaxNode> edit)
    {
        if (!_edits.TryGetValue(document, out var edits))
            _edits[document] = edits = new Dictionary<SyntaxNode, Func<SyntaxNode, SyntaxNode>>();

        if (edits.TryGetValue(node, out var earlier))
            edits[node] = rewritten => edit(earlier(rewritten));
        else
            edits[node] = edit;
    }

    public void Replace(Solution solution, SyntaxNode node, Func<SyntaxNode, SyntaxNode> edit) =>
        Replace(solution.GetDocument(node.SyntaxTree)!.Id, node, edit);

    /// <summary>The solution with every edit made and the edited documents tidied.</summary>
    public async Task<Solution> ApplyAsync(Solution solution, CancellationToken cancellationToken)
    {
        var updated = solution;
        foreach (var (id, edits) in _edits)
        {
            var root = await solution.GetDocument(id)!.GetSyntaxRootAsync(cancellationToken);
            root = root!.ReplaceNodes(edits.Keys, (original, rewritten) => edits[original](rewritten));
            updated = updated.WithDocumentSyntaxRoot(id, root);
        }

        return await MovingSupport.TidyChangedDocumentsAsync(solution, updated, cancellationToken);
    }

    /// <summary>A separated list with <paramref name="first"/> in front, separated as the rest are.</summary>
    public static SeparatedSyntaxList<TNode> Prepend<TNode>(SeparatedSyntaxList<TNode> list, IReadOnlyList<TNode> first)
        where TNode : SyntaxNode
    {
        if (first.Count == 0)
            return list;

        var separators = Enumerable.Range(0, first.Count - (list.Count == 0 ? 1 : 0))
            .Select(_ => SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space))
            .Concat(list.GetSeparators());
        return SyntaxFactory.SeparatedList(first.Concat(list), separators);
    }
}
