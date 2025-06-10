using ModelContextProtocol.Server;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;

public static partial class RefactoringTools
{
    [McpServerTool, Description("Safely delete an unused field (preferred for large-file refactoring)")]
    public static async Task<string> SafeDeleteField(
        [Description("Path to the C# file")] string filePath,
        [Description("Name of the field to delete")] string fieldName,
        [Description("Path to the solution file (.sln) - optional for single file mode")] string? solutionPath = null)
    {
        try
        {
            if (solutionPath != null)
            {
                var solution = await GetOrLoadSolution(solutionPath);
                var document = GetDocumentByPath(solution, filePath);
                if (document != null)
                    return await SafeDeleteFieldWithSolution(document, fieldName);

                return await SafeDeleteFieldSingleFile(filePath, fieldName);
            }

            return await SafeDeleteFieldSingleFile(filePath, fieldName);
        }
        catch (Exception ex)
        {
            return $"Error deleting field: {ex.Message}";
        }
    }

    [McpServerTool, Description("Safely delete an unused method (preferred for large-file refactoring)")]
    public static async Task<string> SafeDeleteMethod(
        [Description("Path to the C# file")] string filePath,
        [Description("Name of the method to delete")] string methodName,
        [Description("Path to the solution file (.sln) - optional for single file mode")] string? solutionPath = null)
    {
        try
        {
            if (solutionPath != null)
            {
                var solution = await GetOrLoadSolution(solutionPath);
                var document = GetDocumentByPath(solution, filePath);
                if (document != null)
                    return await SafeDeleteMethodWithSolution(document, methodName);

                return await SafeDeleteMethodSingleFile(filePath, methodName);
            }

            return await SafeDeleteMethodSingleFile(filePath, methodName);
        }
        catch (Exception ex)
        {
            return $"Error deleting method: {ex.Message}";
        }
    }

    [McpServerTool, Description("Safely delete an unused parameter from a method")]
    public static async Task<string> SafeDeleteParameter(
        [Description("Path to the C# file")] string filePath,
        [Description("Name of the method containing the parameter")] string methodName,
        [Description("Name of the parameter to delete")] string parameterName,
        [Description("Path to the solution file (.sln) - optional for single file mode")] string? solutionPath = null)
    {
        try
        {
            if (solutionPath != null)
            {
                var solution = await GetOrLoadSolution(solutionPath);
                var document = GetDocumentByPath(solution, filePath);
                if (document != null)
                    return await SafeDeleteParameterWithSolution(document, methodName, parameterName);

                return await SafeDeleteParameterSingleFile(filePath, methodName, parameterName);
            }

            return await SafeDeleteParameterSingleFile(filePath, methodName, parameterName);
        }
        catch (Exception ex)
        {
            return $"Error deleting parameter: {ex.Message}";
        }
    }

    [McpServerTool, Description("Safely delete a local variable using a line range")]
    public static async Task<string> SafeDeleteVariable(
        [Description("Path to the C# file")] string filePath,
        [Description("Range of the variable declaration in format 'startLine:startCol-endLine:endCol'")] string selectionRange,
        [Description("Path to the solution file (.sln) - optional for single file mode")] string? solutionPath = null)
    {
        try
        {
            if (solutionPath != null)
            {
                var solution = await GetOrLoadSolution(solutionPath);
                var document = GetDocumentByPath(solution, filePath);
                if (document != null)
                    return await SafeDeleteVariableWithSolution(document, selectionRange);

                return await SafeDeleteVariableSingleFile(filePath, selectionRange);
            }

            return await SafeDeleteVariableSingleFile(filePath, selectionRange);
        }
        catch (Exception ex)
        {
            return $"Error deleting variable: {ex.Message}";
        }
    }

