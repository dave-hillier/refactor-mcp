using ModelContextProtocol.Server;
using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Rename;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

[McpServerToolType]
public static class RenameSymbolTool
{
    [McpServerTool, Description("Rename a symbol across the solution using Roslyn, renaming the file of a type named after it")]
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
            if (!IsValidIdentifier(newName))
                throw new McpException($"Error: '{newName}' is not a valid identifier");

            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var symbol = await SymbolLookup.FindAsync(solution, filePath, oldName, line, column, cancellationToken);

            var renamed = await Renamer.RenameSymbolAsync(solution, symbol, new SymbolRenameOptions(), newName, cancellationToken);

            // The renamer resolves the conflicts it can, such as a call an
            // overload would capture, by qualifying or casting; what is left
            // breaks the build.
            var errors = await SolutionEdits.NewDiagnosticsAsync(solution, renamed, d => d.Severity == DiagnosticSeverity.Error, cancellationToken);
            if (errors.Count > 0)
                throw new McpException($"Error: Renaming '{oldName}' to '{newName}' conflicts with existing code: {SolutionEdits.Describe(errors[0])}");

            renamed = await RenameFilesAsync(renamed, symbol, newName, cancellationToken);
            await TypeRefactoringHelpers.ApplyAsync(solution, renamed, cancellationToken);

            return $"Successfully renamed '{oldName}' to '{newName}'";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error renaming symbol: {ex.Message}", ex);
        }
    }

    private static bool IsValidIdentifier(string name)
    {
        var bare = name.StartsWith('@') ? name[1..] : name;
        if (!SyntaxFacts.IsValidIdentifier(bare))
            return false;

        // A keyword is only an identifier when escaped with @.
        return name.StartsWith('@') || SyntaxFacts.GetKeywordKind(bare) == SyntaxKind.None;
    }

    /// <summary>
    /// Renames the files of a top-level type that are named after it, such as
    /// Customer.cs for Customer, to the new name.
    /// </summary>
    private static async Task<Solution> RenameFilesAsync(Solution solution, ISymbol symbol, string newName, CancellationToken cancellationToken)
    {
        if (symbol is not INamedTypeSymbol { ContainingType: null } type)
            return solution;

        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            var path = reference.SyntaxTree.FilePath;
            if (Path.GetFileNameWithoutExtension(path) != type.Name)
                continue;

            var documentId = solution.GetDocumentIdsWithFilePath(path).FirstOrDefault();
            if (documentId is null)
                continue;

            var fileName = newName.TrimStart('@') + Path.GetExtension(path);
            var newPath = Path.Combine(Path.GetDirectoryName(path)!, fileName);
            if (File.Exists(newPath) || solution.GetDocumentIdsWithFilePath(newPath).Any())
                throw new McpException($"Error: Cannot rename {Path.GetFileName(path)} to {fileName}: {newPath} already exists");

            var document = solution.GetDocument(documentId)!;
            var text = await document.GetTextAsync(cancellationToken);
            solution = solution
                .RemoveDocument(documentId)
                .AddDocument(DocumentId.CreateNewId(documentId.ProjectId), fileName, text, document.Folders, newPath);
        }

        return solution;
    }
}
