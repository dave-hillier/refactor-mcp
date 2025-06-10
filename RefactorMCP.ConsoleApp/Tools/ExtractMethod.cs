using ModelContextProtocol.Server;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

public static partial class RefactoringTools
{
    public static async Task<string> ExtractMethod(
        [Description("Path to the C# file")] string filePath,
        [Description("Range in format 'startLine:startColumn-endLine:endColumn'")] string selectionRange,
        [Description("Name for the new method")] string methodName,
        [Description("Path to the solution file (.sln) - optional for single file mode")] string? solutionPath = null)
    {
        try
        {
            if (solutionPath != null)
            {
                // Solution mode - full semantic analysis
                var solution = await GetOrLoadSolution(solutionPath);
                var document = GetDocumentByPath(solution, filePath);
                if (document != null)
                    return await ExtractMethodWithSolution(document, selectionRange, methodName);

                // Fallback to single file mode when file isn't part of the solution
                return await ExtractMethodSingleFile(filePath, selectionRange, methodName);
            }

            // Single file mode - direct syntax tree manipulation
            return await ExtractMethodSingleFile(filePath, selectionRange, methodName);
        }
        catch (Exception ex)
        {
            return $"Error extracting method: {ex.Message}";
        }
    }

    private static async Task<string> ExtractMethodWithSolution(Document document, string selectionRange, string methodName)
    {
        var sourceText = await document.GetTextAsync();
        var syntaxRoot = await document.GetSyntaxRootAsync();

        if (!TryParseRange(selectionRange, out var startLine, out var startColumn, out var endLine, out var endColumn))
            return "Error: Invalid selection range format. Use 'startLine:startColumn-endLine:endColumn'";

        var startPosition = sourceText.Lines[startLine - 1].Start + startColumn - 1;
        var endPosition = sourceText.Lines[endLine - 1].Start + endColumn - 1;
        var span = TextSpan.FromBounds(startPosition, endPosition);

        var selectedNodes = syntaxRoot!.DescendantNodes()
            .Where(n => span.Contains(n.Span))
            .ToList();

        if (!selectedNodes.Any())
            return "Error: No valid code selected";

        var statementsToExtract = selectedNodes
            .OfType<StatementSyntax>()
            .Where(s => span.IntersectsWith(s.Span))
            .ToList();

        if (!statementsToExtract.Any())
            return "Error: Selected code does not contain extractable statements";

        // Find the containing method
        var containingMethod = selectedNodes.First().Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (containingMethod == null)
            return "Error: Selected code is not within a method";

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

        var newRoot = syntaxRoot;
        foreach (var statement in statementsToExtract.Skip(1))
        {
            newRoot = newRoot?.RemoveNode(statement, SyntaxRemoveOptions.KeepNoTrivia);
        }
        if (statementsToExtract.Any() && newRoot != null)
        {
            newRoot = newRoot.ReplaceNode(statementsToExtract.First(), methodCall);
        }

        // Add the new method to the class
        var containingClass = containingMethod.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (containingClass != null)
        {
            var updatedClass = containingClass.AddMembers(newMethod);
            newRoot = newRoot!.ReplaceNode(containingClass, updatedClass);
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
            return $"Error: File {filePath} not found";

        var sourceText = await File.ReadAllTextAsync(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = await syntaxTree.GetRootAsync();
        var textLines = SourceText.From(sourceText).Lines;

        if (!TryParseRange(selectionRange, out var startLine, out var startColumn, out var endLine, out var endColumn))
            return "Error: Invalid selection range format. Use 'startLine:startColumn-endLine:endColumn'";

        var startPosition = textLines[startLine - 1].Start + startColumn - 1;
        var endPosition = textLines[endLine - 1].Start + endColumn - 1;
        var span = TextSpan.FromBounds(startPosition, endPosition);

        var selectedNodes = syntaxRoot.DescendantNodes()
            .Where(n => span.Contains(n.Span))
            .ToList();

        if (!selectedNodes.Any())
            return "Error: No valid code selected";

        var statementsToExtract = selectedNodes
            .OfType<StatementSyntax>()
            .Where(s => span.IntersectsWith(s.Span))
            .ToList();

        if (!statementsToExtract.Any())
            return "Error: Selected code does not contain extractable statements";

        // Find the containing method
        var containingMethod = selectedNodes.First().Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (containingMethod == null)
            return "Error: Selected code is not within a method";

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

        var newRoot = syntaxRoot;
        foreach (var statement in statementsToExtract.Skip(1))
        {
            newRoot = newRoot.RemoveNode(statement, SyntaxRemoveOptions.KeepNoTrivia);
        }
        if (statementsToExtract.Any())
        {
            newRoot = newRoot.ReplaceNode(statementsToExtract.First(), methodCall);
        }

        // Add the new method to the class
        var containingClass = containingMethod.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (containingClass != null)
        {
            var currentClass = newRoot.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.Text == containingClass.Identifier.Text);

            if (currentClass != null)
            {
                var updatedClass = currentClass.AddMembers(newMethod);
                newRoot = newRoot.ReplaceNode(currentClass, updatedClass);
            }
        }

        // Format and write back to file
        var workspace = new AdhocWorkspace();
        var formattedRoot = Formatter.Format(newRoot, workspace);
        await File.WriteAllTextAsync(filePath, formattedRoot.ToFullString());

        return $"Successfully extracted method '{methodName}' from {selectionRange} in {filePath} (single file mode)";
    }

}
