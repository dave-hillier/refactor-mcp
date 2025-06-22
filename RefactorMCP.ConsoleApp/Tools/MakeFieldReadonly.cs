using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Linq;
using System.Threading;

[McpServerToolType]
public static class MakeFieldReadonlyTool
{
    [McpServerTool, Description("Make a field readonly if assigned only during initialization (preferred for large C# file refactoring)")]
    public static async Task<string> MakeFieldReadonly(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Name of the field to make readonly")] string fieldName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await RefactoringHelpers.RunWithSolutionOrFile(
                solutionPath,
                filePath,
                doc => MakeFieldReadonlyWithSolution(doc, fieldName),
                path => MakeFieldReadonlySingleFile(path, fieldName));
        }
        catch (Exception ex)
        {
            throw new McpException($"Error: {ex.Message}", ex);
        }
    }

    private static async Task<string> MakeFieldReadonlyWithSolution(Document document, string fieldName)
    {
        var syntaxRoot = await document.GetSyntaxRootAsync();

        var fieldDeclaration = syntaxRoot!.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .FirstOrDefault(f => f.Declaration.Variables.Any(v => v.Identifier.ValueText == fieldName));

        if (fieldDeclaration == null)
            throw new McpException($"Error: No field named '{fieldName}' found");

        var variable = fieldDeclaration.Declaration.Variables.First(v => v.Identifier.ValueText == fieldName);
        var initializer = variable.Initializer?.Value;

        var rewriter = new ReadonlyFieldRewriter(fieldName, initializer);
        var newRoot = rewriter.Visit(syntaxRoot);

        var formattedRoot = Formatter.Format(newRoot!, document.Project.Solution.Workspace);
        var newDocument = document.WithSyntaxRoot(formattedRoot);
        var newText = await newDocument.GetTextAsync();
        var encoding = await RefactoringHelpers.GetFileEncodingAsync(document.FilePath!);
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString(), encoding);
        RefactoringHelpers.UpdateSolutionCache(newDocument);

        if (initializer != null)
            return $"Successfully made field '{fieldName}' readonly and moved initialization to constructors in {document.FilePath}";

        return $"Successfully made field '{fieldName}' readonly in {document.FilePath}";
    }

    private static Task<string> MakeFieldReadonlySingleFile(string filePath, string fieldName)
    {
        return RefactoringHelpers.ApplySingleFileEdit(
            filePath,
            text => MakeFieldReadonlyInSource(text, fieldName),
            $"Successfully made field '{fieldName}' readonly in {filePath} (single file mode)");
    }

    public static string MakeFieldReadonlyInSource(string sourceText, string fieldName)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = syntaxTree.GetRoot();

        var fieldDeclaration = syntaxRoot.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .FirstOrDefault(f => f.Declaration.Variables.Any(v => v.Identifier.ValueText == fieldName));

        if (fieldDeclaration == null)
            throw new McpException($"Error: No field named '{fieldName}' found");

        var variable = fieldDeclaration.Declaration.Variables.First(v => v.Identifier.ValueText == fieldName);
        var initializer = variable.Initializer?.Value;

        var rewriter = new ReadonlyFieldRewriter(fieldName, initializer);
        var newRoot = rewriter.Visit(syntaxRoot);

        var formattedRoot = Formatter.Format(newRoot!, RefactoringHelpers.SharedWorkspace);
        return formattedRoot.ToFullString();
    }

}
