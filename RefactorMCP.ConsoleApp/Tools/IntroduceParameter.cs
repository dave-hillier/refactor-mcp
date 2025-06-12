using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

[McpServerToolType]
public static class IntroduceParameterTool
{
    private static async Task<string> IntroduceParameterWithSolution(Document document, string methodName, string selectionRange, string parameterName)
    {
        var sourceText = await document.GetTextAsync();
        var syntaxRoot = await document.GetSyntaxRootAsync();
        var textLines = sourceText.Lines;

        var method = syntaxRoot!.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            return RefactoringHelpers.ThrowMcpException($"Error: No method named '{methodName}' found");

        if (!RefactoringHelpers.TryParseRange(selectionRange, out var startLine, out var startColumn, out var endLine, out var endColumn))
            return RefactoringHelpers.ThrowMcpException("Error: Invalid selection range format");

        var startPosition = textLines[startLine - 1].Start + startColumn - 1;
        var endPosition = textLines[endLine - 1].Start + endColumn - 1;
        var span = TextSpan.FromBounds(startPosition, endPosition);

        var selectedExpression = syntaxRoot.DescendantNodes(span).OfType<ExpressionSyntax>().FirstOrDefault();
        if (selectedExpression == null)
            return RefactoringHelpers.ThrowMcpException("Error: Selected code is not a valid expression");

        var semanticModel = await document.GetSemanticModelAsync();
        var typeInfo = semanticModel!.GetTypeInfo(selectedExpression);
        var typeName = typeInfo.Type?.ToDisplayString() ?? "object";

        var parameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameterName))
            .WithType(SyntaxFactory.ParseTypeName(typeName));

        var invocations = syntaxRoot.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(i =>
                (i.Expression is IdentifierNameSyntax id && id.Identifier.ValueText == methodName) ||
                (i.Expression is MemberAccessExpressionSyntax ma && ma.Name.Identifier.ValueText == methodName))
            .ToList();

        foreach (var invocation in invocations)
        {
            var newArgs = invocation.ArgumentList.AddArguments(SyntaxFactory.Argument(selectedExpression.WithoutTrivia()));
            syntaxRoot = syntaxRoot.ReplaceNode(invocation, invocation.WithArgumentList(newArgs));
        }

        method = syntaxRoot.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.ValueText == methodName);
        selectedExpression = syntaxRoot.DescendantNodes()
            .OfType<ExpressionSyntax>()
            .Where(e => span.Contains(e.Span) || e.Span.Contains(span))
            .OrderBy(e => Math.Abs(e.Span.Length - span.Length))
            .ThenBy(e => e.Span.Length)
            .First();

        var newMethod = method.ReplaceNode(selectedExpression, SyntaxFactory.IdentifierName(parameterName))
            .AddParameterListParameters(parameter);
        SyntaxNode newRoot = syntaxRoot.ReplaceNode(method, newMethod);

        var formattedRoot = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        var newDocument = document.WithSyntaxRoot(formattedRoot);
        var newText = await newDocument.GetTextAsync();
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString());

        return $"Successfully introduced parameter '{parameterName}' from {selectionRange} in method '{methodName}' in {document.FilePath} (solution mode)";
    }

    private static Task<string> IntroduceParameterSingleFile(string filePath, string methodName, string selectionRange, string parameterName)
    {
        return RefactoringHelpers.ApplySingleFileEdit(
            filePath,
            text => IntroduceParameterInSource(text, methodName, selectionRange, parameterName),
            $"Successfully introduced parameter '{parameterName}' from {selectionRange} in method '{methodName}' in {filePath} (single file mode)");
    }

    public static string IntroduceParameterInSource(string sourceText, string methodName, string selectionRange, string parameterName)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = syntaxTree.GetRoot();
        var textLines = SourceText.From(sourceText).Lines;

        var method = syntaxRoot.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            return RefactoringHelpers.ThrowMcpException($"Error: No method named '{methodName}' found");

        if (!RefactoringHelpers.TryParseRange(selectionRange, out var startLine, out var startColumn, out var endLine, out var endColumn))
            return RefactoringHelpers.ThrowMcpException("Error: Invalid selection range format");

        var startPosition = textLines[startLine - 1].Start + startColumn - 1;
        var endPosition = textLines[endLine - 1].Start + endColumn - 1;
        var span = TextSpan.FromBounds(startPosition, endPosition);

        var selectedExpression = syntaxRoot.DescendantNodes()
            .OfType<ExpressionSyntax>()
            .Where(e => span.Contains(e.Span) || e.Span.Contains(span))
            .OrderBy(e => Math.Abs(e.Span.Length - span.Length))
            .ThenBy(e => e.Span.Length)
            .FirstOrDefault();
        if (selectedExpression == null)
            return RefactoringHelpers.ThrowMcpException("Error: Selected code is not a valid expression");

        var parameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameterName))
            .WithType(SyntaxFactory.ParseTypeName("object"));

        var invocations = syntaxRoot.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(i =>
                (i.Expression is IdentifierNameSyntax id && id.Identifier.ValueText == methodName) ||
                (i.Expression is MemberAccessExpressionSyntax ma && ma.Name.Identifier.ValueText == methodName))
            .ToList();

        foreach (var invocation in invocations)
        {
            var newArgs = invocation.ArgumentList.AddArguments(SyntaxFactory.Argument(selectedExpression.WithoutTrivia()));
            syntaxRoot = syntaxRoot.ReplaceNode(invocation, invocation.WithArgumentList(newArgs));
        }

        method = syntaxRoot.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.ValueText == methodName);
        selectedExpression = syntaxRoot.DescendantNodes()
            .OfType<ExpressionSyntax>()
            .Where(e => span.Contains(e.Span) || e.Span.Contains(span))
            .OrderBy(e => Math.Abs(e.Span.Length - span.Length))
            .ThenBy(e => e.Span.Length)
            .First();

        var newMethod = method.ReplaceNode(selectedExpression, SyntaxFactory.IdentifierName(parameterName))
            .AddParameterListParameters(parameter);
        SyntaxNode newRoot = syntaxRoot.ReplaceNode(method, newMethod);

        var formattedRoot = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
        return formattedRoot.ToFullString();
    }
    [McpServerTool, Description("Create a new parameter from selected code (preferred for large C# file refactoring)")]
    public static async Task<string> IntroduceParameter(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Name of the method to add parameter to")] string methodName,
        [Description("Range in format 'startLine:startColumn-endLine:endColumn'")] string selectionRange,
        [Description("Name for the new parameter")] string parameterName)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath);
            var document = RefactoringHelpers.GetDocumentByPath(solution, filePath);
            if (document != null)
                return await IntroduceParameterWithSolution(document, methodName, selectionRange, parameterName);

            return await IntroduceParameterSingleFile(filePath, methodName, selectionRange, parameterName);
        }
        catch (Exception ex)
        {
            throw new McpException($"Error introducing parameter: {ex.Message}", ex);
        }
    }
}
