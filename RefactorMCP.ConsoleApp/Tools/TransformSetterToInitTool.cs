using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;

[McpServerToolType]
public static class TransformSetterToInitTool
{
    [McpServerTool, Description("Convert property setter to init-only setter (preferred for large C# file refactoring)")]
    public static async Task<string> TransformSetterToInit(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Name of the property to transform, optionally qualified by its type as Type.Property")] string propertyName)
    {
        try
        {
            return await RefactoringHelpers.RunWithSolutionOrFile(
                solutionPath,
                filePath,
                doc => TransformSetterToInitWithSolution(doc, propertyName),
                path => TransformSetterToInitSingleFile(path, propertyName));
        }
        catch (Exception ex)
        {
            throw new McpException($"Error transforming setter: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Turns the setter into an init accessor when nothing sets the property
    /// after construction: every assignment is in an object initialiser, a
    /// <c>with</c> expression, or a constructor or init accessor of the type
    /// or a type derived from it.
    /// </summary>
    private static async Task<string> TransformSetterToInitWithSolution(Document document, string propertyName)
    {
        var property = await FieldPropertyRefactoring.FindPropertyAsync(document, propertyName);
        propertyName = property.Name;
        var declaration = await FieldPropertyRefactoring.DeclarationAsync<PropertyDeclarationSyntax>(property);
        if (declaration.AccessorList?.Accessors.Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration)) != true)
            throw new McpException($"Error: Property '{propertyName}' has no setter");
        if (property.IsVirtual || property.IsAbstract || property.IsOverride)
            throw new McpException($"Error: Property '{propertyName}' is virtual, abstract or an override, so its hierarchy would have to change too");

        var solution = document.Project.Solution;
        foreach (var location in (await SymbolFinder.FindReferencesAsync(property, solution)).SelectMany(r => r.Locations))
        {
            var root = (await location.Document.GetSyntaxRootAsync())!;
            var name = (ExpressionSyntax)root.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true);
            if (FieldPropertyRefactoring.IsWrite(name) && !MayUseInit(name, property, (await location.Document.GetSemanticModelAsync())!))
                throw new McpException($"Error: '{propertyName}' is assigned after construction at {location.Location.GetLineSpan()}, which an init accessor does not allow");
        }

        var declaringDocument = solution.GetDocument(declaration.SyntaxTree)!;
        var editor = await DocumentEditor.CreateAsync(declaringDocument);
        editor.ReplaceNode(declaration, new SetterToInitRewriter(propertyName).Visit(declaration)!);

        await FieldPropertyRefactoring.WriteChangesAsync(solution, editor.GetChangedDocument().Project.Solution);
        return $"Successfully converted setter to init for '{propertyName}' in {declaringDocument.FilePath} (solution mode)";
    }

    private static bool MayUseInit(ExpressionSyntax name, IPropertySymbol property, SemanticModel model)
    {
        var reference = FieldPropertyRefactoring.ReferenceExpression(name);
        if (reference.Parent is AssignmentExpressionSyntax { Parent: InitializerExpressionSyntax initializer } assignment
            && assignment.Left == reference
            && (initializer.IsKind(SyntaxKind.ObjectInitializerExpression) || initializer.IsKind(SyntaxKind.WithInitializerExpression)))
            return true;

        if (FieldPropertyRefactoring.ConstructingMember(name, property.IsStatic)?.Parent is not TypeDeclarationSyntax type)
            return false;

        var receiverIsThis = reference is not MemberAccessExpressionSyntax access
            || access.Expression is ThisExpressionSyntax or BaseExpressionSyntax;
        for (var candidate = model.GetDeclaredSymbol(type); candidate is not null; candidate = candidate.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(candidate, property.ContainingType))
                return receiverIsThis;
        }
        return false;
    }

    private static Task<string> TransformSetterToInitSingleFile(string filePath, string propertyName)
    {
        return RefactoringHelpers.ApplySingleFileEdit(
            filePath,
            text => TransformSetterToInitInSource(text, propertyName),
            $"Successfully converted setter to init for '{propertyName}' in {filePath} (single file mode)");
    }

    public static string TransformSetterToInitInSource(string sourceText, string propertyName)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = syntaxTree.GetRoot();

        var property = syntaxRoot.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .FirstOrDefault(p => p.Identifier.ValueText == propertyName);
        if (property == null)
            throw new McpException($"Error: No property named '{propertyName}' found");

        var setter = property.AccessorList?.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.SetAccessorDeclaration));
        if (setter == null)
            throw new McpException($"Error: Property '{propertyName}' has no setter");

        var rewriter = new SetterToInitRewriter(propertyName);
        var newRoot = rewriter.Visit(syntaxRoot);
        var formatted = Formatter.Format(newRoot!, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }
}
