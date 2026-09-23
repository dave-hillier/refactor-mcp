using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using System.Linq;

[McpServerToolType]
public static class EncapsulateFieldTool
{
    [McpServerTool, Description("Encapsulate a field: make it private behind a property that reads and writes it, and point code outside the type at the property")]
    public static async Task<string> EncapsulateField(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file declaring the field")] string filePath,
        [Description("Name of the field to encapsulate")] string fieldName,
        [Description("Name for the property (optional, derived from the field name by default)")] string? propertyName = null)
    {
        try
        {
            var document = await FieldPropertyRefactoring.GetDocumentAsync(solutionPath, filePath);
            var field = await FieldPropertyRefactoring.FindFieldAsync(document, fieldName);
            if (field.IsConst)
                throw new McpException($"Error: '{fieldName}' is a constant, which has no storage to encapsulate");

            var variable = await FieldPropertyRefactoring.DeclarationAsync<VariableDeclaratorSyntax>(field);
            var declaration = (FieldDeclarationSyntax)variable.Parent!.Parent!;
            if (declaration.Declaration.Variables.Count > 1)
                throw new McpException($"Error: '{fieldName}' is declared alongside other fields; declare it on its own first");

            propertyName ??= FieldPropertyRefactoring.PropertyNameFor(fieldName);
            var backingName = propertyName == fieldName ? FieldPropertyRefactoring.FieldNameFor(propertyName) : fieldName;
            foreach (var name in new[] { propertyName, backingName }.Where(n => n != fieldName))
            {
                if (FieldPropertyRefactoring.HasMemberNamed(field.ContainingType, name))
                    throw new McpException($"Error: The type already has a member named '{name}'");
            }

            var solution = document.Project.Solution;
            var declaringDocument = solution.GetDocument(variable.SyntaxTree)!;
            var locations = (await SymbolFinder.FindReferencesAsync(field, solution))
                .SelectMany(r => r.Locations)
                .GroupBy(l => l.Document.Id);

            var changed = solution;
            foreach (var group in locations.Where(g => g.Key != declaringDocument.Id))
            {
                var editor = await DocumentEditor.CreateAsync(changed.GetDocument(group.Key)!);
                await RenameReferences(editor, group, field, fieldName, backingName, propertyName);
                changed = editor.GetChangedDocument().Project.Solution;
            }

            var declaringEditor = await DocumentEditor.CreateAsync(changed.GetDocument(declaringDocument.Id)!);
            await RenameReferences(declaringEditor, locations.FirstOrDefault(g => g.Key == declaringDocument.Id) ?? Enumerable.Empty<ReferenceLocation>(), field, fieldName, backingName, propertyName);
            var typeDeclaration = declaration.Ancestors().OfType<TypeDeclarationSyntax>().First();
            declaringEditor.ReplaceNode(typeDeclaration, (node, _) => Encapsulate((TypeDeclarationSyntax)node, field, fieldName, backingName, propertyName));
            changed = declaringEditor.GetChangedDocument().Project.Solution;

            await FieldPropertyRefactoring.WriteChangesAsync(solution, changed);
            return $"Successfully encapsulated field '{fieldName}' as property '{propertyName}'";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error encapsulating field: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Code inside the type keeps using the field, under its new name if it
    /// has one; code outside uses the property.
    /// </summary>
    private static async Task RenameReferences(
        DocumentEditor editor,
        IEnumerable<ReferenceLocation> locations,
        IFieldSymbol field,
        string fieldName,
        string backingName,
        string propertyName)
    {
        foreach (var location in locations)
        {
            // The field's type is compared in the solution it came from.
            var model = (await location.Document.GetSemanticModelAsync())!;
            var original = (await location.Document.GetSyntaxRootAsync())!.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true);
            var inside = FieldPropertyRefactoring.IsInsideType(original, model, field.ContainingType);

            var name = (SimpleNameSyntax)editor.OriginalRoot.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true);
            if (!inside && FieldPropertyRefactoring.IsPassedByReference(name))
                throw new McpException($"Error: '{fieldName}' is passed by reference at {location.Location.GetLineSpan()}, which a property cannot be");

            var replacement = inside ? backingName : propertyName;
            if (replacement != fieldName)
                editor.ReplaceNode(name, SyntaxFactory.IdentifierName(replacement).WithTriviaFrom(name));
        }
    }

