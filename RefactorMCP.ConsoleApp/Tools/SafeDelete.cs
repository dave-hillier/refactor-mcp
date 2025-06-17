using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Editing;
using RefactorMCP.ConsoleApp.SyntaxRewriters;

[McpServerToolType]
public static class SafeDeleteTool
{
    [McpServerTool, Description("Safely delete an unused field (preferred for large C# file refactoring)")]
    public static async Task<string> SafeDeleteField(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Name of the field to delete")] string fieldName)
    {
        try
        {
            return await RefactoringHelpers.RunWithSolutionOrFile(
                solutionPath,
                filePath,
                doc => SafeDeleteFieldWithSolution(doc, fieldName),
                path => SafeDeleteFieldSingleFile(path, fieldName));
        }
        catch (Exception ex)
        {
            throw new McpException($"Error deleting field: {ex.Message}", ex);
        }
    }

    [McpServerTool, Description("Safely delete an unused method (preferred for large C# file refactoring)")]
    public static async Task<string> SafeDeleteMethod(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Name of the method to delete")] string methodName)
    {
        try
        {
            return await RefactoringHelpers.RunWithSolutionOrFile(
                solutionPath,
                filePath,
                doc => SafeDeleteMethodWithSolution(doc, methodName),
                path => SafeDeleteMethodSingleFile(path, methodName));
        }
        catch (Exception ex)
        {
            throw new McpException($"Error deleting method: {ex.Message}", ex);
        }
    }

    [McpServerTool, Description("Safely delete an unused parameter from a method")]
    public static async Task<string> SafeDeleteParameter(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Name of the method containing the parameter")] string methodName,
        [Description("Name of the parameter to delete")] string parameterName)
    {
        try
        {
            return await RefactoringHelpers.RunWithSolutionOrFile(
                solutionPath,
                filePath,
                doc => SafeDeleteParameterWithSolution(doc, methodName, parameterName),
                path => SafeDeleteParameterSingleFile(path, methodName, parameterName));
        }
        catch (Exception ex)
        {
            throw new McpException($"Error deleting parameter: {ex.Message}", ex);
        }
    }

    [McpServerTool, Description("Safely delete a local variable using a line range")]
    public static async Task<string> SafeDeleteVariable(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Range of the variable declaration in format 'startLine:startCol-endLine:endCol'")] string selectionRange)
    {
        try
        {
            return await RefactoringHelpers.RunWithSolutionOrFile(
                solutionPath,
                filePath,
                doc => SafeDeleteVariableWithSolution(doc, selectionRange),
                path => SafeDeleteVariableSingleFile(path, selectionRange));
        }
        catch (Exception ex)
        {
            throw new McpException($"Error deleting variable: {ex.Message}", ex);
        }
    }

    private static async Task<string> SafeDeleteFieldWithSolution(Document document, string fieldName)
    {
        var root = await document.GetSyntaxRootAsync();
        var field = root!.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .FirstOrDefault(f => f.Declaration.Variables.Any(v => v.Identifier.ValueText == fieldName));
        if (field == null)
            throw new McpException($"Error: Field '{fieldName}' not found. Verify the field name and ensure the file is part of the loaded solution.");

        var variable = field.Declaration.Variables.First(v => v.Identifier.ValueText == fieldName);
        var semanticModel = await document.GetSemanticModelAsync();
        var symbol = semanticModel!.GetDeclaredSymbol(variable) as IFieldSymbol;
        var refs = await SymbolFinder.FindReferencesAsync(symbol!, document.Project.Solution);
        var count = refs.SelectMany(r => r.Locations).Count() - 1;
        if (count > 0)
            throw new McpException($"Error: Field '{fieldName}' is referenced {count} time(s)");

        var rewriter = new FieldRemovalRewriter(fieldName);
        var newRoot = rewriter.Visit(root)!;

        var formatted = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        var newDoc = document.WithSyntaxRoot(formatted);
        var text = await newDoc.GetTextAsync();
        var encoding = await RefactoringHelpers.GetFileEncodingAsync(document.FilePath!);
        await File.WriteAllTextAsync(document.FilePath!, text.ToString(), encoding);
        RefactoringHelpers.UpdateSolutionCache(newDoc);
        return $"Successfully deleted field '{fieldName}' in {document.FilePath}";
    }

    private static Task<string> SafeDeleteFieldSingleFile(string filePath, string fieldName)
    {
        return RefactoringHelpers.ApplySingleFileEdit(
            filePath,
            text => SafeDeleteFieldInSource(text, fieldName),
            $"Successfully deleted field '{fieldName}' in {filePath} (single file mode)");
    }

    public static string SafeDeleteFieldInSource(string sourceText, string fieldName)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetRoot();
        var field = root.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .FirstOrDefault(f => f.Declaration.Variables.Any(v => v.Identifier.ValueText == fieldName));
        if (field == null)
            throw new McpException($"Error: Field '{fieldName}' not found. Verify the field name and ensure the file is part of the loaded solution.");

        var references = root.DescendantNodes().OfType<IdentifierNameSyntax>().Count(id => id.Identifier.ValueText == fieldName);
        if (references > 1)
            throw new McpException($"Error: Field '{fieldName}' is referenced");

        SyntaxNode newRoot;
        if (field.Declaration.Variables.Count == 1)
            newRoot = root.RemoveNode(field, SyntaxRemoveOptions.KeepNoTrivia)!;
        else
        {
            var newDecl = field.Declaration.WithVariables(SyntaxFactory.SeparatedList(field.Declaration.Variables.Where(v => v.Identifier.ValueText != fieldName)));
            newRoot = root.ReplaceNode(field, field.WithDeclaration(newDecl));
        }

        var formatted = Formatter.Format(newRoot!, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }

    private static async Task<string> SafeDeleteMethodWithSolution(Document document, string methodName)
    {
        var root = await document.GetSyntaxRootAsync();
        var method = root!.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            throw new McpException($"Error: Method '{methodName}' not found. Verify the method name and ensure the file is part of the loaded solution.");

        var semanticModel = await document.GetSemanticModelAsync();
        var symbol = semanticModel!.GetDeclaredSymbol(method)!;
        var refs = await SymbolFinder.FindReferencesAsync(symbol, document.Project.Solution);
        var count = refs.SelectMany(r => r.Locations).Count() - 1;
        if (count > 0)
            throw new McpException($"Error: Method '{methodName}' is referenced {count} time(s)");

        var rewriter = new MethodRemovalRewriter(methodName);
        var newRoot = rewriter.Visit(root)!;
        var formatted = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        var newDoc = document.WithSyntaxRoot(formatted);
        var text = await newDoc.GetTextAsync();
        var encoding = await RefactoringHelpers.GetFileEncodingAsync(document.FilePath!);
        await File.WriteAllTextAsync(document.FilePath!, text.ToString(), encoding);
        RefactoringHelpers.UpdateSolutionCache(newDoc);
        return $"Successfully deleted method '{methodName}' in {document.FilePath}";
    }

    private static Task<string> SafeDeleteMethodSingleFile(string filePath, string methodName)
    {
        return RefactoringHelpers.ApplySingleFileEdit(
            filePath,
            text => SafeDeleteMethodInSource(text, methodName),
            $"Successfully deleted method '{methodName}' in {filePath} (single file mode)");
    }

    public static string SafeDeleteMethodInSource(string sourceText, string methodName)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            throw new McpException($"Error: Method '{methodName}' not found. Verify the method name and ensure the file is part of the loaded solution.");

        var references = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Count(inv => inv.Expression is IdentifierNameSyntax id && id.Identifier.ValueText == methodName);
        if (references > 0)
            throw new McpException($"Error: Method '{methodName}' is referenced");

        var rewriter = new MethodRemovalRewriter(methodName);
        var newRoot = rewriter.Visit(root)!;
        var formatted = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }

    private static async Task<string> SafeDeleteParameterWithSolution(Document document, string methodName, string parameterName)
    {
        var root = await document.GetSyntaxRootAsync();
        var method = root!.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            throw new McpException($"Error: Method '{methodName}' not found. Verify the method name and ensure the file is part of the loaded solution.");

        var parameter = method.ParameterList.Parameters.FirstOrDefault(p => p.Identifier.ValueText == parameterName);
        if (parameter == null)
            throw new McpException($"Error: Parameter '{parameterName}' not found. Verify the parameter name and ensure the file is part of the loaded solution.");

        var semanticModel = await document.GetSemanticModelAsync();
        var methodSymbol = semanticModel!.GetDeclaredSymbol(method)!;
        var paramIndex = method.ParameterList.Parameters.IndexOf(parameter);

        var refs = await SymbolFinder.FindReferencesAsync(methodSymbol, document.Project.Solution);
        var docs = refs.SelectMany(r => r.Locations)
            .Where(l => l.Location.IsInSource)
            .Select(l => document.Project.Solution.GetDocument(l.Location.SourceTree)!)
            .Distinct()
            .ToList();

        if (!docs.Contains(document))
            docs.Add(document);

        var generator = SyntaxGenerator.GetGenerator(document.Project.Solution.Workspace, LanguageNames.CSharp);
        var rewriter = new ParameterRemovalRewriter(methodName, paramIndex, generator);

        foreach (var doc in docs)
        {
            var docRoot = await doc.GetSyntaxRootAsync();
            var rewritten = rewriter.Visit(docRoot!);
            var formattedRoot = Formatter.Format(rewritten!, doc.Project.Solution.Workspace);
            var newDoc = doc.WithSyntaxRoot(formattedRoot);
            var newText = await newDoc.GetTextAsync();
            var encoding = await RefactoringHelpers.GetFileEncodingAsync(doc.FilePath!);
            await File.WriteAllTextAsync(doc.FilePath!, newText.ToString(), encoding);
            RefactoringHelpers.UpdateSolutionCache(newDoc);
        }

        return $"Successfully deleted parameter '{parameterName}' from method '{methodName}' in {document.FilePath}";
    }

    private static Task<string> SafeDeleteParameterSingleFile(string filePath, string methodName, string parameterName)
    {
        return RefactoringHelpers.ApplySingleFileEdit(
            filePath,
            text => SafeDeleteParameterInSource(text, methodName, parameterName),
            $"Successfully deleted parameter '{parameterName}' from method '{methodName}' in {filePath} (single file mode)");
    }

    public static string SafeDeleteParameterInSource(string sourceText, string methodName, string parameterName)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            throw new McpException($"Error: Method '{methodName}' not found. Verify the method name and ensure the file is part of the loaded solution.");

        var parameter = method.ParameterList.Parameters.FirstOrDefault(p => p.Identifier.ValueText == parameterName);
        if (parameter == null)
            throw new McpException($"Error: Parameter '{parameterName}' not found. Verify the parameter name and ensure the file is part of the loaded solution.");

        var paramIndex = method.ParameterList.Parameters.IndexOf(parameter);
        var generator = SyntaxGenerator.GetGenerator(RefactoringHelpers.SharedWorkspace, LanguageNames.CSharp);
        var rewriter = new ParameterRemovalRewriter(methodName, paramIndex, generator);
        var newRoot = rewriter.Visit(root)!;
        var formatted = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }

    private static async Task<string> SafeDeleteVariableWithSolution(Document document, string selectionRange)
    {
        var text = await document.GetTextAsync();
        var root = await document.GetSyntaxRootAsync();
        if (!RefactoringHelpers.TryParseRange(selectionRange, out var sl, out var sc, out var el, out var ec))
            throw new McpException("Error: Invalid selection range format");

        if (!RefactoringHelpers.ValidateRange(text, sl, sc, el, ec, out var error))
            throw new McpException(error);

        var start = text.Lines[sl - 1].Start + sc - 1;
        var end = text.Lines[el - 1].Start + ec - 1;
        var span = TextSpan.FromBounds(start, end);
        var variable = root!.DescendantNodes(span).OfType<VariableDeclaratorSyntax>().FirstOrDefault();
        if (variable == null)
            throw new McpException("Error: No variable declaration found in range");

        var semanticModel = await document.GetSemanticModelAsync();
        var symbol = semanticModel!.GetDeclaredSymbol(variable)!;
        var refs = await SymbolFinder.FindReferencesAsync(symbol, document.Project.Solution);
        var count = refs.SelectMany(r => r.Locations).Count() - 1;
        if (count > 0)
            throw new McpException($"Error: Variable '{variable.Identifier.ValueText}' is referenced {count} time(s)");

        var rewriter = new VariableRemovalRewriter(variable.Identifier.ValueText, variable.Span);
        var newRoot = rewriter.Visit(root)!;

        var formatted = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        var newDoc = document.WithSyntaxRoot(formatted);
        var newText = await newDoc.GetTextAsync();
        var encoding = await RefactoringHelpers.GetFileEncodingAsync(document.FilePath!);
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString(), encoding);
        RefactoringHelpers.UpdateSolutionCache(newDoc);
        return $"Successfully deleted variable '{variable.Identifier.ValueText}' in {document.FilePath}";
    }

