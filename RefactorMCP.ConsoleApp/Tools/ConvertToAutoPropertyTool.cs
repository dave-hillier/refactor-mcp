using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
using System.Linq;

[McpServerToolType]
public static class ConvertToAutoPropertyTool
{
    [McpServerTool, Description("Convert a property whose accessors only read and write a private field into an auto-property, removing the field")]
    public static async Task<string> ConvertToAutoProperty(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file declaring the property")] string filePath,
        [Description("Name of the property to convert")] string propertyName)
    {
        try
        {
            var document = await FieldPropertyRefactoring.GetDocumentAsync(solutionPath, filePath);
            var property = await FieldPropertyRefactoring.FindPropertyAsync(document, propertyName);
            propertyName = property.Name;
            var declaration = await FieldPropertyRefactoring.DeclarationAsync<PropertyDeclarationSyntax>(property);
            var solution = document.Project.Solution;
            var propertyDocument = solution.GetDocument(declaration.SyntaxTree)!;
            var model = (await propertyDocument.GetSemanticModelAsync())!;

            var field = BackingField(declaration, property, model);
            if (field.DeclaredAccessibility != Accessibility.Private)
                throw new McpException($"Error: The backing field '{field.Name}' is not private, so code outside the type may use it");

            var locations = (await SymbolFinder.FindReferencesAsync(field, solution))
                .SelectMany(r => r.Locations)
                .Where(l => !(l.Document.Id == propertyDocument.Id && declaration.Span.Contains(l.Location.SourceSpan)))
                .ToList();

            var writtenAfterConstruction = false;
            foreach (var location in locations)
            {
                var root = (await location.Document.GetSyntaxRootAsync())!;
                var name = (ExpressionSyntax)root.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true);
                if (FieldPropertyRefactoring.IsPassedByReference(name))
                    throw new McpException($"Error: The backing field '{field.Name}' is passed by reference at {location.Location.GetLineSpan()}, which a property cannot be");
                if (FieldPropertyRefactoring.IsWrite(name) && !IsInConstructor(name, field.IsStatic))
                    writtenAfterConstruction = true;
            }

            var variable = await FieldPropertyRefactoring.DeclarationAsync<VariableDeclaratorSyntax>(field);
            var documentIds = locations.Select(l => l.Document.Id).Append(propertyDocument.Id).Distinct();
            var changed = solution;
            foreach (var documentId in documentIds)
            {
                var editor = await DocumentEditor.CreateAsync(changed.GetDocument(documentId)!);
                foreach (var location in locations.Where(l => l.Document.Id == documentId))
                {
                    var name = editor.OriginalRoot.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true);
                    editor.ReplaceNode(name, SyntaxFactory.IdentifierName(propertyName).WithTriviaFrom(name));
                }
                if (documentId == propertyDocument.Id)
                    editor.ReplaceNode(declaration, AutoProperty(declaration, variable.Initializer?.Value, writtenAfterConstruction));
                changed = editor.GetChangedDocument().Project.Solution;
            }

            changed = await FieldPropertyRefactoring.RemoveFieldAsync(changed, field, solution.GetDocument(variable.SyntaxTree)!.Id);