    /// <summary>
    /// Makes the field private under its backing name and adds the property
    /// after the type's fields. The field's documentation comment moves to
    /// the property, which is what callers now see.
    /// </summary>
    private static TypeDeclarationSyntax Encapsulate(TypeDeclarationSyntax type, IFieldSymbol field, string fieldName, string backingName, string propertyName)
    {
        var declaration = type.Members.OfType<FieldDeclarationSyntax>()
            .First(f => f.Declaration.Variables.Any(v => v.Identifier.ValueText == fieldName));
        var variable = declaration.Declaration.Variables[0];
        var leading = declaration.GetLeadingTrivia();
        var documentation = leading.Where(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)).ToList();

        var accessModifiers = declaration.Modifiers.Where(IsAccessModifier).ToList();
        var otherModifiers = declaration.Modifiers.Where(m => !IsAccessModifier(m)).ToList();
        var privateModifiers = SyntaxFactory.TokenList(
            new[] { SyntaxFactory.Token(SyntaxKind.PrivateKeyword).WithTrailingTrivia(SyntaxFactory.Space) }
                .Concat(otherModifiers.Select(m => m.WithoutTrivia().WithTrailingTrivia(SyntaxFactory.Space))));
        var privateField = declaration
            .WithModifiers(privateModifiers)
            .WithDeclaration(declaration.Declaration.WithVariables(SyntaxFactory.SingletonSeparatedList(
                variable.WithIdentifier(SyntaxFactory.Identifier(backingName).WithTriviaFrom(variable.Identifier)))))
            .WithLeadingTrivia(WithoutDocumentation(leading));

        var accessibility = accessModifiers.Count == 0 || field.DeclaredAccessibility == Accessibility.Private
            ? "public"
            : string.Join(" ", accessModifiers.Select(m => m.ValueText));
        var property = Property(accessibility, field, declaration.Declaration.Type, propertyName, backingName, FieldPropertyRefactoring.NewLine(type));

        var indentation = FieldPropertyRefactoring.Indentation(declaration);
        var propertyLeading = new List<SyntaxTrivia> { FieldPropertyRefactoring.NewLine(type) };
        propertyLeading.AddRange(indentation);
        foreach (var comment in documentation)
        {
            propertyLeading.Add(comment);
            propertyLeading.AddRange(indentation);
        }
        property = property
            .WithLeadingTrivia(propertyLeading)
            .WithTrailingTrivia(FieldPropertyRefactoring.NewLine(type))
            .WithAdditionalAnnotations(Formatter.Annotation);

        var members = type.Members.Replace(declaration, privateField);
        var lastField = members.OfType<FieldDeclarationSyntax>().Last();
        return type.WithMembers(members.Insert(members.IndexOf(lastField) + 1, property));
    }

    private static PropertyDeclarationSyntax Property(string accessibility, IFieldSymbol field, TypeSyntax type, string propertyName, string backingName, SyntaxTrivia newLine)
    {
        var modifiers = field.IsStatic ? $"{accessibility} static" : accessibility;
        var header = $"{modifiers} {type.WithoutTrivia()} {propertyName}";
        if (field.IsReadOnly)
            return (PropertyDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration($"{header} => {backingName};")!;

        var accessors = new[] { (SyntaxKind.GetAccessorDeclaration, ""), (SyntaxKind.SetAccessorDeclaration, "") };
        var property = (PropertyDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration($"{header} {{ get; }}")!;
        return property
            .WithIdentifier(property.Identifier.WithoutTrivia())
            .WithAccessorList(FieldPropertyRefactoring.BackingAccessors(backingName, accessors, newLine));
    }

    private static SyntaxTriviaList WithoutDocumentation(SyntaxTriviaList leading)
    {
        var kept = new List<SyntaxTrivia>();
        for (var i = 0; i < leading.Count; i++)
        {
            var isDocumentation = leading[i].IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || leading[i].IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);
            var precedesDocumentation = leading[i].IsKind(SyntaxKind.WhitespaceTrivia) && i + 1 < leading.Count
                && (leading[i + 1].IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || leading[i + 1].IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));
            if (!isDocumentation && !precedesDocumentation)
                kept.Add(leading[i]);
        }
        return SyntaxFactory.TriviaList(kept);
    }

    private static bool IsAccessModifier(SyntaxToken modifier) =>
        modifier.IsKind(SyntaxKind.PublicKeyword) || modifier.IsKind(SyntaxKind.PrivateKeyword)
        || modifier.IsKind(SyntaxKind.ProtectedKeyword) || modifier.IsKind(SyntaxKind.InternalKeyword);
}
