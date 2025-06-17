using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace RefactorMCP.ConsoleApp.SyntaxRewriters;

/// <summary>
/// Base rewriter that removes a declaration identified by name. For declarations
/// that contain multiple variables (fields or local variables), only the
/// matching variable is removed; otherwise the entire declaration node is
/// dropped.
/// </summary>
internal abstract class DeclarationRemovalRewriter<T> : CSharpSyntaxRewriter where T : SyntaxNode
{
    protected readonly string Name;

    protected DeclarationRemovalRewriter(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Determines whether the node represents the declaration we want to remove.
    /// </summary>
    protected abstract bool IsTarget(T node);

    /// <summary>
    /// Gets the list of variable declarators for the node, if applicable.
    /// Returns <c>null</c> when the declaration does not contain variables (e.g.,
    /// method declarations).
    /// </summary>
    protected virtual SeparatedSyntaxList<VariableDeclaratorSyntax>? GetDeclarators(T node) => null;

    /// <summary>
    /// Produces a new node with the specified declarators replaced.
    /// Only called when <see cref="GetDeclarators"/> returns a value.
    /// </summary>
    protected virtual T WithDeclarators(T node, SeparatedSyntaxList<VariableDeclaratorSyntax> declarators) => node;

    public override SyntaxNode? Visit(SyntaxNode? node)
    {
        if (node is T typed && IsTarget(typed))
        {
            var declarators = GetDeclarators(typed);
            if (declarators is null)
                return null; // declaration without variables

            var variable = declarators.Value.FirstOrDefault(v => v.Identifier.ValueText == Name);
            if (variable == null)
                return base.Visit(node);

            if (declarators.Value.Count == 1)
                return null;

            var newDecls = SyntaxFactory.SeparatedList(declarators.Value.Where(v => v != variable));
            return WithDeclarators(typed, newDecls);
        }

        return base.Visit(node);
    }
}
