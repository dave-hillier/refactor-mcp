using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

[McpServerToolType]
public static class ExtractMethodTool
{
    [McpServerTool, Description("Extract a code block into a new method (preferred for large C# file refactoring)")]
    public static async Task<string> ExtractMethod(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Range in format 'startLine:startColumn-endLine:endColumn'")] string selectionRange,
        [Description("Name for the new method")] string methodName)
    {
        try
        {
            return await RefactoringHelpers.RunWithSolutionOrFile(
                solutionPath,
                filePath,
                doc => ExtractMethodWithSolution(doc, selectionRange, methodName),
                path => ExtractMethodSingleFile(path, selectionRange, methodName));
        }
        catch (Exception ex)
        {
            throw new McpException($"Error extracting method: {ex.Message}", ex);
        }
    }

    private static async Task<string> ExtractMethodWithSolution(Document document, string selectionRange, string methodName)
    {
        var sourceText = await document.GetTextAsync();
        var syntaxRoot = await document.GetSyntaxRootAsync();
        var span = RefactoringHelpers.ParseSelectionRange(sourceText, selectionRange);

        var selectedNodes = syntaxRoot!.DescendantNodes()
            .Where(n => span.Contains(n.Span))
            .ToList();

        if (!selectedNodes.Any())
            throw new McpException("Error: No valid code selected");

        var containingMethod = selectedNodes.First().Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (containingMethod == null)
            throw new McpException("Error: Selected code is not within a method");
        if (containingMethod.Body == null)
        {
            if (containingMethod.ExpressionBody != null)
                throw new McpException("Error: Extraction from expression-bodied methods is not supported");

            throw new McpException("Error: Selected code is not within a block-bodied method");
        }

        var statementsToExtract = containingMethod.Body.Statements
            .Where(s => span.IntersectsWith(s.FullSpan))
            .ToList();

        if (!statementsToExtract.Any())
            throw new McpException("Error: Selected code does not contain extractable statements");

        var containingClass = containingMethod.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        var semanticModel = await document.GetSemanticModelAsync();
        EnsureDeclaredLocalsStayInside(containingMethod, statementsToExtract, semanticModel);
        EnsureAssignedLocalsStayInside(containingMethod, statementsToExtract, semanticModel);
        var rewriter = new ExtractMethodRewriter(containingMethod, containingClass, statementsToExtract, methodName, semanticModel, span);
        var newRoot = rewriter.Visit(syntaxRoot);

        var formattedRoot = Formatter.Format(newRoot!, document.Project.Solution.Workspace);
        await RefactoringHelpers.WriteAndUpdateCachesAsync(document, formattedRoot);

        return $"Successfully extracted method '{methodName}' from {selectionRange} in {document.FilePath} (solution mode)";
    }

    private static async Task<string> ExtractMethodSingleFile(string filePath, string selectionRange, string methodName)
    {
        var semanticModel = await RefactoringHelpers.GetOrCreateSemanticModelAsync(filePath);
        return await RefactoringHelpers.ApplySingleFileEdit(
            filePath,
            text => ExtractMethodInSource(text, selectionRange, methodName, semanticModel),
            $"Successfully extracted method '{methodName}' from {selectionRange} in {filePath} (single file mode)");
    }

    public static string ExtractMethodInSource(string sourceText, string selectionRange, string methodName, SemanticModel? model = null)
    {
        var syntaxTree = model?.SyntaxTree ?? CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = syntaxTree.GetRoot();
        var text = syntaxTree.GetText();
        var span = RefactoringHelpers.ParseSelectionRange(text, selectionRange);

        var selectedNodes = syntaxRoot.DescendantNodes()
            .Where(n => span.Contains(n.Span))
            .ToList();

        if (!selectedNodes.Any())
            throw new McpException("Error: No valid code selected");

        var containingMethod = selectedNodes.First().Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (containingMethod == null)
            throw new McpException("Error: Selected code is not within a method");
        if (containingMethod.Body == null)
        {
            if (containingMethod.ExpressionBody != null)
                throw new McpException("Error: Extraction from expression-bodied methods is not supported");

            throw new McpException("Error: Selected code is not within a block-bodied method");
        }

        var statementsToExtract = containingMethod.Body.Statements
            .Where(s => span.IntersectsWith(s.FullSpan))
            .ToList();

        if (!statementsToExtract.Any())
            throw new McpException("Error: Selected code does not contain extractable statements");

        var containingClass = containingMethod.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        EnsureDeclaredLocalsStayInside(containingMethod, statementsToExtract, model);
        EnsureAssignedLocalsStayInside(containingMethod, statementsToExtract, model);
        var rewriter = new ExtractMethodRewriter(containingMethod, containingClass, statementsToExtract, methodName, model, span);
        var newRoot = rewriter.Visit(syntaxRoot);

        var formattedRoot = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
        return formattedRoot.ToFullString();
    }

