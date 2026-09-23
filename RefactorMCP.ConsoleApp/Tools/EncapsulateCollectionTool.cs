using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using System.Linq;

[McpServerToolType]
public static class EncapsulateCollectionTool
{
    [McpServerTool, Description("Encapsulate a private List<T> field: expose it as a read-only list, add Add and Remove methods, and point callers that added or removed through the exposing property at them")]
    public static async Task<string> EncapsulateCollection(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file declaring the field")] string filePath,
        [Description("Name of the list field, optionally qualified by its type as Type.field")] string fieldName,
        [Description("Singular name for one element, used in AddX and RemoveX (optional, derived from the property name by default)")] string? elementName = null)
    {
        try
        {
            var document = await FieldPropertyRefactoring.GetDocumentAsync(solutionPath, filePath);
            var field = await FieldPropertyRefactoring.FindFieldAsync(document, fieldName);
            fieldName = field.Name;
            if (field.DeclaredAccessibility != Accessibility.Private)
                throw new McpException($"Error: '{fieldName}' is not private, so code outside the type can change it; encapsulate the field first");
            if (field.Type is not INamedTypeSymbol { Name: "List", Arity: 1 } list
                || list.ContainingNamespace.ToDisplayString() != "System.Collections.Generic")
                throw new McpException($"Error: '{fieldName}' is a {field.Type.ToDisplayString()}, and only List<T> fields are supported");

            var solution = document.Project.Solution;
            var variable = await FieldPropertyRefactoring.DeclarationAsync<VariableDeclaratorSyntax>(field);
            var declaringDocument = solution.GetDocument(variable.SyntaxTree)!;
            var model = (await declaringDocument.GetSemanticModelAsync())!;
            var type = variable.Ancestors().OfType<TypeDeclarationSyntax>().First();

            var exposing = ExposingProperty(type, field, model);
            if (exposing?.AccessorList?.Accessors.Any(a => !a.IsKind(SyntaxKind.GetAccessorDeclaration)) == true)
                throw new McpException($"Error: Property '{exposing.Identifier.ValueText}' can replace the list, so it cannot become read-only");

            var propertyName = exposing?.Identifier.ValueText ?? FieldPropertyRefactoring.PropertyNameFor(fieldName);
            elementName ??= Singular(propertyName);
            var addName = "Add" + elementName;
            var removeName = "Remove" + elementName;
            var names = exposing is null ? new[] { propertyName, addName, removeName } : new[] { addName, removeName };
            foreach (var name in names)
            {
                if (FieldPropertyRefactoring.HasMemberNamed(field.ContainingType, name))
                    throw new McpException($"Error: The type already has a member named '{name}'");
            }

            var calls = new List<(DocumentId Document, InvocationExpressionSyntax Call, string Method)>();
            if (exposing is not null)
            {
                var property = model.GetDeclaredSymbol(exposing)!;
                foreach (var location in (await SymbolFinder.FindReferencesAsync(property, solution)).SelectMany(r => r.Locations))
                {
                    var root = (await location.Document.GetSyntaxRootAsync())!;
                    var reference = FieldPropertyRefactoring.ReferenceExpression(root.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true));
                    if (reference.Parent is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Add" or "Remove" } member
                        && member.Expression == reference
                        && member.Parent is InvocationExpressionSyntax { ArgumentList.Arguments.Count: 1 } call)
                        calls.Add((location.Document.Id, call, member.Name.Identifier.ValueText == "Add" ? addName : removeName));
                }
            }

            var elementType = list.TypeArguments[0].ToMinimalDisplayString(model, variable.SpanStart, NullableFormat);
            var changed = solution;
            foreach (var documentId in calls.Select(c => c.Document).Append(declaringDocument.Id).Distinct())
            {
                var editor = await DocumentEditor.CreateAsync(changed.GetDocument(documentId)!);
                foreach (var (_, call, method) in calls.Where(c => c.Document == documentId))
                    editor.ReplaceNode(call, (node, _) => CallOnOwner((InvocationExpressionSyntax)node, method));

                if (documentId == declaringDocument.Id)
                {
                    var members = Members(fieldName, propertyName, elementName, elementType, exposing, FieldPropertyRefactoring.NewLine(type));
                    editor.ReplaceNode(type, (node, _) => Encapsulate((TypeDeclarationSyntax)node, fieldName, propertyName, exposing is not null, members));
                }
                changed = editor.GetChangedDocument().Project.Solution;
            }

            await EnsureStillCompiles(solution, changed, propertyName);
            await FieldPropertyRefactoring.WriteChangesAsync(solution, changed);
            return $"Successfully encapsulated '{fieldName}' behind '{propertyName}', {addName} and {removeName}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error encapsulating collection: {ex.Message}", ex);
        }
    }

    private static readonly SymbolDisplayFormat NullableFormat = SymbolDisplayFormat.MinimallyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>The property of the type whose getter only returns the field, if there is one.</summary>
    private static PropertyDeclarationSyntax? ExposingProperty(TypeDeclarationSyntax type, IFieldSymbol field, SemanticModel model) =>
        type.Members.OfType<PropertyDeclarationSyntax>().FirstOrDefault(p =>
            FieldPropertyRefactoring.GetterExpression(p) is { } returned
            && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(returned).Symbol, field));

    /// <summary><c>Tags</c> gives <c>Tag</c> and <c>Entries</c> gives <c>Entry</c>.</summary>
    private static string Singular(string plural)
    {
        if (plural.EndsWith("ies", StringComparison.Ordinal) && plural.Length > 3)
            return plural[..^3] + "y";
        if (plural.EndsWith("s", StringComparison.Ordinal) && !plural.EndsWith("ss", StringComparison.Ordinal))
            return plural[..^1];
        return plural;
    }

    /// <summary><c>order.Tags.Add(x)</c> becomes <c>order.AddTag(x)</c>, and <c>Tags.Add(x)</c> becomes <c>AddTag(x)</c>.</summary>
    private static InvocationExpressionSyntax CallOnOwner(InvocationExpressionSyntax call, string method)
    {
        var collection = ((MemberAccessExpressionSyntax)call.Expression).Expression;
        var name = SyntaxFactory.IdentifierName(method);
        ExpressionSyntax target = collection switch
        {
            MemberAccessExpressionSyntax access => access.WithName(name),
            _ => name.WithTriviaFrom(collection),
        };
        return call.WithExpression(target);
    }

    /// <summary>
    /// The read-only property, when one has to be created, and the Add and
    /// Remove methods, each preceded by a blank line.
    /// </summary>
    private static List<MemberDeclarationSyntax> Members(
        string fieldName,
        string propertyName,
        string elementName,
        string elementType,
        PropertyDeclarationSyntax? exposing,
        SyntaxTrivia newLine)
    {
        var parameter = char.ToLowerInvariant(elementName[0]) + elementName[1..];
        var texts = new List<string>();
        if (exposing is null)
            texts.Add($"public global::System.Collections.Generic.IReadOnlyList<{elementType}> {propertyName} => {fieldName}.AsReadOnly();");
        texts.Add($"public void Add{elementName}({elementType} {parameter}) => {fieldName}.Add({parameter});");
        texts.Add($"public bool Remove{elementName}({elementType} {parameter}) => {fieldName}.Remove({parameter});");

        return texts
            .Select(text => SyntaxFactory.ParseMemberDeclaration(text)!
                .WithLeadingTrivia(newLine)
                .WithTrailingTrivia(newLine)
                .WithAdditionalAnnotations(Formatter.Annotation, Simplifier.Annotation))
            .ToList();
    }

    /// <summary>
    /// Makes the exposing property return a read-only view, or adds one after
    /// the fields, and places the new methods after the property.
    /// </summary>
    private static TypeDeclarationSyntax Encapsulate(
        TypeDeclarationSyntax type,
        string fieldName,
        string propertyName,
        bool hasExposingProperty,
        List<MemberDeclarationSyntax> members)
    {
        int index;
        if (hasExposingProperty)
        {
            var property = type.Members.OfType<PropertyDeclarationSyntax>().First(p => p.Identifier.ValueText == propertyName);
            var returned = FieldPropertyRefactoring.GetterExpression(property)!;
            var readOnlyView = SyntaxFactory.ParseExpression($"{fieldName}.AsReadOnly()").WithTriviaFrom(returned);
            var elementType = (property.Type.WithoutTrivia() switch
            {
                GenericNameSyntax generic => generic,
                QualifiedNameSyntax { Right: GenericNameSyntax generic } => generic,
                _ => throw new McpException($"Error: Property '{propertyName}' is not declared as a List<T>"),
            }).TypeArgumentList;
            var readOnlyType = SyntaxFactory.ParseTypeName($"global::System.Collections.Generic.IReadOnlyList{elementType}")
                .WithTriviaFrom(property.Type)
                .WithAdditionalAnnotations(Simplifier.Annotation);
            var rewritten = property.ReplaceNode(returned, readOnlyView).WithType(readOnlyType);
            type = type.ReplaceNode(property, rewritten);
            index = type.Members.IndexOf(type.Members.OfType<PropertyDeclarationSyntax>().First(p => p.Identifier.ValueText == propertyName)) + 1;
        }
        else
        {
            index = type.Members.IndexOf(type.Members.OfType<FieldDeclarationSyntax>().Last()) + 1;
        }

        var indentation = FieldPropertyRefactoring.Indentation(type.Members[0]);
        var placed = members.Select(m => m.WithLeadingTrivia(m.GetLeadingTrivia().AddRange(indentation)));
        return type.WithMembers(type.Members.InsertRange(index, placed));
    }

    /// <summary>
    /// A caller that used the list in a way a read-only view does not allow,
    /// such as <c>Clear</c> or passing it where a <c>List&lt;T&gt;</c> is
    /// expected, no longer compiles. Such a change is refused.
    /// </summary>
    private static async Task EnsureStillCompiles(Solution before, Solution after, string propertyName)
    {
        foreach (var projectId in after.GetChanges(before).GetProjectChanges().Select(p => p.ProjectId))
        {
            var beforeErrors = (await before.GetProject(projectId)!.GetCompilationAsync())!.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.Id + d.GetMessage())
                .ToList();
            var introduced = (await after.GetProject(projectId)!.GetCompilationAsync())!.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .FirstOrDefault(d => !beforeErrors.Remove(d.Id + d.GetMessage()));
            if (introduced is not null)
                throw new McpException($"Error: Code uses '{propertyName}' in a way a read-only list does not allow, at {introduced.Location.GetLineSpan()}: {introduced.GetMessage()}");
        }
    }
}
