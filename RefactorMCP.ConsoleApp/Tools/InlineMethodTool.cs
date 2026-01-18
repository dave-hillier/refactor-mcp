using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.FindSymbols;
using System.Linq;
using System.IO;

[McpServerToolType]
public static class InlineMethodTool
{

    private static async Task InlineReferences(MethodDeclarationSyntax method, Solution solution, ISymbol methodSymbol)
    {
        var refs = await SymbolFinder.FindReferencesAsync(methodSymbol, solution);
        var documents = refs.SelectMany(r => r.Locations)
            .Where(l => l.Location.IsInSource)
            .Select(l => solution.GetDocument(l.Location.SourceTree)!)
            .Distinct();

        foreach (var refDoc in documents)
        {
            var refRoot = await refDoc.GetSyntaxRootAsync();
            var semanticModel = await refDoc.GetSemanticModelAsync();
            var rewriter = new InlineInvocationRewriter(method, semanticModel!, (IMethodSymbol)methodSymbol);
            var newRoot = rewriter.Visit(refRoot!);

            if (!ReferenceEquals(refRoot, newRoot))
            {
                var formatted = Formatter.Format(newRoot!, refDoc.Project.Solution.Workspace);
                var newDoc = refDoc.WithSyntaxRoot(formatted);
                var text = await newDoc.GetTextAsync();
                var encoding = await RefactoringHelpers.GetFileEncodingAsync(refDoc.FilePath!);
                await File.WriteAllTextAsync(refDoc.FilePath!, text.ToString(), encoding);
                RefactoringHelpers.UpdateSolutionCache(newDoc);
            }
        }
    }

    private static async Task<string> InlineMethodWithSolution(Document document, string methodName)
    {
        var root = await document.GetSyntaxRootAsync();
        var semanticModel = await document.GetSemanticModelAsync();

        var method = root!.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            throw new McpException($"Error: Method '{methodName}' not found");

        var symbol = semanticModel!.GetDeclaredSymbol(method)!;
        await InlineReferences(method, document.Project.Solution, symbol);

        var newRoot = await document.GetSyntaxRootAsync();
        var updatedMethod = newRoot!.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.ValueText == methodName);
        newRoot = newRoot.RemoveNode(updatedMethod, SyntaxRemoveOptions.KeepNoTrivia);
        var formattedRoot = Formatter.Format(newRoot!, document.Project.Solution.Workspace);
        var newDocument = document.WithSyntaxRoot(formattedRoot);
        var newText = await newDocument.GetTextAsync();
        var encoding = await RefactoringHelpers.GetFileEncodingAsync(document.FilePath!);
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString(), encoding);
        RefactoringHelpers.UpdateSolutionCache(newDocument);

        return $"Successfully inlined method '{methodName}' in {document.FilePath} (solution mode)";
    }

    private static Task<string> InlineMethodSingleFile(string filePath, string methodName)
    {
        return RefactoringHelpers.ApplySingleFileEdit(
            filePath,
            text => InlineMethodInSource(text, methodName),
            $"Successfully inlined method '{methodName}' in {filePath} (single file mode)");
    }

    public static string InlineMethodInSource(string sourceText, string methodName)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName && m.ParameterList.Parameters.Count == 0);
        if (method == null)
            throw new McpException($"Error: Method '{methodName}' not found or has parameters");

        var rewriter = new InlineInvocationRewriter(method);
        var newRoot = rewriter.Visit(root)!;
        var updatedMethod = newRoot.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.ValueText == methodName && m.ParameterList.Parameters.Count == 0);
        newRoot = newRoot.RemoveNode(updatedMethod, SyntaxRemoveOptions.KeepNoTrivia);
        var formatted = Formatter.Format(newRoot!, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }

    [McpServerTool, Description("Inline a method and remove its declaration (preferred for large C# file refactoring)")]
    public static async Task<string> InlineMethod(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file containing the method")] string filePath,
        [Description("Name of the method to inline")] string methodName)
    {
        try
        {
            return await RefactoringHelpers.RunWithSolutionOrFile(
                solutionPath,
                filePath,
                doc => InlineMethodWithSolution(doc, methodName),
                path => InlineMethodSingleFile(path, methodName));
        }
        catch (Exception ex)
        {
            throw new McpException($"Error inlining method: {ex.Message}", ex);
        }
    }
}
