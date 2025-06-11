using ModelContextProtocol.Server;
using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using System.ComponentModel;

[McpServerToolType, McpServerPromptType]
public static class AnalyzeRefactoringOpportunitiesTool
{
    [McpServerPrompt, Description("Analyze a C# file for refactoring opportunities like long methods or unused code")]
    public static async Task<string> AnalyzeRefactoringOpportunities(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath)
    {
        try
        {
            SyntaxTree syntaxTree;
            SemanticModel? model = null;
            Solution? solution = null;
            Document? document = null;

            solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath);
            document = RefactoringHelpers.GetDocumentByPath(solution, filePath);
            if (document != null)
            {
                syntaxTree = await document.GetSyntaxTreeAsync() ?? CSharpSyntaxTree.ParseText(await File.ReadAllTextAsync(filePath));
                model = await document.GetSemanticModelAsync();
            }
            else
            {
                syntaxTree = CSharpSyntaxTree.ParseText(await File.ReadAllTextAsync(filePath));
                model = null;
            }

            var root = await syntaxTree.GetRootAsync();
            var suggestions = new List<string>();

            foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                var span = syntaxTree.GetLineSpan(method.Span);
                var lines = span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
                if (lines > 30)
                    suggestions.Add($"Method '{method.Identifier}' is {lines} lines long -> consider extract-method");

                var parameters = method.ParameterList.Parameters.Count;
                if (parameters >= 5)
                    suggestions.Add($"Method '{method.Identifier}' has {parameters} parameters -> consider introducing parameter object");

                if (!method.Modifiers.Any(SyntaxKind.StaticKeyword) && model != null)
                {
                    var accessesInstance = method.DescendantNodes()
                        .Any(n => n is ThisExpressionSyntax ||
                                  n is MemberAccessExpressionSyntax ma &&
                                      model.GetSymbolInfo(ma).Symbol is { IsStatic: false });
                    if (!accessesInstance)
                        suggestions.Add($"Method '{method.Identifier}' does not access instance state -> convert-to-static-with-parameters");
                }
            }

            foreach (var cls in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                var members = cls.Members.Count;
                var span = syntaxTree.GetLineSpan(cls.Span);
                var lines = span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
                if (members > 15 || lines > 300)
                    suggestions.Add($"Class '{cls.Identifier}' is large ({members} members) -> consider splitting or move-method");
            }

            if (model != null)
            {
                var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
                foreach (var method in methods)
                {
                    if (method.Modifiers.Any(SyntaxKind.PublicKeyword))
                        continue;

                    var symbol = model.GetDeclaredSymbol(method) as IMethodSymbol;
                    if (symbol != null)
                    {
                        var refs = await SymbolFinder.FindReferencesAsync(symbol, solution!);
                        if (refs.All(r => r.Locations.All(l => l.Location.SourceSpan == method.Identifier.Span)))
                            suggestions.Add($"Method '{method.Identifier}' appears unused -> safe-delete-method");
                    }
                }

                var fields = root.DescendantNodes().OfType<FieldDeclarationSyntax>();
                foreach (var field in fields)
                {
                    foreach (var variable in field.Declaration.Variables)
                    {
                        var symbol = model.GetDeclaredSymbol(variable) as IFieldSymbol;
                        if (symbol != null)
                        {
                            var refs = await SymbolFinder.FindReferencesAsync(symbol, solution!);
                            if (refs.All(r => r.Locations.All(l => l.Location.SourceSpan == variable.Identifier.Span)))
                                suggestions.Add($"Field '{variable.Identifier}' appears unused -> safe-delete-field");
                        }
                    }
                }
            }
            else
            {
                // Simple unused code detection in single-file mode
                var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
                foreach (var method in methods)
                {
                    if (method.Modifiers.Any(SyntaxKind.PublicKeyword))
                        continue;

                    var identifier = method.Identifier.ValueText;
                    var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
                        .Select(inv => inv.Expression)
                        .OfType<IdentifierNameSyntax>()
                        .Count(id => id.Identifier.ValueText == identifier);
                    if (invocations == 0)
                        suggestions.Add($"Method '{identifier}' appears unused -> safe-delete-method");
                }

                // Simple unused field detection in single-file mode
                var fields = root.DescendantNodes().OfType<FieldDeclarationSyntax>();
                foreach (var field in fields)
                {
                    foreach (var variable in field.Declaration.Variables)
                    {
                        var identifier = variable.Identifier.ValueText;
                        var references = root.DescendantNodes().OfType<IdentifierNameSyntax>()
                            .Count(id => id.Identifier.ValueText == identifier);
                        if (references <= 1)
                            suggestions.Add($"Field '{identifier}' appears unused -> safe-delete-field");
                    }
                }
            }

            if (suggestions.Count == 0)
                return "No obvious refactoring opportunities detected";

            return "Suggestions:\n" + string.Join("\n", suggestions);
        }
        catch (Exception ex)
        {
            throw new McpException($"Error analyzing file: {ex.Message}", ex);
        }
    }
}
