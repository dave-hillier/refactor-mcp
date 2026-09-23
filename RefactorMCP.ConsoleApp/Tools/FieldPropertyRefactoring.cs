using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using System.IO;
using System.Linq;

/// <summary>
/// Shared lookups and edits for the field, property and constant refactorings:
/// finding a member by name in a document, recognising writes, preparing a
/// value to be inlined elsewhere, and writing a changed solution back to disk.
/// </summary>
internal static class FieldPropertyRefactoring
{
    internal static async Task<Document> GetDocumentAsync(string solutionPath, string filePath)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath);
        return RefactoringHelpers.GetDocumentByPath(solution, filePath)
            ?? throw new McpException($"Error: File {filePath} not found in solution");
    }

    internal static async Task<IFieldSymbol> FindFieldAsync(Document document, string fieldName)
    {
        return (await DeclaredSymbolsAsync(document, fieldName)).OfType<IFieldSymbol>().FirstOrDefault()
            ?? throw new McpException($"Error: No field named '{fieldName}' found");
    }

    internal static async Task<IPropertySymbol> FindPropertyAsync(Document document, string propertyName)
    {
        return (await DeclaredSymbolsAsync(document, propertyName)).OfType<IPropertySymbol>().FirstOrDefault()
            ?? throw new McpException($"Error: No property named '{propertyName}' found");
    }

    internal static async Task<IReadOnlyList<IMethodSymbol>> FindMethodsAsync(Document document, string methodName)
    {
        var methods = (await DeclaredSymbolsAsync(document, methodName)).OfType<IMethodSymbol>().ToList();
        return methods.Count > 0
            ? methods
            : throw new McpException($"Error: No method named '{methodName}' found");
    }

    private static async Task<IReadOnlyList<ISymbol>> DeclaredSymbolsAsync(Document document, string name)
    {
        var root = await document.GetSyntaxRootAsync();
        var model = await document.GetSemanticModelAsync();
        return root!.DescendantNodes()
            .Where(node => node is MemberDeclarationSyntax or VariableDeclaratorSyntax)
            .Select(node => model!.GetDeclaredSymbol(node))
            .Where(symbol => symbol is not null && symbol.Name == name)
            .Select(symbol => symbol!)
            .ToList();
    }

    /// <summary>The syntax that declares a symbol in source, such as its variable declarator or property.</summary>
    internal static async Task<TSyntax> DeclarationAsync<TSyntax>(ISymbol symbol) where TSyntax : SyntaxNode
    {
        var reference = symbol.DeclaringSyntaxReferences.First();
        return (TSyntax)await reference.GetSyntaxAsync();
    }

    internal static bool HasMemberNamed(INamedTypeSymbol type, string name) =>
        type.GetMembers(name).Any(member => !member.IsImplicitlyDeclared);

    /// <summary>
    /// The expression that names a reference, widened to include its
    /// qualifier: <c>this._x</c> or <c>order.Count</c> rather than the bare name.
    /// </summary>
    internal static ExpressionSyntax ReferenceExpression(SyntaxNode name)
    {
        var expression = (ExpressionSyntax)name;
        return expression.Parent switch
        {
            MemberAccessExpressionSyntax access when access.Name == expression => access,
            MemberBindingExpressionSyntax binding when binding.Name == expression => binding,
            _ => expression,
        };
    }

    /// <summary>True when the reference is assigned, incremented or passed by reference.</summary>
    internal static bool IsWrite(ExpressionSyntax reference)
    {
        var expression = ReferenceExpression(reference);
        return expression.Parent switch
        {
            AssignmentExpressionSyntax assignment => assignment.Left == expression,
            PrefixUnaryExpressionSyntax prefix => prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression),
            PostfixUnaryExpressionSyntax postfix => postfix.IsKind(SyntaxKind.PostIncrementExpression) || postfix.IsKind(SyntaxKind.PostDecrementExpression),
            ArgumentSyntax argument => !argument.RefKindKeyword.IsKind(SyntaxKind.None) && !argument.RefKindKeyword.IsKind(SyntaxKind.InKeyword),
            _ => false,
        };
    }

    /// <summary>True when the reference is passed as a <c>ref</c>, <c>out</c> or <c>in</c> argument.</summary>
    internal static bool IsPassedByReference(ExpressionSyntax reference) =>
        ReferenceExpression(reference).Parent is ArgumentSyntax argument && !argument.RefKindKeyword.IsKind(SyntaxKind.None);

    /// <summary>
    /// A copy of <paramref name="value"/> that means the same thing wherever it
    /// is placed: names are fully qualified, then marked for simplification at
    /// the destination, and the whole is parenthesised in case the destination
    /// binds more tightly.
    /// </summary>
    internal static ExpressionSyntax Inlinable(ExpressionSyntax value, SemanticModel model, Workspace workspace)
    {
        var expanded = (ExpressionSyntax)Simplifier.Expand(value, model, workspace);
        return SyntaxFactory.ParenthesizedExpression(expanded.WithoutTrivia())
            .WithAdditionalAnnotations(Simplifier.Annotation);
    }

    /// <summary>
    /// Formats the nodes marked with <see cref="Formatter.Annotation"/>,
    /// simplifies those marked with <see cref="Simplifier.Annotation"/>, and
    /// writes every changed document back to disk, keeping the session current.
    /// </summary>
    internal static async Task WriteChangesAsync(Solution original, Solution changed)
    {
        var documentIds = changed.GetChanges(original)
            .GetProjectChanges()
            .SelectMany(project => project.GetChangedDocuments())
            .ToList();

        var tidied = changed;
        foreach (var id in documentIds)
        {
            var document = tidied.GetDocument(id)!;
            document = await Simplifier.ReduceAsync(document, Simplifier.Annotation);
            document = await Formatter.FormatAsync(document, Formatter.Annotation);
            tidied = document.Project.Solution;
        }

        foreach (var id in documentIds)
        {
            var document = tidied.GetDocument(id)!;
            var text = await document.GetTextAsync();
            var encoding = await RefactoringHelpers.GetFileEncodingAsync(document.FilePath!);
            await File.WriteAllTextAsync(document.FilePath!, text.ToString(), encoding);
            RefactoringHelpers.UpdateSolutionCache(document);
        }
    }

    /// <summary>The whitespace a member or statement is indented by.</summary>
    internal static SyntaxTriviaList Indentation(SyntaxNode node)
    {
        var last = node.GetLeadingTrivia().LastOrDefault();
        return last.IsKind(SyntaxKind.WhitespaceTrivia)
            ? SyntaxFactory.TriviaList(last)
            : SyntaxFactory.TriviaList();
    }

    internal static SyntaxToken AccessibilityToken(string accessModifier) => accessModifier.ToLowerInvariant() switch
    {
        "public" => SyntaxFactory.Token(SyntaxKind.PublicKeyword),
        "protected" => SyntaxFactory.Token(SyntaxKind.ProtectedKeyword),
        "internal" => SyntaxFactory.Token(SyntaxKind.InternalKeyword),
        _ => SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
    };

    /// <summary>
    /// Adds a field to a type after its existing fields, or first when it has
    /// none, separated from the member that follows by a blank line.
    /// </summary>
    internal static TypeDeclarationSyntax AddField(TypeDeclarationSyntax type, FieldDeclarationSyntax field)
    {
        var newLine = NewLine(type);
        var lastField = type.Members.OfType<FieldDeclarationSyntax>().LastOrDefault();
        if (lastField is not null)
        {
            var placed = field
                .WithLeadingTrivia(Indentation(lastField))
                .WithTrailingTrivia(newLine)
                .WithAdditionalAnnotations(Formatter.Annotation);
            return type.WithMembers(type.Members.Insert(type.Members.IndexOf(lastField) + 1, placed));
        }

        var first = type.Members.FirstOrDefault();
        var leading = first is null ? SyntaxFactory.TriviaList() : Indentation(first);
        var trailing = first is null
            ? SyntaxFactory.TriviaList(newLine)
            : SyntaxFactory.TriviaList(newLine, newLine);
        var firstPlaced = field
            .WithLeadingTrivia(leading)
            .WithTrailingTrivia(trailing)
            .WithAdditionalAnnotations(Formatter.Annotation);
        return type.WithMembers(type.Members.Insert(0, firstPlaced));
    }

    /// <summary>The line ending a node already uses, so inserted lines match it.</summary>
    internal static SyntaxTrivia NewLine(SyntaxNode node)
    {
        var existing = node.DescendantTrivia(descendIntoTrivia: false).FirstOrDefault(t => t.IsKind(SyntaxKind.EndOfLineTrivia));
        return existing.IsKind(SyntaxKind.EndOfLineTrivia) ? existing : SyntaxFactory.EndOfLine(Environment.NewLine);
    }
}
