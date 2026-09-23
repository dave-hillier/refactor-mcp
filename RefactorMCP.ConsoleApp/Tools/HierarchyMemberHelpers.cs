using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;

/// <summary>
/// What pulling members up and pushing them down share: finding the member and
/// the class it moves to, translating type parameters between a class and its
/// base, and taking members out of one type and into another without
/// disturbing the layout around them.
/// </summary>
internal static class HierarchyMemberHelpers
{
    internal sealed record FieldTarget(
        IFieldSymbol Symbol,
        VariableDeclaratorSyntax Variable,
        FieldDeclarationSyntax Field,
        TypeDeclarationSyntax Type,
        INamedTypeSymbol ContainingType);

    internal sealed record MethodTarget(
        IMethodSymbol Symbol,
        MethodDeclarationSyntax Method,
        TypeDeclarationSyntax Type,
        INamedTypeSymbol ContainingType);

    internal sealed record SourceType(INamedTypeSymbol Symbol, TypeDeclarationSyntax Declaration, Document Document);

    internal static async Task<FieldTarget> FindFieldAsync(
        Document document,
        string className,
        string fieldName,
        CancellationToken cancellationToken)
    {
        var (type, declaration) = await TypeRefactoringHelpers.FindTypeAsync(document, className, cancellationToken);
        var model = await document.GetSemanticModelAsync(cancellationToken);
        var variable = declaration.Members.OfType<FieldDeclarationSyntax>()
            .SelectMany(f => f.Declaration.Variables)
            .FirstOrDefault(v => v.Identifier.ValueText == fieldName)
            ?? throw new McpException($"Error: Field {fieldName} not found in {className}");

        var field = (FieldDeclarationSyntax)variable.Parent!.Parent!;
        return new FieldTarget((IFieldSymbol)model!.GetDeclaredSymbol(variable, cancellationToken)!, variable, field, declaration, type);
    }

