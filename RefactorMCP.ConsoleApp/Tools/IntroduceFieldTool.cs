using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Editing;
using System.Linq;

[McpServerToolType]
public static class IntroduceFieldTool
{
    [McpServerTool, Description("Introduce a new field from selected expression (preferred for large C# file refactoring)")]
    public static async Task<string> IntroduceField(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Range in format 'startLine:startColumn-endLine:endColumn'")] string selectionRange,
        [Description("Name for the new field")] string fieldName,
        [Description("Access modifier (private, public, protected, internal)")] string accessModifier = "private")
    {
        try
        {
            return await RefactoringHelpers.RunWithSolutionOrFile(
                solutionPath,
                filePath,
                doc => IntroduceFieldWithSolution(doc, selectionRange, fieldName, accessModifier),
                path => IntroduceFieldSingleFile(path, selectionRange, fieldName, accessModifier));
        }
        catch (Exception ex)
        {
            throw new McpException($"Error introducing field: {ex.Message}", ex);
        }
    }

    private static async Task<string> IntroduceFieldWithSolution(Document document, string selectionRange, string fieldName, string accessModifier)
    {
        var sourceText = await document.GetTextAsync();
        var syntaxRoot = await document.GetSyntaxRootAsync();

        if (!RefactoringHelpers.TryParseRange(selectionRange, out var startLine, out var startColumn, out var endLine, out var endColumn))
            throw new McpException("Error: Invalid selection range format");

        if (!RefactoringHelpers.ValidateRange(sourceText, startLine, startColumn, endLine, endColumn, out var error))
            throw new McpException(error);

        var startPosition = sourceText.Lines[startLine - 1].Start + startColumn - 1;
        var endPosition = sourceText.Lines[endLine - 1].Start + endColumn - 1;
        var span = TextSpan.FromBounds(startPosition, endPosition);

        var selectedExpression = syntaxRoot!.DescendantNodes()
            .OfType<ExpressionSyntax>()
            .FirstOrDefault(e => span.Contains(e.Span) || e.Span.Contains(span));

        if (selectedExpression == null)
            throw new McpException("Error: Selected code is not a valid expression");

        // Get the semantic model to determine the type
        var semanticModel = await document.GetSemanticModelAsync();
        var typeInfo = semanticModel!.GetTypeInfo(selectedExpression);
        var typeName = typeInfo.Type?.ToDisplayString() ?? "var";

        // Create the field declaration
        var accessModifierToken = accessModifier.ToLower() switch
        {
            "public" => SyntaxFactory.Token(SyntaxKind.PublicKeyword),
            "protected" => SyntaxFactory.Token(SyntaxKind.ProtectedKeyword),
            "internal" => SyntaxFactory.Token(SyntaxKind.InternalKeyword),
            _ => SyntaxFactory.Token(SyntaxKind.PrivateKeyword)
        };

        var fieldDeclaration = SyntaxFactory.FieldDeclaration(
            SyntaxFactory.VariableDeclaration(
                SyntaxFactory.IdentifierName(typeName))
            .WithVariables(SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.VariableDeclarator(fieldName)
                .WithInitializer(SyntaxFactory.EqualsValueClause(selectedExpression)))))
            .WithModifiers(SyntaxFactory.TokenList(accessModifierToken));

        var fieldReference = SyntaxFactory.IdentifierName(fieldName);
        var containingClass = selectedExpression.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (containingClass != null)
        {
            var classSymbol = semanticModel.GetDeclaredSymbol(containingClass);
            if (classSymbol?.GetMembers().OfType<IFieldSymbol>().Any(f => f.Name == fieldName) == true)
                return $"Error: Field '{fieldName}' already exists";
        }
        var rewriter = new FieldIntroductionRewriter(selectedExpression, fieldReference, fieldDeclaration, containingClass);
        var newRoot = rewriter.Visit(syntaxRoot);

