using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

[McpServerToolType]
public static class ExtractMethodTool
{
    public static async Task<string> ExtractMethod(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Range in format 'startLine:startColumn-endLine:endColumn'")] string selectionRange,
        [Description("Name for the new method")] string methodName)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath);
            var document = RefactoringHelpers.GetDocumentByPath(solution, filePath);
            if (document != null)
                return await ExtractMethodWithSolution(document, selectionRange, methodName);

            return await ExtractMethodSingleFile(filePath, selectionRange, methodName);
        }
        catch (Exception ex)
        {
            throw new McpException($"Error extracting method: {ex.Message}", ex);
        }
    }

    private static async Task<string> ExtractMethodWithSolution(Document document, string selectionRange, string methodName)
    {
        var sourceText = await document.GetTextAsync();
        var syntaxRoot = await document.GetSyntaxRootAsync();

        if (!RefactoringHelpers.TryParseRange(selectionRange, out var startLine, out var startColumn, out var endLine, out var endColumn))
            return RefactoringHelpers.ThrowMcpException("Error: Invalid selection range format. Use 'startLine:startColumn-endLine:endColumn'");

        var startPosition = sourceText.Lines[startLine - 1].Start + startColumn - 1;
        var endPosition = sourceText.Lines[endLine - 1].Start + endColumn - 1;
        var span = TextSpan.FromBounds(startPosition, endPosition);

        var selectedNodes = syntaxRoot!.DescendantNodes()
            .Where(n => span.Contains(n.Span))
            .ToList();

        if (!selectedNodes.Any())
            return RefactoringHelpers.ThrowMcpException("Error: No valid code selected");

        var containingMethod = selectedNodes.First().Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (containingMethod == null)
            return RefactoringHelpers.ThrowMcpException("Error: Selected code is not within a method");

        var statementsToExtract = containingMethod.Body!.Statements
            .Where(s => span.IntersectsWith(s.FullSpan))
            .ToList();

        if (!statementsToExtract.Any())
            return RefactoringHelpers.ThrowMcpException("Error: Selected code does not contain extractable statements");

        // Create the new method
        var newMethod = SyntaxFactory.MethodDeclaration(
            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
            methodName)
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword)))
            .WithBody(SyntaxFactory.Block(statementsToExtract));

        // Replace selected statements with method call
        var methodCall = SyntaxFactory.ExpressionStatement(
            SyntaxFactory.InvocationExpression(
                SyntaxFactory.IdentifierName(methodName)));

        var methodBody = containingMethod.Body!;
        var updatedBody = methodBody.ReplaceNode(statementsToExtract.First(), methodCall);
        foreach (var statement in statementsToExtract.Skip(1))
            updatedBody = updatedBody.RemoveNode(statement, SyntaxRemoveOptions.KeepNoTrivia);
        var updatedMethod = containingMethod.WithBody(updatedBody);

        var containingClass = containingMethod.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        SyntaxNode newRoot = syntaxRoot;
        if (containingClass != null)
        {
            var classWithUpdatedMethod = containingClass.ReplaceNode(containingMethod, updatedMethod);
            var updatedClass = classWithUpdatedMethod.AddMembers(newMethod);
            newRoot = syntaxRoot.ReplaceNode(containingClass, updatedClass);
        }

        var formattedRoot = Formatter.Format(newRoot!, document.Project.Solution.Workspace);
        var newDocument = document.WithSyntaxRoot(formattedRoot);

        // Write the changes back to the file
        var newText = await newDocument.GetTextAsync();
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString());

        return $"Successfully extracted method '{methodName}' from {selectionRange} in {document.FilePath} (solution mode)";
    }

    private static async Task<string> ExtractMethodSingleFile(string filePath, string selectionRange, string methodName)
    {
        if (!File.Exists(filePath))
            return RefactoringHelpers.ThrowMcpException($"Error: File {filePath} not found (current dir: {Directory.GetCurrentDirectory()})");

        var sourceText = await File.ReadAllTextAsync(filePath);
        var newText = ExtractMethodInSource(sourceText, selectionRange, methodName);
        await File.WriteAllTextAsync(filePath, newText);

        return $"Successfully extracted method '{methodName}' from {selectionRange} in {filePath} (single file mode)";
    }

    public static string ExtractMethodInSource(string sourceText, string selectionRange, string methodName)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = syntaxTree.GetRoot();
        var textLines = SourceText.From(sourceText).Lines;

        if (!RefactoringHelpers.TryParseRange(selectionRange, out var startLine, out var startColumn, out var endLine, out var endColumn))
            return RefactoringHelpers.ThrowMcpException("Error: Invalid selection range format. Use 'startLine:startColumn-endLine:endColumn'");

        var startPosition = textLines[startLine - 1].Start + startColumn - 1;
        var endPosition = textLines[endLine - 1].Start + endColumn - 1;
        var span = TextSpan.FromBounds(startPosition, endPosition);

        var selectedNodes = syntaxRoot.DescendantNodes()
            .Where(n => span.Contains(n.Span))
            .ToList();

        if (!selectedNodes.Any())
            return RefactoringHelpers.ThrowMcpException("Error: No valid code selected");

        var containingMethod = selectedNodes.First().Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (containingMethod == null)
            return RefactoringHelpers.ThrowMcpException("Error: Selected code is not within a method");

        var statementsToExtract = containingMethod.Body!.Statements
            .Where(s => span.IntersectsWith(s.FullSpan))
            .ToList();

        if (!statementsToExtract.Any())
            return RefactoringHelpers.ThrowMcpException("Error: Selected code does not contain extractable statements");

        // Create the new method
        var newMethod = SyntaxFactory.MethodDeclaration(
            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
            methodName)
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword)))
            .WithBody(SyntaxFactory.Block(statementsToExtract));

        // Replace selected statements with method call
        var methodCall = SyntaxFactory.ExpressionStatement(
            SyntaxFactory.InvocationExpression(
                SyntaxFactory.IdentifierName(methodName)));

        var methodBody = containingMethod.Body!;
        var updatedBody = methodBody.ReplaceNode(statementsToExtract.First(), methodCall);
        foreach (var statement in statementsToExtract.Skip(1))
            updatedBody = updatedBody.RemoveNode(statement, SyntaxRemoveOptions.KeepNoTrivia);
        var updatedMethod = containingMethod.WithBody(updatedBody);

        var containingClass = containingMethod.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        SyntaxNode newRoot = syntaxRoot;
        if (containingClass != null)
        {
            var classWithUpdatedMethod = containingClass.ReplaceNode(containingMethod, updatedMethod);
            var updatedClass = classWithUpdatedMethod.AddMembers(newMethod);
            newRoot = syntaxRoot.ReplaceNode(containingClass, updatedClass);
        }

        var formattedRoot = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
        return formattedRoot.ToFullString();
    }

}