    /// <summary>
    /// A local the selected statements declare and the rest of the method goes on to use
    /// would be left behind with no declaration, so the extraction is refused rather than
    /// emitting code that cannot compile. The statements move whole, so only references
    /// outside them matter.
    /// </summary>
    private static void EnsureDeclaredLocalsStayInside(
        MethodDeclarationSyntax containingMethod,
        List<StatementSyntax> statements,
        SemanticModel? semanticModel)
    {
        if (semanticModel == null)
            return;

        var extractedSpan = TextSpan.FromBounds(statements.First().SpanStart, statements.Last().Span.End);
        var declared = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var node in statements.SelectMany(s => s.DescendantNodesAndSelf()))
        {
            var symbol = DeclaredLocal(node, semanticModel);
            if (symbol != null)
                declared.Add(symbol);
        }

        if (declared.Count == 0)
            return;

        foreach (var node in containingMethod.DescendantNodes())
        {
            if (node.SpanStart < extractedSpan.End || node is not SimpleNameSyntax name)
                continue;

            var symbol = semanticModel.GetSymbolInfo(node).Symbol;
            if (symbol == null || !declared.Contains(symbol))
                continue;

            var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            throw new McpException(
                $"Error: The extracted block declares '{name.Identifier.ValueText}', which is used at line {line}. " +
                "Include that code in the extraction, or narrow the selection.");
        }
    }

    /// <summary>
    /// A value the selected statements assign to a local or parameter from outside them,
    /// and that the rest of the method reads, would be assigned to a copy in the new
    /// method and lost, so the extraction is refused.
    /// </summary>
    private static void EnsureAssignedLocalsStayInside(
        MethodDeclarationSyntax containingMethod,
        List<StatementSyntax> statements,
        SemanticModel? semanticModel)
    {
        if (semanticModel == null)
            return;

        var dataFlow = semanticModel.AnalyzeDataFlow(statements.First(), statements.Last());
        if (dataFlow == null || !dataFlow.Succeeded || dataFlow.DataFlowsOut.IsEmpty)
            return;

        var extractedSpan = TextSpan.FromBounds(statements.First().SpanStart, statements.Last().Span.End);
        foreach (var node in containingMethod.DescendantNodes())
        {
            if (node.SpanStart < extractedSpan.End || node is not SimpleNameSyntax name)
                continue;

            var symbol = semanticModel.GetSymbolInfo(node).Symbol;
            if (symbol == null || !dataFlow.DataFlowsOut.Contains(symbol, SymbolEqualityComparer.Default))
                continue;

            var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            throw new McpException(
                $"Error: The extracted block assigns '{name.Identifier.ValueText}', which is used at line {line}. " +
                "Include that code in the extraction, or narrow the selection.");
        }
    }

    /// <summary>
    /// The symbol a node declares, for the node kinds an extracted statement can declare a
    /// local with: a local variable, a foreach variable, a pattern or deconstruction
    /// designation, or a local function.
    /// </summary>
    private static ISymbol? DeclaredLocal(SyntaxNode node, SemanticModel semanticModel)
    {
        return node switch
        {
            VariableDeclaratorSyntax variable => semanticModel.GetDeclaredSymbol(variable),
            ForEachStatementSyntax forEach => semanticModel.GetDeclaredSymbol(forEach),
            SingleVariableDesignationSyntax designation => semanticModel.GetDeclaredSymbol(designation),
            LocalFunctionStatementSyntax localFunction => semanticModel.GetDeclaredSymbol(localFunction),
            _ => null,
        };
    }

}
