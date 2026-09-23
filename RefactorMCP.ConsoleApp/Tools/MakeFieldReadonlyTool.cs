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
using System.Threading;

[McpServerToolType]
public static class MakeFieldReadonlyTool
{
    [McpServerTool, Description("Make a field readonly if assigned only during initialization (preferred for large C# file refactoring)")]
    public static async Task<string> MakeFieldReadonly(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Name of the field to make readonly, optionally qualified by its type as Type.field")] string fieldName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await RefactoringHelpers.RunWithSolutionOrFile(
                solutionPath,
                filePath,
                doc => MakeFieldReadonlyWithSolution(doc, fieldName),
                path => MakeFieldReadonlySingleFile(path, fieldName));
        }
        catch (Exception ex)
        {
            throw new McpException($"Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Adds <c>readonly</c> when every assignment of the field happens while
    /// an object of its type is constructed. The initialiser stays where it
    /// is: a readonly field may keep one, and moving it would change when it
    /// runs.
    /// </summary>
    private static async Task<string> MakeFieldReadonlyWithSolution(Document document, string fieldName)
    {
        var field = await FieldPropertyRefactoring.FindFieldAsync(document, fieldName);
        fieldName = field.Name;
        if (field.IsConst)
            throw new McpException($"Error: '{fieldName}' is a constant, which is already immutable");
        if (field.IsReadOnly)
            return $"Field '{fieldName}' is already readonly";
        if (field.IsVolatile)
            throw new McpException($"Error: '{fieldName}' is volatile, and a field cannot be both volatile and readonly");
        if (IsMutableStruct(field.Type))
            throw new McpException($"Error: '{fieldName}' holds the mutable struct '{field.Type.Name}', whose methods would change a copy once the field is readonly");

        var variable = await FieldPropertyRefactoring.DeclarationAsync<VariableDeclaratorSyntax>(field);
        var declaration = (FieldDeclarationSyntax)variable.Parent!.Parent!;
        if (declaration.Declaration.Variables.Count > 1)
            throw new McpException($"Error: '{fieldName}' is declared alongside other fields, which would become readonly too; declare it on its own first");

        var solution = document.Project.Solution;
        foreach (var location in (await SymbolFinder.FindReferencesAsync(field, solution)).SelectMany(r => r.Locations))
        {
            var root = (await location.Document.GetSyntaxRootAsync())!;
            var name = (ExpressionSyntax)root.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true);
            if (FieldPropertyRefactoring.IsWrite(name) && !IsWrittenDuringConstruction(name, field, (await location.Document.GetSemanticModelAsync())!))
                throw new McpException($"Error: '{fieldName}' is assigned outside a constructor at {location.Location.GetLineSpan()}");
        }

        var declaringDocument = solution.GetDocument(declaration.SyntaxTree)!;
        var editor = await DocumentEditor.CreateAsync(declaringDocument);
        editor.ReplaceNode(declaration, WithReadonly(declaration));

        await FieldPropertyRefactoring.WriteChangesAsync(solution, editor.GetChangedDocument().Project.Solution);
        return $"Successfully made field '{fieldName}' readonly in {declaringDocument.FilePath}";
    }

    /// <summary>
    /// A write a readonly field allows: in a constructor or init accessor of
    /// the declaring type itself, through <c>this</c> rather than another
    /// instance.
    /// </summary>
    private static bool IsWrittenDuringConstruction(ExpressionSyntax name, IFieldSymbol field, SemanticModel model)
    {
        var member = FieldPropertyRefactoring.ConstructingMember(name, field.IsStatic);
        if (member?.Parent is not TypeDeclarationSyntax type
            || !SymbolEqualityComparer.Default.Equals(model.GetDeclaredSymbol(type), field.ContainingType))
            return false;

        return FieldPropertyRefactoring.ReferenceExpression(name) switch
        {
            MemberAccessExpressionSyntax access => access.Expression is ThisExpressionSyntax || field.IsStatic,
            _ => true,
        };
    }

    /// <summary>
    /// A struct declared in the solution that is not readonly and has a field
    /// that is not readonly. Calling its members through a readonly field
    /// would act on a defensive copy.
    /// </summary>
    private static bool IsMutableStruct(ITypeSymbol type) =>
        type is INamedTypeSymbol { TypeKind: TypeKind.Struct, IsReadOnly: false } named
        && named.Locations.Any(l => l.IsInSource)
        && named.GetMembers().OfType<IFieldSymbol>().Any(f => !f.IsStatic && !f.IsReadOnly && !f.IsConst);

    /// <summary>
    /// Places <c>readonly</c> after the access and <c>static</c> modifiers, as
    /// C# style orders them, or first, taking over the declaration's leading
    /// trivia, when there are none.
    /// </summary>
    private static FieldDeclarationSyntax WithReadonly(FieldDeclarationSyntax declaration)
    {
        var readonlyToken = SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword).WithTrailingTrivia(SyntaxFactory.Space);
        var modifiers = declaration.Modifiers;
        var index = modifiers.Count;
        while (index > 0 && !IsAccessOrStatic(modifiers[index - 1]))
            index--;

        if (index > 0)
            return declaration.WithModifiers(modifiers.Insert(index, readonlyToken));

        var leading = declaration.GetLeadingTrivia();
        return declaration
            .WithoutLeadingTrivia()
            .WithModifiers(declaration.WithoutLeadingTrivia().Modifiers.Insert(0, readonlyToken.WithLeadingTrivia(leading)));
    }

    private static bool IsAccessOrStatic(SyntaxToken modifier) =>
        modifier.IsKind(SyntaxKind.PublicKeyword) || modifier.IsKind(SyntaxKind.PrivateKeyword)
        || modifier.IsKind(SyntaxKind.ProtectedKeyword) || modifier.IsKind(SyntaxKind.InternalKeyword)
        || modifier.IsKind(SyntaxKind.StaticKeyword) || modifier.IsKind(SyntaxKind.NewKeyword);

    private static Task<string> MakeFieldReadonlySingleFile(string filePath, string fieldName)
    {
        return RefactoringHelpers.ApplySingleFileEdit(
            filePath,
            text => MakeFieldReadonlyInSource(text, fieldName),
            $"Successfully made field '{fieldName}' readonly in {filePath} (single file mode)");
    }

    public static string MakeFieldReadonlyInSource(string sourceText, string fieldName)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = syntaxTree.GetRoot();

        var fieldDeclaration = syntaxRoot.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .FirstOrDefault(f => f.Declaration.Variables.Any(v => v.Identifier.ValueText == fieldName));

        if (fieldDeclaration == null)
            throw new McpException($"Error: No field named '{fieldName}' found");

        var rewriter = new ReadonlyFieldRewriter(fieldName);
        var newRoot = rewriter.Visit(syntaxRoot);

        var formattedRoot = Formatter.Format(newRoot!, RefactoringHelpers.SharedWorkspace);
        return formattedRoot.ToFullString();
    }
}
