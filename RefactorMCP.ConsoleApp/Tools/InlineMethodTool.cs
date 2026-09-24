using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Linq;
using System.IO;
using System.Threading;

[McpServerToolType]
public static class InlineMethodTool
{

    private static async Task<string> InlineMethodWithSolution(Document document, string methodName, int? line)
    {
        var root = await document.GetSyntaxRootAsync();
        var candidates = root!.DescendantNodes()
            .Select(node => node switch
            {
                MethodDeclarationSyntax method => (Declaration: (MemberDeclarationSyntax)method, Name: method.Identifier),
                PropertyDeclarationSyntax property => (Declaration: property, Name: property.Identifier),
                _ => (Declaration: null!, Name: default),
            })
            .Where(c => c.Declaration is not null && c.Name.ValueText == methodName)
            .ToList();
        if (line.HasValue)
            candidates = candidates.Where(c => c.Name.GetLocation().GetLineSpan().StartLinePosition.Line + 1 == line.Value).ToList();

        if (candidates.Count == 0)
            throw new McpException($"Error: Method '{methodName}' not found");
        if (candidates.Count > 1)
            throw new McpException($"Error: '{methodName}' has overloads; pass the line of the one to inline");

        var uses = await MethodInliner.InlineAsync(document, candidates[0].Declaration, CancellationToken.None);
        var kind = candidates[0].Declaration is PropertyDeclarationSyntax ? "property" : "method";
        return $"Successfully inlined {kind} '{methodName}' at {uses} use(s) in {document.FilePath} (solution mode)";
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

    [McpServerTool, Description("Inline a method, or a read-only property whose getter computes its value, at every use and remove its declaration (preferred for large C# file refactoring)")]
    public static async Task<string> InlineMethod(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file containing the method")] string filePath,
        [Description("Name of the method or property to inline")] string methodName,
        [Description("Line of the member's declaration, to choose between overloads (1-based, optional)")] int? line = null)
    {
        try
        {
            return await RefactoringHelpers.RunWithSolutionOrFile(
                solutionPath,
                filePath,
                doc => InlineMethodWithSolution(doc, methodName, line),
                path => InlineMethodSingleFile(path, methodName));
        }
        catch (Exception ex)
        {
            throw new McpException($"Error inlining method: {ex.Message}", ex);
        }
    }
}
