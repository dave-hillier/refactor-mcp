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
            var (syntaxTree, model, solution) = await LoadSyntaxTreeAndModel(solutionPath, filePath);
            var root = await syntaxTree.GetRootAsync();

            var suggestions = new List<string>();

            AnalyzeMethodMetrics(root, syntaxTree, model, suggestions);
            AnalyzeClassMetrics(root, syntaxTree, suggestions);
            await AnalyzeUnusedMembersAsync(root, model, solution, suggestions);

            if (suggestions.Count == 0)
                return "No obvious refactoring opportunities detected";

            return "Suggestions:\n" + string.Join("\n", suggestions);
        }
        catch (Exception ex)
        {
            throw new McpException($"Error analyzing file: {ex.Message}", ex);
        }
    }

    private static async Task<(SyntaxTree tree, SemanticModel? model, Solution? solution)> LoadSyntaxTreeAndModel(string solutionPath, string filePath)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath);
        var document = RefactoringHelpers.GetDocumentByPath(solution, filePath);
        if (document != null)
        {
            var tree = await document.GetSyntaxTreeAsync() ?? CSharpSyntaxTree.ParseText(await File.ReadAllTextAsync(filePath));
            var model = await document.GetSemanticModelAsync();
            return (tree, model, solution);
        }

        var syntaxTree = CSharpSyntaxTree.ParseText(await File.ReadAllTextAsync(filePath));
        return (syntaxTree, null, null);
    }

    private static void AnalyzeMethodMetrics(SyntaxNode root, SyntaxTree syntaxTree, SemanticModel? model, List<string> suggestions)
    {
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
    }

    private static void AnalyzeClassMetrics(SyntaxNode root, SyntaxTree syntaxTree, List<string> suggestions)
    {
        foreach (var cls in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            var members = cls.Members.Count;
            var span = syntaxTree.GetLineSpan(cls.Span);
            var lines = span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
            if (members > 15 || lines > 300)
                suggestions.Add($"Class '{cls.Identifier}' is large ({members} members) -> consider splitting or move-method");
        }
    }

    private static async Task AnalyzeUnusedMembersAsync(SyntaxNode root, SemanticModel? model, Solution? solution, List<string> suggestions)
    {
        if (model != null)
        {
            await AnalyzeUnusedWithModelAsync(root, model, solution!, suggestions);
        }
        else
        {
            AnalyzeUnusedSingleFile(root, suggestions);
        }
    }

    private static async Task AnalyzeUnusedWithModelAsync(SyntaxNode root, SemanticModel model, Solution solution, List<string> suggestions)
    {
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        foreach (var method in methods)
        {
            if (method.Modifiers.Any(SyntaxKind.PublicKeyword))
                continue;

            if (model.GetDeclaredSymbol(method) is IMethodSymbol symbol)
            {
                var refs = await SymbolFinder.FindReferencesAsync(symbol, solution);
                if (refs.All(r => r.Locations.All(l => l.Location.SourceSpan == method.Identifier.Span)))
                    suggestions.Add($"Method '{method.Identifier}' appears unused -> safe-delete-method");
            }
        }

        var fields = root.DescendantNodes().OfType<FieldDeclarationSyntax>();
        foreach (var field in fields)
        {
            foreach (var variable in field.Declaration.Variables)
            {
                if (model.GetDeclaredSymbol(variable) is IFieldSymbol symbol)
                {
                    var refs = await SymbolFinder.FindReferencesAsync(symbol, solution);
                    if (refs.All(r => r.Locations.All(l => l.Location.SourceSpan == variable.Identifier.Span)))
                        suggestions.Add($"Field '{variable.Identifier}' appears unused -> safe-delete-field");
                }
            }
        }
    }

    private static void AnalyzeUnusedSingleFile(SyntaxNode root, List<string> suggestions)
    {
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
}