    private static async Task<string> SafeDeleteFieldWithSolution(Document document, string fieldName)
    {
        var root = await document.GetSyntaxRootAsync();
        var field = root!.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .FirstOrDefault(f => f.Declaration.Variables.Any(v => v.Identifier.ValueText == fieldName));
        if (field == null)
            return $"Error: Field '{fieldName}' not found";

        var variable = field.Declaration.Variables.First(v => v.Identifier.ValueText == fieldName);
        var semanticModel = await document.GetSemanticModelAsync();
        var symbol = semanticModel!.GetDeclaredSymbol(variable) as IFieldSymbol;
        var refs = await SymbolFinder.FindReferencesAsync(symbol!, document.Project.Solution);
        var count = refs.SelectMany(r => r.Locations).Count() - 1;
        if (count > 0)
            return $"Error: Field '{fieldName}' is referenced {count} time(s)";

        SyntaxNode newRoot;
        if (field.Declaration.Variables.Count == 1)
            newRoot = root.RemoveNode(field, SyntaxRemoveOptions.KeepNoTrivia);
        else
        {
            var newDecl = field.Declaration.WithVariables(SyntaxFactory.SeparatedList(field.Declaration.Variables.Where(v => v.Identifier.ValueText != fieldName)));
            newRoot = root.ReplaceNode(field, field.WithDeclaration(newDecl));
        }

        var formatted = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        var newDoc = document.WithSyntaxRoot(formatted);
        var text = await newDoc.GetTextAsync();
        await File.WriteAllTextAsync(document.FilePath!, text.ToString());
        return $"Successfully deleted field '{fieldName}' in {document.FilePath}";
    }

    private static async Task<string> SafeDeleteFieldSingleFile(string filePath, string fieldName)
    {
        if (!File.Exists(filePath))
            return $"Error: File {filePath} not found";

        var sourceText = await File.ReadAllTextAsync(filePath);
        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = await tree.GetRootAsync();
        var field = root.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .FirstOrDefault(f => f.Declaration.Variables.Any(v => v.Identifier.ValueText == fieldName));
        if (field == null)
            return $"Error: Field '{fieldName}' not found";

        var references = root.DescendantNodes().OfType<IdentifierNameSyntax>().Count(id => id.Identifier.ValueText == fieldName);
        if (references > 1)
            return $"Error: Field '{fieldName}' is referenced";

        SyntaxNode newRoot;
        if (field.Declaration.Variables.Count == 1)
            newRoot = root.RemoveNode(field, SyntaxRemoveOptions.KeepNoTrivia);
        else
        {
            var newDecl = field.Declaration.WithVariables(SyntaxFactory.SeparatedList(field.Declaration.Variables.Where(v => v.Identifier.ValueText != fieldName)));
            newRoot = root.ReplaceNode(field, field.WithDeclaration(newDecl));
        }

        var formatted = Formatter.Format(newRoot, SharedWorkspace);
        await File.WriteAllTextAsync(filePath, formatted.ToFullString());
        return $"Successfully deleted field '{fieldName}' in {filePath} (single file mode)";
    }

    private static async Task<string> SafeDeleteMethodWithSolution(Document document, string methodName)
    {
        var root = await document.GetSyntaxRootAsync();
        var method = root!.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            return $"Error: Method '{methodName}' not found";

        var semanticModel = await document.GetSemanticModelAsync();
        var symbol = semanticModel!.GetDeclaredSymbol(method)!;
        var refs = await SymbolFinder.FindReferencesAsync(symbol, document.Project.Solution);
        var count = refs.SelectMany(r => r.Locations).Count() - 1;
        if (count > 0)
            return $"Error: Method '{methodName}' is referenced {count} time(s)";

        var newRoot = root.RemoveNode(method, SyntaxRemoveOptions.KeepNoTrivia);
        var formatted = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        var newDoc = document.WithSyntaxRoot(formatted);
        var text = await newDoc.GetTextAsync();
        await File.WriteAllTextAsync(document.FilePath!, text.ToString());
        return $"Successfully deleted method '{methodName}' in {document.FilePath}";
    }

    private static async Task<string> SafeDeleteMethodSingleFile(string filePath, string methodName)
    {
        if (!File.Exists(filePath))
            return $"Error: File {filePath} not found";

        var sourceText = await File.ReadAllTextAsync(filePath);
        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = await tree.GetRootAsync();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            return $"Error: Method '{methodName}' not found";

        var references = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Count(inv => inv.Expression is IdentifierNameSyntax id && id.Identifier.ValueText == methodName);
        if (references > 0)
            return $"Error: Method '{methodName}' is referenced";

        var newRoot = root.RemoveNode(method, SyntaxRemoveOptions.KeepNoTrivia);
        var formatted = Formatter.Format(newRoot, SharedWorkspace);
        await File.WriteAllTextAsync(filePath, formatted.ToFullString());
        return $"Successfully deleted method '{methodName}' in {filePath} (single file mode)";
    }