    private static Task<string> SafeDeleteVariableSingleFile(string filePath, string selectionRange)
    {
        return RefactoringHelpers.ApplySingleFileEdit(
            filePath,
            text => SafeDeleteVariableInSource(text, selectionRange),
            $"Successfully deleted variable in {filePath} (single file mode)");
    }

    public static string SafeDeleteVariableInSource(string sourceText, string selectionRange)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetRoot();
        var text = SourceText.From(sourceText);
        var lines = text.Lines;
        if (!RefactoringHelpers.TryParseRange(selectionRange, out var sl, out var sc, out var el, out var ec))
            throw new McpException("Error: Invalid selection range format");

        if (!RefactoringHelpers.ValidateRange(text, sl, sc, el, ec, out var error))
            throw new McpException(error);

        var start = lines[sl - 1].Start + sc - 1;
        var end = lines[el - 1].Start + ec - 1;
        var span = TextSpan.FromBounds(start, end);
        var variable = root.DescendantNodes(span).OfType<VariableDeclaratorSyntax>().FirstOrDefault();
        if (variable == null)
            throw new McpException("Error: No variable declaration found in range");

        var name = variable.Identifier.ValueText;
        var references = root.DescendantNodes().OfType<IdentifierNameSyntax>().Count(id => id.Identifier.ValueText == name);
        if (references > 1)
            throw new McpException($"Error: Variable '{name}' is referenced");

        var rewriter = new VariableRemovalRewriter(name, variable.Span);
        var newRoot = rewriter.Visit(root)!;
        var formatted = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }
}