            await FieldPropertyRefactoring.WriteChangesAsync(solution, changed);
            return $"Successfully converted '{propertyName}' to an auto-property and removed '{field.Name}'";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error converting to auto-property: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// The field the property's getter returns and its setter, if any,
    /// assigns <c>value</c> to, with nothing else in either accessor.
    /// </summary>
    private static IFieldSymbol BackingField(PropertyDeclarationSyntax declaration, IPropertySymbol property, SemanticModel model)
    {
        var accessors = declaration.AccessorList?.Accessors.ToList() ?? new List<AccessorDeclarationSyntax>();
        if (declaration.ExpressionBody is null && accessors.All(a => a.Body is null && a.ExpressionBody is null))
            throw new McpException($"Error: Property '{property.Name}' has no backing field; it is already an auto-property");

        var notTrivial = new McpException($"Error: Property '{property.Name}' does more than read and write a single field");
        var getter = declaration.ExpressionBody?.Expression
            ?? Returned(accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.GetAccessorDeclaration)))
            ?? throw notTrivial;
        var field = model.GetSymbolInfo(getter).Symbol as IFieldSymbol ?? throw notTrivial;
        if (getter is not (IdentifierNameSyntax or MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax }))
            throw notTrivial;

        foreach (var setter in accessors.Where(a => !a.IsKind(SyntaxKind.GetAccessorDeclaration)))
        {
            var assignment = Assigned(setter);
            if (assignment is null
                || !SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(assignment.Left).Symbol, field)
                || model.GetSymbolInfo(assignment.Right).Symbol is not IParameterSymbol { Name: "value" })
                throw notTrivial;
        }

        if (!SymbolEqualityComparer.Default.Equals(field.ContainingType, property.ContainingType)
            || !SymbolEqualityComparer.Default.Equals(field.Type, property.Type)
            || field.IsStatic != property.IsStatic)
            throw notTrivial;

        return field;
    }

    private static ExpressionSyntax? Returned(AccessorDeclarationSyntax? getter) => getter switch
    {
        { ExpressionBody: { } body } => body.Expression,
        { Body.Statements: [ReturnStatementSyntax { Expression: { } returned }] } => returned,
        _ => null,
    };

    private static AssignmentExpressionSyntax? Assigned(AccessorDeclarationSyntax setter) => setter switch
    {
        { ExpressionBody.Expression: AssignmentExpressionSyntax assignment } => assignment,
        { Body.Statements: [ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment }] } => assignment,
        _ => null,
    } is { } found && found.IsKind(SyntaxKind.SimpleAssignmentExpression) ? found : null;

    /// <summary>Constructors may assign a get-only auto-property; anything else needs a setter.</summary>
    private static bool IsInConstructor(SyntaxNode node, bool isStatic)
    {
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
                return false;
            if (ancestor is ConstructorDeclarationSyntax constructor)
                return constructor.Modifiers.Any(SyntaxKind.StaticKeyword) == isStatic;
            if (ancestor is MemberDeclarationSyntax)
                return false;
        }
        return false;
    }

    /// <summary>
    /// The property with accessors that have no bodies, on one line. The
    /// accessors keep their modifiers and attributes; a get-only property
    /// whose field the type writes after construction gets a private setter.
    /// </summary>
    private static PropertyDeclarationSyntax AutoProperty(PropertyDeclarationSyntax declaration, ExpressionSyntax? initializer, bool needsSetter)
    {
        var accessors = declaration.AccessorList?.Accessors.ToList() ?? new List<AccessorDeclarationSyntax>();
        var texts = accessors.Count == 0
            ? new List<string> { "get;" }
            : accessors.Select(a => $"{Attributes(a)}{FieldPropertyRefactoring.AccessorModifiers(a)}{a.Keyword.ValueText};").ToList();
        if (needsSetter && accessors.All(a => a.IsKind(SyntaxKind.GetAccessorDeclaration)))
            texts.Add("private set;");

        var parsed = (PropertyDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration($"int P {{ {string.Join(" ", texts)} }}")!;
        var trailing = declaration.GetTrailingTrivia();
        var converted = declaration
            .WithIdentifier(declaration.Identifier.WithTrailingTrivia(SyntaxFactory.Space))
            .WithExpressionBody(null)
            .WithAccessorList(parsed.AccessorList!.WithoutTrivia())
            .WithSemicolonToken(default);

        if (initializer is null)
            return converted.WithTrailingTrivia(trailing);

        return converted
            .WithAccessorList(converted.AccessorList!.WithTrailingTrivia(SyntaxFactory.Space))
            .WithInitializer(SyntaxFactory.EqualsValueClause(
                SyntaxFactory.Token(SyntaxKind.EqualsToken).WithTrailingTrivia(SyntaxFactory.Space),
                initializer.WithoutTrivia()))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken).WithTrailingTrivia(trailing));
    }

    private static string Attributes(AccessorDeclarationSyntax accessor) =>
        accessor.AttributeLists.Count == 0 ? "" : string.Join(" ", accessor.AttributeLists.Select(a => a.WithoutTrivia().ToString())) + " ";
}
