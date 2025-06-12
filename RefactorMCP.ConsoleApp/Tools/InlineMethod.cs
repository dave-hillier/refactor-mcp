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
using System.Collections.Generic;

[McpServerToolType]
public static class InlineMethodTool
{
    private class ParameterRewriter : CSharpSyntaxRewriter
    {
        private readonly Dictionary<string, ExpressionSyntax> _map;
        public ParameterRewriter(Dictionary<string, ExpressionSyntax> map)
        {
            _map = map;
        }
        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (_map.TryGetValue(node.Identifier.ValueText, out var expr))
                return expr;
            return base.VisitIdentifierName(node);
        }
    }

    private static SyntaxNode InlineInvocation(MethodDeclarationSyntax method, InvocationExpressionSyntax invocation)
    {
        var argMap = method.ParameterList.Parameters
            .Zip(invocation.ArgumentList.Arguments, (p, a) => new { p, a })
            .ToDictionary(x => x.p.Identifier.ValueText, x => x.a.Expression);

        var rewriter = new ParameterRewriter(argMap);
        var statements = method.Body!.Statements.Select(s => (StatementSyntax)rewriter.Visit(s)!);
        var stmt = invocation.FirstAncestorOrSelf<ExpressionStatementSyntax>();
        if (stmt != null && method.ReturnType is PredefinedTypeSyntax pts && pts.Keyword.IsKind(SyntaxKind.VoidKeyword))
        {
            return SyntaxFactory.Block(statements);
        }
        return invocation;
    }

    private static async Task InlineReferences(MethodDeclarationSyntax method, Solution solution, ISymbol methodSymbol)
    {
        var refs = await SymbolFinder.FindReferencesAsync(methodSymbol, solution);
        foreach (var loc in refs.SelectMany(r => r.Locations))
        {
            if (!loc.Location.IsInSource) continue;
            var refDoc = solution.GetDocument(loc.Location.SourceTree)!;
            var refRoot = await refDoc.GetSyntaxRootAsync();
            var node = refRoot!.FindNode(loc.Location.SourceSpan);
            var invocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
            if (invocation == null) continue;
            var inlineBlock = InlineInvocation(method, invocation);
            SyntaxNode newRefRoot;
            if (inlineBlock is BlockSyntax block && invocation.Parent is ExpressionStatementSyntax stmt)
            {
                newRefRoot = refRoot.ReplaceNode(stmt, block.Statements);
            }
            else
            {
                continue;
            }
            var formatted = Formatter.Format(newRefRoot, refDoc.Project.Solution.Workspace);
            var newDoc = refDoc.WithSyntaxRoot(formatted);
            var text = await newDoc.GetTextAsync();
            await File.WriteAllTextAsync(refDoc.FilePath!, text.ToString());
        }
    }

    private static async Task<string> InlineMethodWithSolution(Document document, string methodName)
    {
        var root = await document.GetSyntaxRootAsync();
        var semanticModel = await document.GetSemanticModelAsync();

        var method = root!.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            return RefactoringHelpers.ThrowMcpException($"Error: Method '{methodName}' not found");

        var symbol = semanticModel!.GetDeclaredSymbol(method)!;
        await InlineReferences(method, document.Project.Solution, symbol);

        var newRoot = (await document.GetSyntaxRootAsync())!.RemoveNode(method, SyntaxRemoveOptions.KeepNoTrivia);
        var formattedRoot = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        var newDocument = document.WithSyntaxRoot(formattedRoot);
        var newText = await newDocument.GetTextAsync();
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString());

        return $"Successfully inlined method '{methodName}' in {document.FilePath} (solution mode)";
    }

    private static async Task<string> InlineMethodSingleFile(string filePath, string methodName)
    {
        if (!File.Exists(filePath))
            return RefactoringHelpers.ThrowMcpException($"Error: File {filePath} not found (current dir: {Directory.GetCurrentDirectory()})");

        var sourceText = await File.ReadAllTextAsync(filePath);
        var newText = InlineMethodInSource(sourceText, methodName);
        await File.WriteAllTextAsync(filePath, newText);
        return $"Successfully inlined method '{methodName}' in {filePath} (single file mode)";
    }

    public static string InlineMethodInSource(string sourceText, string methodName)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName && m.ParameterList.Parameters.Count == 0);
        if (method == null)
            return RefactoringHelpers.ThrowMcpException($"Error: Method '{methodName}' not found or has parameters");

        var bodyText = string.Join("\n", method.Body!.Statements.Select(s => s.ToFullString()));

        var invocationNodes = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(i => i.Expression is IdentifierNameSyntax id && id.Identifier.ValueText == methodName)
            .ToList();

        foreach (var inv in invocationNodes)
        {
            var stmt = inv.FirstAncestorOrSelf<ExpressionStatementSyntax>();
            if (stmt != null)
            {
                var newStmt = SyntaxFactory.ParseStatement(bodyText);
                root = root.ReplaceNode(stmt, newStmt);
            }
        }

        root = root.RemoveNode(method, SyntaxRemoveOptions.KeepNoTrivia);
        var formatted = Formatter.Format(root, RefactoringHelpers.SharedWorkspace);
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
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath);
            var document = RefactoringHelpers.GetDocumentByPath(solution, filePath);
            if (document != null)
                return await InlineMethodWithSolution(document, methodName);

            return await InlineMethodSingleFile(filePath, methodName);
        }
        catch (Exception ex)
        {
            throw new McpException($"Error inlining method: {ex.Message}", ex);
        }
    }
}