        var formattedRoot = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        var editor = await DocumentEditor.CreateAsync(document);
        editor.ReplaceNode(syntaxRoot, formattedRoot);
        var newDocument = editor.GetChangedDocument();
        var newText = await newDocument.GetTextAsync();
        var encoding = await RefactoringHelpers.GetFileEncodingAsync(document.FilePath!);
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString(), encoding);
        RefactoringHelpers.UpdateSolutionCache(newDocument);

        return $"Successfully introduced {accessModifier} field '{fieldName}' from {selectionRange} in {document.FilePath} (solution mode)";
    }

    private static async Task<string> IntroduceFieldSingleFile(string filePath, string selectionRange, string fieldName, string accessModifier)
    {
        if (!File.Exists(filePath))
            throw new McpException($"Error: File {filePath} not found");

        var (sourceText, encoding) = await RefactoringHelpers.ReadFileWithEncodingAsync(filePath);
        var model = await RefactoringHelpers.GetOrCreateSemanticModelAsync(filePath);
        var newText = IntroduceFieldInSource(sourceText, selectionRange, fieldName, accessModifier, model);
        if (newText.StartsWith("Error:"))
            return newText;

        await File.WriteAllTextAsync(filePath, newText, encoding);
        RefactoringHelpers.UpdateFileCaches(filePath, newText);
        return $"Successfully introduced {accessModifier} field '{fieldName}' from {selectionRange} in {filePath} (single file mode)";
    }

    public static string IntroduceFieldInSource(string sourceText, string selectionRange, string fieldName, string accessModifier, SemanticModel? model = null)
    {
        var syntaxTree = model?.SyntaxTree ?? CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = syntaxTree.GetRoot();
        var text = syntaxTree.GetText();
        var textLines = text.Lines;

        if (!RefactoringHelpers.TryParseRange(selectionRange, out var startLine, out var startColumn, out var endLine, out var endColumn))
            throw new McpException("Error: Invalid selection range format");

        if (!RefactoringHelpers.ValidateRange(text, startLine, startColumn, endLine, endColumn, out var error))
            throw new McpException(error);

        var startPosition = textLines[startLine - 1].Start + startColumn - 1;
        var endPosition = textLines[endLine - 1].Start + endColumn - 1;
        var span = TextSpan.FromBounds(startPosition, endPosition);

        var selectedExpression = syntaxRoot.DescendantNodes()
            .OfType<ExpressionSyntax>()
            .FirstOrDefault(e => span.Contains(e.Span) || e.Span.Contains(span));

        if (selectedExpression == null)
            throw new McpException("Error: Selected code is not a valid expression");

        var typeName = "var";
        if (model != null)
        {
            var typeInfo = model.GetTypeInfo(selectedExpression);
            if (typeInfo.Type != null)
                typeName = typeInfo.Type.ToDisplayString();
        }

        // Create the field declaration
        var accessModifierToken = accessModifier.ToLower() switch
        {
            "public" => SyntaxFactory.Token(SyntaxKind.PublicKeyword),
            "protected" => SyntaxFactory.Token(SyntaxKind.ProtectedKeyword),
            "internal" => SyntaxFactory.Token(SyntaxKind.InternalKeyword),
            _ => SyntaxFactory.Token(SyntaxKind.PrivateKeyword)
        };

        var fieldDeclaration = SyntaxFactory.FieldDeclaration(
            SyntaxFactory.VariableDeclaration(
                SyntaxFactory.IdentifierName(typeName))
            .WithVariables(SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.VariableDeclarator(fieldName)
                .WithInitializer(SyntaxFactory.EqualsValueClause(selectedExpression)))))
            .WithModifiers(SyntaxFactory.TokenList(accessModifierToken));

        var fieldReference = SyntaxFactory.IdentifierName(fieldName);
        var containingClass = selectedExpression.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (containingClass != null)
        {
            var exists = containingClass.Members
                .OfType<FieldDeclarationSyntax>()
                .SelectMany(f => f.Declaration.Variables)
                .Any(v => v.Identifier.ValueText == fieldName);
            if (exists)
                return $"Error: Field '{fieldName}' already exists";
        }
        var rewriter = new FieldIntroductionRewriter(selectedExpression, fieldReference, fieldDeclaration, containingClass);
        var newRoot = rewriter.Visit(syntaxRoot);

        var formattedRoot = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
        return formattedRoot.ToFullString();
    }

}
