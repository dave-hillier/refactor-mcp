using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Text;
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

    /// <summary>
    /// The expression a selection covers exactly, ignoring whitespace at
    /// either end, or null when it covers anything else.
    /// </summary>
    internal static ExpressionSyntax? SelectedExpression(SyntaxNode root, SourceText text, TextSpan selection)
    {
        var start = selection.Start;
        var end = selection.End;
        while (start < end && char.IsWhiteSpace(text[start]))
            start++;
        while (end > start && char.IsWhiteSpace(text[end - 1]))
            end--;

        var span = TextSpan.FromBounds(start, end);
        return root.FindNode(span, getInnermostNodeForTie: true)
            .AncestorsAndSelf()
            .TakeWhile(n => n.Span == span)
            .OfType<ExpressionSyntax>()
            .FirstOrDefault();
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

    /// <summary>
    /// Replaces every reference to a field with its initialiser, qualified and
    /// parenthesised as each use needs, then removes the field.
    /// </summary>
    internal static async Task<Solution> InlineFieldValueAsync(Solution solution, IFieldSymbol field, IEnumerable<ReferencedSymbol> references)
    {
        var variable = await DeclarationAsync<VariableDeclaratorSyntax>(field);
        var declaringDocument = solution.GetDocument(variable.SyntaxTree)!;
        var declaringModel = (await declaringDocument.GetSemanticModelAsync())!;
        var value = Inlinable(variable.Initializer!.Value, declaringModel, solution.Workspace);

        var locations = references.SelectMany(r => r.Locations).GroupBy(l => l.Document.Id);
        var changed = solution;
        foreach (var group in locations)
        {
            var editor = await DocumentEditor.CreateAsync(changed.GetDocument(group.Key)!);
            foreach (var location in group)
            {
                var name = editor.OriginalRoot.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true);
                if (IsInNameOf(name))
                    throw new McpException($"Error: '{field.Name}' is named by nameof at {location.Location.GetLineSpan()}, which needs a symbol rather than a value");

                var reference = ReferenceExpression(name);
                editor.ReplaceNode(reference, value.WithTriviaFrom(reference));
            }
            changed = editor.GetChangedDocument().Project.Solution;
        }

        return await RemoveFieldAsync(changed, field, declaringDocument.Id);
    }

    /// <summary>
    /// Removes a field from the document that declares it, after other edits
    /// may have moved it, by finding it by name in its type.
    /// </summary>
    internal static async Task<Solution> RemoveFieldAsync(Solution solution, IFieldSymbol field, DocumentId declaringDocument)
    {
        var document = solution.GetDocument(declaringDocument)!;
        var root = (await document.GetSyntaxRootAsync())!;
        var type = root.DescendantNodes().OfType<TypeDeclarationSyntax>()
            .First(t => t.Identifier.ValueText == field.ContainingType.Name && FieldVariables(t).Any(v => v.Identifier.ValueText == field.Name));
        return document.WithSyntaxRoot(root.ReplaceNode(type, RemoveField(type, field.Name))).Project.Solution;
    }

    /// <summary>
    /// Accessors that read and write a backing field, one to a line:
    /// <c>get => _field;</c> and <c>set => _field = value;</c>, each keeping
    /// the modifiers given for it.
    /// </summary>
    internal static AccessorListSyntax BackingAccessors(string fieldName, IEnumerable<(SyntaxKind Kind, string Modifiers)> accessors, SyntaxTrivia newLine)
    {
        var nl = newLine.ToString();
        var lines = accessors.Select(accessor => accessor.Kind switch
        {
            SyntaxKind.GetAccessorDeclaration => $"{accessor.Modifiers}get => {fieldName};",
            SyntaxKind.InitAccessorDeclaration => $"{accessor.Modifiers}init => {fieldName} = value;",
            _ => $"{accessor.Modifiers}set => {fieldName} = value;",
        });
        var property = (PropertyDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(
            $"int P{nl}{{{nl}{string.Join(nl, lines)}{nl}}}")!;
        return property.AccessorList!
            .WithLeadingTrivia(newLine)
            .WithAdditionalAnnotations(Formatter.Annotation);
    }

    /// <summary>The modifiers of an accessor as source text, followed by a space when there are any.</summary>
    internal static string AccessorModifiers(AccessorDeclarationSyntax accessor) =>
        accessor.Modifiers.Count == 0 ? "" : accessor.Modifiers.ToString() + " ";

    /// <summary>True when the node lies inside a declaration of the type, including nested types and other partial parts.</summary>
    internal static bool IsInsideType(SyntaxNode node, SemanticModel model, INamedTypeSymbol type) =>
        node.Ancestors().OfType<BaseTypeDeclarationSyntax>()
            .Any(t => SymbolEqualityComparer.Default.Equals(model.GetDeclaredSymbol(t), type));

    /// <summary><c>_title</c> and <c>m_title</c> become <c>Title</c>; <c>title</c> becomes <c>Title</c>.</summary>
    internal static string PropertyNameFor(string fieldName)
    {
        var name = fieldName.StartsWith("m_", StringComparison.Ordinal) ? fieldName[2..] : fieldName.TrimStart('_');
        return name.Length == 0 ? fieldName : char.ToUpperInvariant(name[0]) + name[1..];
    }

    /// <summary><c>Title</c> becomes <c>_title</c>.</summary>
    internal static string FieldNameFor(string propertyName) =>
        "_" + char.ToLowerInvariant(propertyName[0]) + propertyName[1..];

    internal static bool IsInNameOf(SyntaxNode node) =>
        node.Ancestors().OfType<InvocationExpressionSyntax>()
            .Any(invocation => invocation.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" });

    private static IEnumerable<VariableDeclaratorSyntax> FieldVariables(TypeDeclarationSyntax type) =>
        type.Members.OfType<FieldDeclarationSyntax>().SelectMany(f => f.Declaration.Variables);

    /// <summary>
    /// Removes a field from a type. A field declared alongside others leaves
    /// the declaration with the rest; a field declared alone takes its
    /// comments with it, and the blank lines around it close up as if it had
    /// never been there.
    /// </summary>
    internal static TypeDeclarationSyntax RemoveField(TypeDeclarationSyntax type, string fieldName)
    {
        var variable = FieldVariables(type).First(v => v.Identifier.ValueText == fieldName);
        var declaration = (FieldDeclarationSyntax)variable.Parent!.Parent!;
        if (declaration.Declaration.Variables.Count > 1)
        {
            var remaining = declaration.Declaration.WithVariables(declaration.Declaration.Variables.Remove(variable));
            return type.ReplaceNode(declaration, declaration.WithDeclaration(remaining));
        }

        return RemoveMember(type, declaration);
    }

    /// <summary>
    /// Removes a member with its leading comments. The member that follows
    /// keeps a blank line above it when the removed member had one, or when
    /// it had one itself and does not become the first member.
    /// </summary>
    internal static TypeDeclarationSyntax RemoveMember(TypeDeclarationSyntax type, MemberDeclarationSyntax member)
    {
        var index = type.Members.IndexOf(member);
        var members = type.Members.RemoveAt(index);
        if (index < members.Count)
        {
            var next = members[index];
            var blank = StartsWithBlankLine(member) || (StartsWithBlankLine(next) && index > 0);
            var leading = next.GetLeadingTrivia().SkipWhile(t => t.IsKind(SyntaxKind.WhitespaceTrivia) || t.IsKind(SyntaxKind.EndOfLineTrivia));
            var kept = next.GetLeadingTrivia().TakeWhile(t => t.IsKind(SyntaxKind.WhitespaceTrivia) || t.IsKind(SyntaxKind.EndOfLineTrivia)).LastOrDefault(t => t.IsKind(SyntaxKind.WhitespaceTrivia));
            var trivia = new List<SyntaxTrivia>();
            if (blank)
                trivia.Add(NewLine(type));
            if (kept.IsKind(SyntaxKind.WhitespaceTrivia))
                trivia.Add(kept);
            trivia.AddRange(leading);
            members = members.Replace(next, next.WithLeadingTrivia(trivia));
        }

        return type.WithMembers(members);
    }

    private static bool StartsWithBlankLine(SyntaxNode node) =>
        node.GetLeadingTrivia().SkipWhile(t => t.IsKind(SyntaxKind.WhitespaceTrivia)).FirstOrDefault().IsKind(SyntaxKind.EndOfLineTrivia);

    /// <summary>The line ending a node already uses, so inserted lines match it.</summary>
    internal static SyntaxTrivia NewLine(SyntaxNode node)
    {
        var existing = node.DescendantTrivia(descendIntoTrivia: false).FirstOrDefault(t => t.IsKind(SyntaxKind.EndOfLineTrivia));
        return existing.IsKind(SyntaxKind.EndOfLineTrivia) ? existing : SyntaxFactory.EndOfLine(Environment.NewLine);
    }
}
