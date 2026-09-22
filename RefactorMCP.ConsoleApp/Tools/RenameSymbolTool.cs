using ModelContextProtocol.Server;
using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.FindSymbols;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

[McpServerToolType]
public static class RenameSymbolTool
{
    [McpServerTool, Description("Rename a symbol across the solution using Roslyn")]
    public static async Task<string> RenameSymbol(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file containing the symbol")] string filePath,
        [Description("Current name of the symbol")] string oldName,
        [Description("New name for the symbol")] string newName,
        [Description("Line number of the symbol (1-based, optional)")] int? line = null,
        [Description("Column number of the symbol (1-based, optional)")] int? column = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var document = RefactoringHelpers.GetDocumentByPath(solution, filePath);
            if (document == null)
                throw new McpException($"Error: File {filePath} not found in solution");

            var symbol = await FindSymbol(document, oldName, line, column, cancellationToken);
            if (symbol == null)
                throw new McpException($"Error: Symbol '{oldName}' not found");

            var options = new SymbolRenameOptions();
            var renamed = await Renamer.RenameSymbolAsync(solution, symbol, options, newName, cancellationToken);
            var changes = renamed.GetChanges(solution);
            foreach (var projectChange in changes.GetProjectChanges())
            {
                foreach (var id in projectChange.GetChangedDocuments())
                {
                    var newDoc = renamed.GetDocument(id)!;
                    var text = await newDoc.GetTextAsync(cancellationToken);
                    var encoding = await RefactoringHelpers.GetFileEncodingAsync(newDoc.FilePath!, cancellationToken);
                    await File.WriteAllTextAsync(newDoc.FilePath!, text.ToString(), encoding, cancellationToken);
                    RefactoringHelpers.UpdateSolutionCache(newDoc);
                }
            }

            return $"Successfully renamed '{oldName}' to '{newName}'";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error renaming symbol: {ex.Message}", ex);
        }
    }

    private static async Task<ISymbol?> FindSymbol(Document document, string name, int? line, int? column, CancellationToken cancellationToken)
    {
        var model = await document.GetSemanticModelAsync(cancellationToken);
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        if (model == null || root == null)
            return null;

        if (line.HasValue && column.HasValue)
        {
            var text = await document.GetTextAsync(cancellationToken);
            if (line.Value > 0 && line.Value <= text.Lines.Count && column.Value > 0)
            {
                var pos = text.Lines[line.Value - 1].Start + column.Value - 1;
                var token = root.FindToken(pos);
                var node = token.Parent;
                while (node != null)
                {
                    var sym = model.GetDeclaredSymbol(node) ?? model.GetSymbolInfo(node).Symbol;
                    if (sym != null && sym.Name == name)
                        return sym;
                    node = node.Parent;
                }
            }
        }

        var decls = await SymbolFinder.FindDeclarationsAsync(document.Project, name, false, cancellationToken);
        var declaration = decls.FirstOrDefault();
        if (declaration != null)
            return declaration;

        // Locals, parameters and local functions are not declarations of the
        // project, so the symbol finder cannot see them. Look in this document.
        return FindLocalInDocument(model, root, name);
    }

    private static ISymbol? FindLocalInDocument(SemanticModel model, SyntaxNode root, string name)
    {
        var matches = root.DescendantNodes()
            .Select(node => model.GetDeclaredSymbol(node))
            .OfType<ISymbol>()
            .Where(symbol => symbol.Name == name && IsLocalSymbol(symbol))
            .Distinct(SymbolEqualityComparer.Default)
            .ToList();

        if (matches.Count > 1)
            throw new McpException($"Error: Multiple symbols named '{name}' found in the document; pass line and column to choose one");

        return matches.FirstOrDefault();
    }

    private static bool IsLocalSymbol(ISymbol symbol)
    {
        return symbol.Kind == SymbolKind.Local
            || symbol.Kind == SymbolKind.Parameter
            || symbol is IMethodSymbol { MethodKind: MethodKind.LocalFunction };
    }
}