    private static async Task<string> SafeDeleteParameterWithSolution(Document document, string methodName, string parameterName)
    {
        var root = await document.GetSyntaxRootAsync();
        var method = root!.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            return $"Error: Method '{methodName}' not found";

        var parameter = method.ParameterList.Parameters.FirstOrDefault(p => p.Identifier.ValueText == parameterName);
        if (parameter == null)
            return $"Error: Parameter '{parameterName}' not found";

        var semanticModel = await document.GetSemanticModelAsync();
        var methodSymbol = semanticModel!.GetDeclaredSymbol(method)!;
        var paramIndex = method.ParameterList.Parameters.IndexOf(parameter);

        var refs = await SymbolFinder.FindReferencesAsync(methodSymbol, document.Project.Solution);
        foreach (var location in refs.SelectMany(r => r.Locations))
        {
            if (!location.Location.IsInSource) continue;
            var refDoc = document.Project.Solution.GetDocument(location.Location.SourceTree)!;
            var refRoot = await refDoc.GetSyntaxRootAsync();
            var node = refRoot!.FindNode(location.Location.SourceSpan);
            if (node is IdentifierNameSyntax && node.Parent is InvocationExpressionSyntax invocation)
            {
                if (paramIndex < invocation.ArgumentList.Arguments.Count)
                {
                    var newArgs = invocation.ArgumentList.Arguments.RemoveAt(paramIndex);
                    var newInv = invocation.WithArgumentList(invocation.ArgumentList.WithArguments(newArgs));
                    refRoot = refRoot.ReplaceNode(invocation, newInv);
                    var formattedRoot = Formatter.Format(refRoot, refDoc.Project.Solution.Workspace);
                    var newRefDoc = refDoc.WithSyntaxRoot(formattedRoot);
                    var newText = await newRefDoc.GetTextAsync();
                    await File.WriteAllTextAsync(refDoc.FilePath!, newText.ToString());
                }
            }
        }

        var newMethod = method.WithParameterList(method.ParameterList.WithParameters(method.ParameterList.Parameters.Remove(parameter)));
        var newRoot = root.ReplaceNode(method, newMethod);
        var formatted = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        var newDoc = document.WithSyntaxRoot(formatted);
        var text = await newDoc.GetTextAsync();
        await File.WriteAllTextAsync(document.FilePath!, text.ToString());
        return $"Successfully deleted parameter '{parameterName}' from method '{methodName}' in {document.FilePath}";
    }

    private static async Task<string> SafeDeleteParameterSingleFile(string filePath, string methodName, string parameterName)
    {
        if (!File.Exists(filePath))
            return $"Error: File {filePath} not found";

        var sourceText = await File.ReadAllTextAsync(filePath);
        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = await tree.GetRootAsync();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            return $"Error: Method '{methodName}' not found";

        var parameter = method.ParameterList.Parameters.FirstOrDefault(p => p.Identifier.ValueText == parameterName);
        if (parameter == null)
            return $"Error: Parameter '{parameterName}' not found";

        var paramIndex = method.ParameterList.Parameters.IndexOf(parameter);
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(i => i.Expression is IdentifierNameSyntax id && id.Identifier.ValueText == methodName);

        foreach (var invocation in invocations)
        {
            if (paramIndex < invocation.ArgumentList.Arguments.Count)
            {
                var newArgs = invocation.ArgumentList.Arguments.RemoveAt(paramIndex);
                var newInv = invocation.WithArgumentList(invocation.ArgumentList.WithArguments(newArgs));
                root = root.ReplaceNode(invocation, newInv);
            }
        }

        var newMethod = method.WithParameterList(method.ParameterList.WithParameters(method.ParameterList.Parameters.Remove(parameter)));
        root = root.ReplaceNode(method, newMethod);
        var formatted = Formatter.Format(root, SharedWorkspace);
        await File.WriteAllTextAsync(filePath, formatted.ToFullString());
        return $"Successfully deleted parameter '{parameterName}' from method '{methodName}' in {filePath} (single file mode)";
    }