    internal static async Task<MethodTarget> FindMethodAsync(
        Document document,
        string className,
        string methodName,
        int? line,
        CancellationToken cancellationToken)
    {
        var (type, declaration) = await TypeRefactoringHelpers.FindTypeAsync(document, className, cancellationToken);
        var model = await document.GetSemanticModelAsync(cancellationToken);
        var candidates = declaration.Members.OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.ValueText == methodName)
            .ToList();
        var method = TypeRefactoringHelpers.Choose(candidates, m => m.Identifier, $"Method {methodName} in {className}", line);
        return new MethodTarget(model!.GetDeclaredSymbol(method, cancellationToken)!, method, declaration, type);
    }

    /// <summary>The declaration of <paramref name="type"/>'s base class, which must be in the solution.</summary>
    internal static async Task<SourceType> SourceBaseAsync(
        Solution solution,
        INamedTypeSymbol type,
        CancellationToken cancellationToken)
    {
        var baseType = type.BaseType;
        if (baseType is null || baseType.SpecialType == SpecialType.System_Object || type.TypeKind != TypeKind.Class)
            throw new McpException($"Error: {type.Name} has no base class");

        return await DeclarationAsync(solution, baseType, cancellationToken)
            ?? throw new McpException($"Error: The base class {baseType.OriginalDefinition.ToDisplayString()} is not declared in the solution");
    }

    internal static async Task<SourceType?> DeclarationAsync(
        Solution solution,
        INamedTypeSymbol type,
        CancellationToken cancellationToken)
    {
        var reference = type.OriginalDefinition.DeclaringSyntaxReferences.FirstOrDefault();
        if (reference is null)
            return null;

        var declaration = (TypeDeclarationSyntax)await reference.GetSyntaxAsync(cancellationToken);
        var document = solution.GetDocument(reference.SyntaxTree);
        return document is null ? null : new SourceType(type.OriginalDefinition, declaration, document);
    }

    /// <summary>Every class below <paramref name="type"/> declared in the solution, with its declaration.</summary>
    internal static async Task<IReadOnlyList<SourceType>> SubclassesAsync(
        Solution solution,
        INamedTypeSymbol type,
        bool transitive,
        CancellationToken cancellationToken)
    {
        var derived = await SymbolFinder.FindDerivedClassesAsync(type.OriginalDefinition, solution, transitive, cancellationToken: cancellationToken);
        var found = new List<SourceType>();
        foreach (var subclass in derived)
        {
            if (await DeclarationAsync(solution, subclass, cancellationToken) is { } declaration)
                found.Add(declaration);
        }

        return found;
    }

    /// <summary>
    /// How a subclass's type parameters read in its base class: for
    /// <c>Repository&lt;T&gt; : Store&lt;T&gt;</c> with <c>Store&lt;TItem&gt;</c>, <c>T</c> becomes <c>TItem</c>.
    /// </summary>
    internal static Dictionary<ITypeParameterSymbol, TypeSyntax> TowardsBase(INamedTypeSymbol subclass)
    {
        var map = new Dictionary<ITypeParameterSymbol, TypeSyntax>(SymbolEqualityComparer.Default);
        var baseType = subclass.BaseType!;
        for (var i = 0; i < baseType.TypeArguments.Length; i++)
        {
            if (baseType.TypeArguments[i] is ITypeParameterSymbol parameter
                && SymbolEqualityComparer.Default.Equals(parameter.DeclaringType, subclass.OriginalDefinition)
                && !map.ContainsKey(parameter))
            {
                map[parameter] = SyntaxFactory.IdentifierName(baseType.OriginalDefinition.TypeParameters[i].Name);
            }
        }

        return map;
    }

    /// <summary>
    /// How a base class's type parameters read in a subclass: for
    /// <c>OrderStore : Store&lt;Order&gt;</c>, <c>TItem</c> becomes <c>Order</c>.
    /// </summary>
    internal static Dictionary<ITypeParameterSymbol, TypeSyntax> TowardsSubclass(
        INamedTypeSymbol subclass,
        SemanticModel subclassModel,
        int position)
    {
        var map = new Dictionary<ITypeParameterSymbol, TypeSyntax>(SymbolEqualityComparer.Default);
        var baseType = subclass.BaseType!;
        for (var i = 0; i < baseType.TypeArguments.Length; i++)
        {
            map[baseType.OriginalDefinition.TypeParameters[i]] =
                SyntaxFactory.ParseTypeName(baseType.TypeArguments[i].ToMinimalDisplayString(subclassModel, position));
        }

        return map;
    }

    /// <summary>
    /// Rewrites each use of a mapped type parameter in <paramref name="node"/>,
    /// which must belong to the tree <paramref name="model"/> describes.
    /// </summary>
    internal static T Substitute<T>(T node, SemanticModel model, IReadOnlyDictionary<ITypeParameterSymbol, TypeSyntax> map)
        where T : SyntaxNode
    {
        var uses = node.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(n => model.GetSymbolInfo(n).Symbol is ITypeParameterSymbol p && map.ContainsKey(p))
            .ToList();

        return node.ReplaceNodes(uses, (original, _) =>
            map[(ITypeParameterSymbol)model.GetSymbolInfo(original).Symbol!].WithTriviaFrom(original));
    }

    /// <summary>
    /// The first thing <paramref name="node"/> uses that belongs to
    /// <paramref name="subclass"/> alone, so would not exist in its base: a
    /// member other than <paramref name="self"/>, a nested type, or a type
    /// parameter the base does not share. Null when there is none.
    /// </summary>
    internal static string? SubclassOnlyUse(
        SyntaxNode node,
        SemanticModel model,
        INamedTypeSymbol subclass,
        ISymbol self,
        IReadOnlyDictionary<ITypeParameterSymbol, TypeSyntax> map)
    {
        var definition = subclass.OriginalDefinition;
        foreach (var name in node.DescendantNodesAndSelf().OfType<SimpleNameSyntax>())
        {
            var symbol = model.GetSymbolInfo(name).Symbol;
            if (symbol is null || SymbolEqualityComparer.Default.Equals(symbol.OriginalDefinition, self.OriginalDefinition))
                continue;

            var belongs = symbol switch
            {
                ITypeParameterSymbol parameter => SymbolEqualityComparer.Default.Equals(parameter.DeclaringType, definition)
                    && !map.ContainsKey(parameter),
                INamedTypeSymbol type => IsWithin(type.ContainingType, definition),
                IFieldSymbol or IPropertySymbol or IMethodSymbol or IEventSymbol =>
                    symbol is not IMethodSymbol { MethodKind: MethodKind.LocalFunction }
                    && IsWithin(symbol.ContainingType, definition),
                _ => false,
            };

            if (belongs)
                return symbol.Name;
        }

        return null;
    }

    private static bool IsWithin(INamedTypeSymbol? type, INamedTypeSymbol definition)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, definition))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Changes a member's modifiers, keeping its comments and indentation in
    /// front of whichever token ends up first.
    /// </summary>
    internal static T WithModifiers<T>(T member, Func<SyntaxTokenList, SyntaxTokenList> change)
        where T : MemberDeclarationSyntax
    {
        var leading = member.GetLeadingTrivia();
        var stripped = member.WithoutLeadingTrivia();
        return (T)stripped.WithModifiers(change(stripped.Modifiers)).WithLeadingTrivia(leading);
    }

    /// <summary>A private member moving into a base class becomes protected so the subclass keeps access.</summary>
    internal static SyntaxTokenList WidenPrivate(SyntaxTokenList modifiers)
    {
        var access = modifiers.Where(m => SyntaxFacts.IsAccessibilityModifier(m.Kind())).ToList();
        if (access.Count == 1 && access[0].IsKind(SyntaxKind.PrivateKeyword))
            return modifiers.Replace(access[0], SyntaxFactory.Token(access[0].LeadingTrivia, SyntaxKind.ProtectedKeyword, access[0].TrailingTrivia));

        if (access.Count == 0)
            return modifiers.Insert(0, SyntaxFactory.Token(SyntaxKind.ProtectedKeyword).WithTrailingTrivia(SyntaxFactory.Space));

        return modifiers;
    }

    /// <summary>Puts <paramref name="keyword"/> after the accessibility modifiers, where C# style expects it.</summary>
    internal static SyntaxTokenList WithModifier(SyntaxTokenList modifiers, SyntaxKind keyword)
    {
        var index = modifiers.TakeWhile(m => SyntaxFacts.IsAccessibilityModifier(m.Kind())).Count();
        return modifiers.Insert(index, SyntaxFactory.Token(keyword).WithTrailingTrivia(SyntaxFactory.Space));
    }

    internal static SyntaxTokenList WithoutModifiers(SyntaxTokenList modifiers, params SyntaxKind[] kinds) =>
        SyntaxFactory.TokenList(modifiers.Where(m => !kinds.Contains(m.Kind())));

    /// <summary>
    /// The declaration's tokens without any trivia, which is what two members
    /// share when they are the same code laid out or commented differently.
    /// </summary>
    internal static string Shape(SyntaxNode node) =>
        string.Join(" ", node.DescendantTokens().Select(t => t.Text));

    /// <summary>
    /// A declaration of just <paramref name="variable"/>, taking the field's
    /// comments only when the field declares nothing else.
    /// </summary>
    internal static FieldDeclarationSyntax SingleVariable(FieldDeclarationSyntax field, VariableDeclaratorSyntax variable)
    {
        var single = field.WithDeclaration(field.Declaration.WithVariables(
            SyntaxFactory.SingletonSeparatedList(variable.WithoutTrivia())));
        return field.Declaration.Variables.Count == 1
            ? single
            : single.WithLeadingTrivia(SyntaxFactory.TriviaList());
    }

    /// <summary>Removes one field from its type, or one variable from a declaration of several.</summary>
    internal static void RemoveField(MultiDocumentEdits edits, Document document, TypeDeclarationSyntax type, FieldDeclarationSyntax field, VariableDeclaratorSyntax variable)
    {
        if (field.Declaration.Variables.Count == 1)
        {
            edits.RemoveMember(document, type, field);
            return;
        }

        edits.Replace(document, field, f =>
        {
            var current = f.Declaration.Variables.First(v => v.Identifier.ValueText == variable.Identifier.ValueText);
            return f.WithDeclaration(f.Declaration.WithVariables(f.Declaration.Variables.Remove(current)));
        });
    }

    /// <summary>
    /// Removes a member with its comments. The blank line that separated it
    /// from the member before goes with it, and a member left first in the
    /// type loses the blank line that separated it from the removed one.
    /// </summary>
    internal static TypeDeclarationSyntax RemoveMember(TypeDeclarationSyntax type, MemberDeclarationSyntax member)
    {
        var index = type.Members.IndexOf(member);
        var updated = type.RemoveNode(member, SyntaxRemoveOptions.KeepUnbalancedDirectives)!;
        if (index == 0 && updated.Members.Count > 0)
        {
            var first = updated.Members[0];
            updated = updated.ReplaceNode(first, first.WithLeadingTrivia(WithoutLeadingBlankLines(first.GetLeadingTrivia())));
        }

        return updated;
    }

    /// <summary>
    /// Adds a member to a type: a field after the last field, a constructor
    /// after the fields and constructors, anything else at the end. Fields sit
    /// together; other members are set apart by a blank line.
    /// </summary>
    internal static TypeDeclarationSyntax InsertMember(TypeDeclarationSyntax type, MemberDeclarationSyntax member, SyntaxTrivia endOfLine)
    {
        var isField = member is FieldDeclarationSyntax;
        var after = member switch
        {
            FieldDeclarationSyntax => type.Members.LastOrDefault(m => m is FieldDeclarationSyntax),
            ConstructorDeclarationSyntax => type.Members.LastOrDefault(m => m is FieldDeclarationSyntax or ConstructorDeclarationSyntax),
            _ => type.Members.LastOrDefault(),
        };
        var index = after is null ? 0 : type.Members.IndexOf(after) + 1;

        var previous = index > 0 ? type.Members[index - 1] : null;
        var leading = WithoutLeadingBlankLines(member.GetLeadingTrivia());
        if (previous is not null && !(isField && previous is FieldDeclarationSyntax))
            leading = leading.Insert(0, endOfLine);

        var inserted = member
            .WithLeadingTrivia(leading)
            .WithTrailingTrivia(member.GetTrailingTrivia().Any(t => t.IsKind(SyntaxKind.EndOfLineTrivia))
                ? member.GetTrailingTrivia()
                : member.GetTrailingTrivia().Add(endOfLine))
            .WithAdditionalAnnotations(Formatter.Annotation);
        var members = type.Members.Insert(index, inserted);

        if (index + 1 < members.Count)
        {
            var next = members[index + 1];
            var nextLeading = next.GetLeadingTrivia();
            var separated = nextLeading.TakeWhile(t => t.IsKind(SyntaxKind.WhitespaceTrivia)).Count() < nextLeading.Count
                && nextLeading.SkipWhile(t => t.IsKind(SyntaxKind.WhitespaceTrivia)).First().IsKind(SyntaxKind.EndOfLineTrivia);
            if (!separated && !(isField && next is FieldDeclarationSyntax))
                members = members.Replace(next, next.WithLeadingTrivia(nextLeading.Insert(0, endOfLine)));
        }

        return type.WithMembers(members);
    }

    /// <summary>Drops the empty lines at the start of a member's trivia, keeping the indentation of its first line.</summary>
    internal static SyntaxTriviaList WithoutLeadingBlankLines(SyntaxTriviaList trivia)
    {
        var blank = trivia.TakeWhile(t => t.IsKind(SyntaxKind.WhitespaceTrivia) || t.IsKind(SyntaxKind.EndOfLineTrivia)).ToList();
        var lastEndOfLine = blank.FindLastIndex(t => t.IsKind(SyntaxKind.EndOfLineTrivia));
        return SyntaxFactory.TriviaList(trivia.Skip(lastEndOfLine + 1));
    }
}

