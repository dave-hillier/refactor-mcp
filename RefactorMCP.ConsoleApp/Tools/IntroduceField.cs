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
            return RefactoringHelpers.ThrowMcpException("Error: Invalid selection range format");

        var startPosition = sourceText.Lines[startLine - 1].Start + startColumn - 1;
        var endPosition = sourceText.Lines[endLine - 1].Start + endColumn - 1;
        var span = TextSpan.FromBounds(startPosition, endPosition);

        var selectedExpression = syntaxRoot!.DescendantNodes()
            .OfType<ExpressionSyntax>()
            .FirstOrDefault(e => span.Contains(e.Span) || e.Span.Contains(span));

        if (selectedExpression == null)
            return RefactoringHelpers.ThrowMcpException("Error: Selected code is not a valid expression");

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
                return RefactoringHelpers.ThrowMcpException($"Error: Field '{fieldName}' already exists");
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

    private static Task<string> IntroduceFieldSingleFile(string filePath, string selectionRange, string fieldName, string accessModifier)
    {
        return RefactoringHelpers.ApplySingleFileEdit(
            filePath,
            text => IntroduceFieldInSource(text, selectionRange, fieldName, accessModifier),
            $"Successfully introduced {accessModifier} field '{fieldName}' from {selectionRange} in {filePath} (single file mode)");
    }

    public static string IntroduceFieldInSource(string sourceText, string selectionRange, string fieldName, string accessModifier)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = syntaxTree.GetRoot();
        var textLines = SourceText.From(sourceText).Lines;

        if (!RefactoringHelpers.TryParseRange(selectionRange, out var startLine, out var startColumn, out var endLine, out var endColumn))
            return RefactoringHelpers.ThrowMcpException("Error: Invalid selection range format");

        var startPosition = textLines[startLine - 1].Start + startColumn - 1;
        var endPosition = textLines[endLine - 1].Start + endColumn - 1;
        var span = TextSpan.FromBounds(startPosition, endPosition);

        var selectedExpression = syntaxRoot.DescendantNodes()
            .OfType<ExpressionSyntax>()
            .FirstOrDefault(e => span.Contains(e.Span) || e.Span.Contains(span));

        if (selectedExpression == null)
            return RefactoringHelpers.ThrowMcpException("Error: Selected code is not a valid expression");

        // In single file mode, use 'var' for type since we don't have semantic analysis
        var typeName = "var";

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
                return RefactoringHelpers.ThrowMcpException($"Error: Field '{fieldName}' already exists");
        }
        var rewriter = new FieldIntroductionRewriter(selectedExpression, fieldReference, fieldDeclaration, containingClass);
        var newRoot = rewriter.Visit(syntaxRoot);

        var formattedRoot = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
        return formattedRoot.ToFullString();
    }

}