    private static async Task<string> SafeDeleteVariableWithSolution(Document document, string selectionRange)
    {
        var text = await document.GetTextAsync();
        var root = await document.GetSyntaxRootAsync();
        if (!TryParseRange(selectionRange, out var sl, out var sc, out var el, out var ec))
            return "Error: Invalid selection range format";

        var start = text.Lines[sl - 1].Start + sc - 1;
        var end = text.Lines[el - 1].Start + ec - 1;
        var span = TextSpan.FromBounds(start, end);
        var variable = root!.DescendantNodes(span).OfType<VariableDeclaratorSyntax>().FirstOrDefault();
        if (variable == null)
            return "Error: No variable declaration found in range";

        var semanticModel = await document.GetSemanticModelAsync();
        var symbol = semanticModel!.GetDeclaredSymbol(variable)!;
        var refs = await SymbolFinder.FindReferencesAsync(symbol, document.Project.Solution);
        var count = refs.SelectMany(r => r.Locations).Count() - 1;
        if (count > 0)
            return $"Error: Variable '{variable.Identifier.ValueText}' is referenced {count} time(s)";

        var statement = variable.FirstAncestorOrSelf<LocalDeclarationStatementSyntax>();
        SyntaxNode newRoot;
        if (statement!.Declaration.Variables.Count == 1)
            newRoot = root.RemoveNode(statement, SyntaxRemoveOptions.KeepNoTrivia);
        else
        {
            var newDecl = statement.Declaration.WithVariables(SyntaxFactory.SeparatedList(statement.Declaration.Variables.Where(v => v != variable)));
            newRoot = root.ReplaceNode(statement, statement.WithDeclaration(newDecl));
        }

        var formatted = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        var newDoc = document.WithSyntaxRoot(formatted);
        var newText = await newDoc.GetTextAsync();
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString());
        return $"Successfully deleted variable '{variable.Identifier.ValueText}' in {document.FilePath}";
    }

    private static async Task<string> SafeDeleteVariableSingleFile(string filePath, string selectionRange)
    {
        if (!File.Exists(filePath))
            return $"Error: File {filePath} not found";

        var sourceText = await File.ReadAllTextAsync(filePath);
        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = await tree.GetRootAsync();
        var lines = SourceText.From(sourceText).Lines;
        if (!TryParseRange(selectionRange, out var sl, out var sc, out var el, out var ec))
            return "Error: Invalid selection range format";

        var start = lines[sl - 1].Start + sc - 1;
        var end = lines[el - 1].Start + ec - 1;
        var span = TextSpan.FromBounds(start, end);
        var variable = root.DescendantNodes(span).OfType<VariableDeclaratorSyntax>().FirstOrDefault();
        if (variable == null)
            return "Error: No variable declaration found in range";

        var name = variable.Identifier.ValueText;
        var references = root.DescendantNodes().OfType<IdentifierNameSyntax>().Count(id => id.Identifier.ValueText == name);
        if (references > 1)
            return $"Error: Variable '{name}' is referenced";

        var statement = variable.FirstAncestorOrSelf<LocalDeclarationStatementSyntax>();
        SyntaxNode newRoot;
        if (statement!.Declaration.Variables.Count == 1)
            newRoot = root.RemoveNode(statement, SyntaxRemoveOptions.KeepNoTrivia);
        else
        {
            var newDecl = statement.Declaration.WithVariables(SyntaxFactory.SeparatedList(statement.Declaration.Variables.Where(v => v != variable)));
            newRoot = root.ReplaceNode(statement, statement.WithDeclaration(newDecl));
        }

        var formatted = Formatter.Format(newRoot, SharedWorkspace);
        await File.WriteAllTextAsync(filePath, formatted.ToFullString());
        return $"Successfully deleted variable '{name}' in {filePath} (single file mode)";
    }
}