/// <summary>
/// Edits to several documents, applied together. Nodes are named as they are
/// in the original trees and tracked through earlier edits, so edits to a type
/// and to members inside it can be combined.
/// </summary>
internal sealed class MultiDocumentEdits
{
    private readonly Solution _solution;
    private readonly Dictionary<DocumentId, List<SyntaxNode>> _tracked = new();
    private readonly Dictionary<DocumentId, List<Func<SyntaxNode, SyntaxNode>>> _edits = new();
    private readonly Dictionary<DocumentId, HashSet<string>> _imports = new();

    public MultiDocumentEdits(Solution solution) => _solution = solution;

    /// <summary>Replaces a node with what <paramref name="edit"/> makes of its current version.</summary>
    public void Replace<T>(Document document, T node, Func<T, SyntaxNode> edit)
        where T : SyntaxNode
    {
        Add(document, new SyntaxNode[] { node }, root =>
        {
            var current = Current(root, node);
            return root.ReplaceNode(current, edit(current));
        });
    }

    public void RemoveMember(Document document, TypeDeclarationSyntax type, MemberDeclarationSyntax member)
    {
        Add(document, new SyntaxNode[] { type, member }, root =>
        {
            var currentType = Current(root, type);
            return root.ReplaceNode(currentType, HierarchyMemberHelpers.RemoveMember(currentType, Current(root, member)));
        });
    }

