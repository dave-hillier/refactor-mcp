using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;

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
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath);
            var document = RefactoringHelpers.GetDocumentByPath(solution, filePath);
            if (document != null)
                return await SafeDeleteFieldWithSolution(document, fieldName);

            return await SafeDeleteFieldSingleFile(filePath, fieldName);
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
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath);
            var document = RefactoringHelpers.GetDocumentByPath(solution, filePath);
            if (document != null)
                return await SafeDeleteMethodWithSolution(document, methodName);

            return await SafeDeleteMethodSingleFile(filePath, methodName);
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
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath);
            var document = RefactoringHelpers.GetDocumentByPath(solution, filePath);
            if (document != null)
                return await SafeDeleteParameterWithSolution(document, methodName, parameterName);

            return await SafeDeleteParameterSingleFile(filePath, methodName, parameterName);
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
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath);
            var document = RefactoringHelpers.GetDocumentByPath(solution, filePath);
            if (document != null)
                return await SafeDeleteVariableWithSolution(document, selectionRange);

            return await SafeDeleteVariableSingleFile(filePath, selectionRange);
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
            return RefactoringHelpers.ThrowMcpException($"Error: Field '{fieldName}' not found");

        var variable = field.Declaration.Variables.First(v => v.Identifier.ValueText == fieldName);
        var semanticModel = await document.GetSemanticModelAsync();
        var symbol = semanticModel!.GetDeclaredSymbol(variable) as IFieldSymbol;
        var refs = await SymbolFinder.FindReferencesAsync(symbol!, document.Project.Solution);
        var count = refs.SelectMany(r => r.Locations).Count() - 1;
        if (count > 0)
            return RefactoringHelpers.ThrowMcpException($"Error: Field '{fieldName}' is referenced {count} time(s)");

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
            return RefactoringHelpers.ThrowMcpException($"Error: Field '{fieldName}' not found");

        var references = root.DescendantNodes().OfType<IdentifierNameSyntax>().Count(id => id.Identifier.ValueText == fieldName);
        if (references > 1)
            return RefactoringHelpers.ThrowMcpException($"Error: Field '{fieldName}' is referenced");

        SyntaxNode newRoot;
        if (field.Declaration.Variables.Count == 1)
            newRoot = root.RemoveNode(field, SyntaxRemoveOptions.KeepNoTrivia);
        else
        {
            var newDecl = field.Declaration.WithVariables(SyntaxFactory.SeparatedList(field.Declaration.Variables.Where(v => v.Identifier.ValueText != fieldName)));
            newRoot = root.ReplaceNode(field, field.WithDeclaration(newDecl));
        }

        var formatted = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }

    private static async Task<string> SafeDeleteMethodWithSolution(Document document, string methodName)
    {
        var root = await document.GetSyntaxRootAsync();
        var method = root!.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            return RefactoringHelpers.ThrowMcpException($"Error: Method '{methodName}' not found");

        var semanticModel = await document.GetSemanticModelAsync();
        var symbol = semanticModel!.GetDeclaredSymbol(method)!;
        var refs = await SymbolFinder.FindReferencesAsync(symbol, document.Project.Solution);
        var count = refs.SelectMany(r => r.Locations).Count() - 1;
        if (count > 0)
            return RefactoringHelpers.ThrowMcpException($"Error: Method '{methodName}' is referenced {count} time(s)");

        var newRoot = root.RemoveNode(method, SyntaxRemoveOptions.KeepNoTrivia);
        var formatted = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        var newDoc = document.WithSyntaxRoot(formatted);
        var text = await newDoc.GetTextAsync();
        await File.WriteAllTextAsync(document.FilePath!, text.ToString());
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
            return RefactoringHelpers.ThrowMcpException($"Error: Method '{methodName}' not found");

        var references = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Count(inv => inv.Expression is IdentifierNameSyntax id && id.Identifier.ValueText == methodName);
        if (references > 0)
            return RefactoringHelpers.ThrowMcpException($"Error: Method '{methodName}' is referenced");

        var newRoot = root.RemoveNode(method, SyntaxRemoveOptions.KeepNoTrivia);
        var formatted = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }

    private static async Task<string> SafeDeleteParameterWithSolution(Document document, string methodName, string parameterName)
    {
        var root = await document.GetSyntaxRootAsync();
        var method = root!.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            return RefactoringHelpers.ThrowMcpException($"Error: Method '{methodName}' not found");

        var parameter = method.ParameterList.Parameters.FirstOrDefault(p => p.Identifier.ValueText == parameterName);
        if (parameter == null)
            return RefactoringHelpers.ThrowMcpException($"Error: Parameter '{parameterName}' not found");

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
            return RefactoringHelpers.ThrowMcpException($"Error: Method '{methodName}' not found");

        var parameter = method.ParameterList.Parameters.FirstOrDefault(p => p.Identifier.ValueText == parameterName);
        if (parameter == null)
            return RefactoringHelpers.ThrowMcpException($"Error: Parameter '{parameterName}' not found");

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
        var formatted = Formatter.Format(root, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }

    private static async Task<string> SafeDeleteVariableWithSolution(Document document, string selectionRange)
    {
        var text = await document.GetTextAsync();
        var root = await document.GetSyntaxRootAsync();
        if (!RefactoringHelpers.TryParseRange(selectionRange, out var sl, out var sc, out var el, out var ec))
            return RefactoringHelpers.ThrowMcpException("Error: Invalid selection range format");

        var start = text.Lines[sl - 1].Start + sc - 1;
        var end = text.Lines[el - 1].Start + ec - 1;
        var span = TextSpan.FromBounds(start, end);
        var variable = root!.DescendantNodes(span).OfType<VariableDeclaratorSyntax>().FirstOrDefault();
        if (variable == null)
            return RefactoringHelpers.ThrowMcpException("Error: No variable declaration found in range");

        var semanticModel = await document.GetSemanticModelAsync();
        var symbol = semanticModel!.GetDeclaredSymbol(variable)!;
        var refs = await SymbolFinder.FindReferencesAsync(symbol, document.Project.Solution);
        var count = refs.SelectMany(r => r.Locations).Count() - 1;
        if (count > 0)
            return RefactoringHelpers.ThrowMcpException($"Error: Variable '{variable.Identifier.ValueText}' is referenced {count} time(s)");

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
        var lines = SourceText.From(sourceText).Lines;
        if (!RefactoringHelpers.TryParseRange(selectionRange, out var sl, out var sc, out var el, out var ec))
            return RefactoringHelpers.ThrowMcpException("Error: Invalid selection range format");

        var start = lines[sl - 1].Start + sc - 1;
        var end = lines[el - 1].Start + ec - 1;
        var span = TextSpan.FromBounds(start, end);
        var variable = root.DescendantNodes(span).OfType<VariableDeclaratorSyntax>().FirstOrDefault();
        if (variable == null)
            return RefactoringHelpers.ThrowMcpException("Error: No variable declaration found in range");

        var name = variable.Identifier.ValueText;
        var references = root.DescendantNodes().OfType<IdentifierNameSyntax>().Count(id => id.Identifier.ValueText == name);
        if (references > 1)
            return RefactoringHelpers.ThrowMcpException($"Error: Variable '{name}' is referenced");

        var statement = variable.FirstAncestorOrSelf<LocalDeclarationStatementSyntax>();
        SyntaxNode newRoot;
        if (statement!.Declaration.Variables.Count == 1)
            newRoot = root.RemoveNode(statement, SyntaxRemoveOptions.KeepNoTrivia);
        else
        {
            var newDecl = statement.Declaration.WithVariables(SyntaxFactory.SeparatedList(statement.Declaration.Variables.Where(v => v != variable)));
            newRoot = root.ReplaceNode(statement, statement.WithDeclaration(newDecl));
        }

        var formatted = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }
}
