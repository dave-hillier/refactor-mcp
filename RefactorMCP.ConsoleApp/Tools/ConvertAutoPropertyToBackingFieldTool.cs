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
public static class ConvertAutoPropertyToBackingFieldTool
{
    [McpServerTool, Description("Convert an auto-property into a property with a private backing field and accessors that read and write it")]
    public static async Task<string> ConvertAutoPropertyToBackingField(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the property")] string filePath,
        [Description("Name of the auto-property to convert")] string propertyName,
        [Description("Name for the backing field (optional, _camelCase of the property by default)")] string? fieldName = null)
    {
        try
        {
            var document = await FieldPropertyRefactoring.GetDocumentAsync(solutionPath, filePath);
            var property = await FieldPropertyRefactoring.FindPropertyAsync(document, propertyName);
            propertyName = property.Name;
            var declaration = await FieldPropertyRefactoring.DeclarationAsync<PropertyDeclarationSyntax>(property);
            if (!IsAutoProperty(property, declaration))
                throw new McpException($"Error: Property '{propertyName}' is not an auto-property");

            fieldName ??= FieldPropertyRefactoring.FieldNameFor(propertyName);
            if (FieldPropertyRefactoring.HasMemberNamed(property.ContainingType, fieldName))
                throw new McpException($"Error: The type already has a member named '{fieldName}'");

            var accessors = declaration.AccessorList!.Accessors;
            var getOnly = accessors.All(a => a.IsKind(SyntaxKind.GetAccessorDeclaration));
            var isReadOnly = !accessors.Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration));

            // A get-only auto-property is assigned in constructors; with no
            // setter left, those assignments have to go to the field.
            var solution = document.Project.Solution;
            var propertyDocument = solution.GetDocument(declaration.SyntaxTree)!;
            var writes = new List<ReferenceLocation>();
            if (getOnly)
            {
                foreach (var location in (await SymbolFinder.FindReferencesAsync(property, solution)).SelectMany(r => r.Locations))
                {
                    var root = (await location.Document.GetSyntaxRootAsync())!;
                    if (FieldPropertyRefactoring.IsWrite((ExpressionSyntax)root.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true)))
                        writes.Add(location);
                }
            }

            var changed = solution;
            foreach (var documentId in writes.Select(l => l.Document.Id).Append(propertyDocument.Id).Distinct())
            {
                var editor = await DocumentEditor.CreateAsync(changed.GetDocument(documentId)!);
                foreach (var location in writes.Where(l => l.Document.Id == documentId))
                {
                    var name = editor.OriginalRoot.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true);
                    editor.ReplaceNode(name, SyntaxFactory.IdentifierName(fieldName).WithTriviaFrom(name));
                }
                if (documentId == propertyDocument.Id)
                {
                    var field = BackingField(declaration, property, fieldName, isReadOnly);
                    var type = declaration.Ancestors().OfType<TypeDeclarationSyntax>().First();
                    editor.ReplaceNode(declaration, WithBackingField(declaration, fieldName, getOnly));
                    editor.ReplaceNode(type, (node, _) => FieldPropertyRefactoring.AddField((TypeDeclarationSyntax)node, field));
                }
                changed = editor.GetChangedDocument().Project.Solution;
            }

            await FieldPropertyRefactoring.WriteChangesAsync(solution, changed);
            return $"Successfully gave '{propertyName}' the backing field '{fieldName}'";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error converting auto-property: {ex.Message}", ex);
        }
    }

    private static bool IsAutoProperty(IPropertySymbol property, PropertyDeclarationSyntax declaration) =>
        !property.IsAbstract
        && !property.IsExtern
        && property.ContainingType.TypeKind != TypeKind.Interface
        && declaration.AccessorList is { } accessors
        && accessors.Accessors.All(a => a.Body is null && a.ExpressionBody is null);

    /// <summary>
    /// A private field of the property's type holding its initialiser. It is
    /// readonly when nothing but constructors and <c>init</c> can assign it.
    /// </summary>
    private static FieldDeclarationSyntax BackingField(PropertyDeclarationSyntax declaration, IPropertySymbol property, string fieldName, bool isReadOnly)
    {
        var modifiers = SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));
        if (property.IsStatic)
            modifiers = modifiers.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));
        if (isReadOnly)
            modifiers = modifiers.Add(SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));

        var variable = SyntaxFactory.VariableDeclarator(fieldName);
        if (declaration.Initializer is { } initializer)
            variable = variable.WithInitializer(SyntaxFactory.EqualsValueClause(initializer.Value.WithoutTrivia()));

        return SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration(declaration.Type.WithoutTrivia(), SyntaxFactory.SingletonSeparatedList(variable)))
            .WithModifiers(modifiers);
    }

    /// <summary>
    /// A get-only property reads the field through an expression body;
    /// otherwise each accessor gets a body, keeping its modifiers.
    /// </summary>
    private static PropertyDeclarationSyntax WithBackingField(PropertyDeclarationSyntax declaration, string fieldName, bool getOnly)
    {
        var withoutInitializer = declaration.WithInitializer(null).WithSemicolonToken(default);
        var trailing = declaration.GetTrailingTrivia();

        if (getOnly)
        {
            return withoutInitializer
                .WithIdentifier(declaration.Identifier.WithTrailingTrivia(SyntaxFactory.Space))
                .WithAccessorList(null)
                .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(
                    SyntaxFactory.Token(SyntaxKind.EqualsGreaterThanToken).WithTrailingTrivia(SyntaxFactory.Space),
                    SyntaxFactory.IdentifierName(fieldName)))
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken).WithTrailingTrivia(trailing));
        }

        var accessors = declaration.AccessorList!.Accessors
            .Select(a => (a.Kind(), FieldPropertyRefactoring.AccessorModifiers(a)));
        var newLine = FieldPropertyRefactoring.NewLine(declaration.Ancestors().OfType<TypeDeclarationSyntax>().First());
        return withoutInitializer
            .WithIdentifier(declaration.Identifier.WithoutTrivia())
            .WithAccessorList(FieldPropertyRefactoring.BackingAccessors(fieldName, accessors, newLine).WithTrailingTrivia(trailing));
    }
}