    public void Import(Document document, IEnumerable<string> namespaces)
    {
        if (!_imports.TryGetValue(document.Id, out var set))
            _imports[document.Id] = set = new(StringComparer.Ordinal);
        set.UnionWith(namespaces);
    }

    public async Task<Solution> ApplyAsync(CancellationToken cancellationToken)
    {
        var solution = _solution;
        foreach (var id in _edits.Keys.Union(_imports.Keys).ToList())
        {
            var root = (await solution.GetDocument(id)!.GetSyntaxRootAsync(cancellationToken))!;
            if (_edits.TryGetValue(id, out var edits))
            {
                root = root.TrackNodes(_tracked[id]);
                foreach (var edit in edits)
                    root = edit(root);
            }

            if (_imports.TryGetValue(id, out var namespaces))
                root = TypeRefactoringHelpers.AddUsings((CompilationUnitSyntax)root, namespaces);

            solution = solution.WithDocumentSyntaxRoot(id, root);
            var formatted = await Formatter.FormatAsync(solution.GetDocument(id)!, Formatter.Annotation, cancellationToken: cancellationToken);
            solution = formatted.Project.Solution;
        }

        return solution;
    }

    private void Add(Document document, IEnumerable<SyntaxNode> nodes, Func<SyntaxNode, SyntaxNode> edit)
    {
        if (!_edits.TryGetValue(document.Id, out var edits))
        {
            _edits[document.Id] = edits = new();
            _tracked[document.Id] = new();
        }

        _tracked[document.Id].AddRange(nodes);
        edits.Add(edit);
    }

    private static T Current<T>(SyntaxNode root, T node)
        where T : SyntaxNode =>
        root.GetCurrentNode(node) ?? throw new InvalidOperationException($"Lost track of {node.Kind()} while editing");
}
