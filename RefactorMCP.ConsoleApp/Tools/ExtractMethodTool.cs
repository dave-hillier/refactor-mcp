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
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Range in format 'startLine:startColumn-endLine:endColumn'")] string selectionRange,
        [Description("Name for the new method")] string methodName)
    {
        try
        {
            return await RefactoringHelpers.RunWithSolution(
                solutionPath,
                filePath,
                doc => ExtractMethodWithSolution(doc, selectionRange, methodName));
        }
        catch (Exception ex)
        {
            throw new McpException($"Error extracting method: {ex.Message}", ex);
        }
    }

    private static async Task<string> ExtractMethodWithSolution(Document document, string selectionRange, string methodName)
    {
        var sourceText = await document.GetTextAsync();
        var span = RefactoringHelpers.ParseSelectionRange(sourceText, selectionRange);

        var formattedRoot = await ExtractAsync(document, span, methodName);
        await RefactoringHelpers.WriteAndUpdateCachesAsync(document, formattedRoot);

        return $"Successfully extracted method '{methodName}' from {selectionRange} in {document.FilePath} (solution mode)";
    }

    /// <summary>
    /// The document's root, formatted, with the selection extracted into a new method.
    /// A selection covering exactly one expression extracts that expression into a
    /// method returning its value; any other selection extracts the statements it
    /// touches in the innermost block that holds all of it.
    /// </summary>
    internal static async Task<SyntaxNode> ExtractAsync(Document document, TextSpan span, string methodName)
    {
        var sourceText = await document.GetTextAsync();
        var syntaxRoot = (await document.GetSyntaxRootAsync())!;

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

        var containingClass = containingMethod.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        var semanticModel = (await document.GetSemanticModelAsync())!;

        // A name the class already uses can only be that of a method whose body is the
        // selected code, which the selection then calls instead of a new method.
        var nameTaken = semanticModel.GetDeclaredSymbol(containingMethod)?.ContainingType.GetMembers(methodName).Any() == true;

        SyntaxNode newRoot;
        ExpressionSyntax? extractedExpression = null;
        var expression = FieldPropertyRefactoring.SelectedExpression(syntaxRoot, sourceText, span);
        if (expression != null && expression.Parent is not ExpressionStatementSyntax && containingMethod.Body.Span.Contains(expression.Span))
        {
            EnsureExtractableExpression(expression, semanticModel);
            EnsureAssignedLocalsStayInside(containingMethod, expression.Span, semanticModel.AnalyzeDataFlow(expression), semanticModel);
            if (!nameTaken)
                extractedExpression = expression;
            newRoot = nameTaken
                ? await ExistingMethodCall.ReplaceAsync(document, semanticModel, containingMethod, new[] { expression }, methodName)
                : new ExtractMethodRewriter(containingMethod, containingClass, expression, methodName, semanticModel).Visit(syntaxRoot)!;
        }
        else
        {
            var statementsToExtract = SelectedStatements(containingMethod, sourceText, span);
            if (!statementsToExtract.Any())
                throw new McpException("Error: Selected code does not contain extractable statements");

            EnsureDeclaredLocalsStayInside(containingMethod, statementsToExtract, semanticModel);
            EnsureAssignedLocalsStayInside(
                containingMethod,
                TextSpan.FromBounds(statementsToExtract.First().SpanStart, statementsToExtract.Last().Span.End),
                semanticModel.AnalyzeDataFlow(statementsToExtract.First(), statementsToExtract.Last()),
                semanticModel);
            EnsureNoEarlyReturn(containingMethod, statementsToExtract, semanticModel);
            newRoot = nameTaken
                ? await ExistingMethodCall.ReplaceAsync(document, semanticModel, containingMethod, statementsToExtract, methodName)
                : new ExtractMethodRewriter(containingMethod, containingClass, statementsToExtract, methodName, semanticModel, span).Visit(syntaxRoot)!;
        }

        var formatted = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        return extractedExpression is null ? formatted : KeepContinuationIndents(formatted, extractedExpression, methodName);
    }

    /// <summary>
    /// An expression written over several lines keeps the indentation of its later
    /// lines relative to the statement it is in, as it had where it came from; the
    /// formatter would otherwise indent them from where it moved.
    /// </summary>
    private static SyntaxNode KeepContinuationIndents(SyntaxNode root, ExpressionSyntax original, string methodName)
    {
        var originalIndents = LineIndents(original).ToList();
        if (originalIndents.Count == 0)
            return root;

        var moved = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.ValueText == methodName)
            .Select(m => m.Body?.Statements.LastOrDefault() as ReturnStatementSyntax)
            .LastOrDefault(r => r?.Expression is not null && SyntaxFactory.AreEquivalent(r.Expression, original));
        if (moved is null)
            return root;

        var line = original.SyntaxTree.GetText().Lines.GetLineFromPosition(original.SpanStart).ToString();
        var originalBase = line[..(line.Length - line.TrimStart().Length)];
        var newBase = moved.GetLeadingTrivia().LastOrDefault(t => t.IsKind(SyntaxKind.WhitespaceTrivia)).ToString();

        var replacements = LineIndents(moved.Expression!).Zip(originalIndents).ToDictionary(
            pair => pair.First,
            pair => SyntaxFactory.Whitespace(newBase + (pair.Second.ToString().StartsWith(originalBase, StringComparison.Ordinal)
                ? pair.Second.ToString()[originalBase.Length..]
                : "")));
        return root.ReplaceTrivia(replacements.Keys, (trivia, _) => replacements[trivia]);
    }

    /// <summary>The whitespace that starts each line after the first, within a node.</summary>
    private static IEnumerable<SyntaxTrivia> LineIndents(SyntaxNode node)
    {
        var previous = default(SyntaxTrivia);
        foreach (var trivia in node.DescendantTrivia())
        {
            if (trivia.IsKind(SyntaxKind.WhitespaceTrivia) && previous.IsKind(SyntaxKind.EndOfLineTrivia))
                yield return trivia;
            previous = trivia;
        }
    }

    /// <summary>
    /// The statements the selection touches, in the innermost block or switch section of
    /// the method that holds all of it, or the single statement an if, else or loop runs
    /// without braces.
    /// A selection that spans several blocks takes whole statements of the block around
    /// them. Blocks of lambdas and local functions are not searched.
    /// </summary>
    private static List<StatementSyntax> SelectedStatements(MethodDeclarationSyntax containingMethod, SourceText text, TextSpan span)
    {
        var trimmed = Trim(text, span);
        SyntaxNode container = containingMethod.Body!;
        foreach (var node in containingMethod.Body!.DescendantNodes(n => n is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax))
        {
            var holdsSelection = node switch
            {
                BlockSyntax block => block.OpenBraceToken.Span.End <= trimmed.Start && trimmed.End <= block.CloseBraceToken.SpanStart,
                SwitchSectionSyntax { Statements.Count: > 0 } section =>
                    TextSpan.FromBounds(section.Statements.First().FullSpan.Start, section.Statements.Last().Span.End).Contains(trimmed),
                StatementSyntax statement => IsEmbedded(statement) && statement.Span.Contains(trimmed),
                _ => false,
            };
            if (holdsSelection && container.Span.Contains(node.Span))
                container = node;
        }

        return container switch
        {
            BlockSyntax inner => inner.Statements.Where(s => span.IntersectsWith(s.FullSpan)).ToList(),
            SwitchSectionSyntax section => section.Statements.Where(s => span.IntersectsWith(s.FullSpan)).ToList(),
            _ => new List<StatementSyntax> { (StatementSyntax)container },
        };
    }

    // A statement an if, else or loop runs directly, rather than one in a block.
    private static bool IsEmbedded(StatementSyntax statement) =>
        statement is not BlockSyntax &&
        statement.Parent is IfStatementSyntax or ElseClauseSyntax or WhileStatementSyntax or DoStatementSyntax
            or ForStatementSyntax or CommonForEachStatementSyntax or UsingStatementSyntax or LockStatementSyntax or FixedStatementSyntax;

    private static TextSpan Trim(SourceText text, TextSpan span)
    {
        var start = span.Start;
        var end = span.End;
        while (start < end && char.IsWhiteSpace(text[start]))
            start++;
        while (end > start && char.IsWhiteSpace(text[end - 1]))
            end--;
        return TextSpan.FromBounds(start, end);
    }

    /// <summary>
    /// An expression is extracted into a method returning its value, so it must have a
    /// value whose type can be written, and must not be the target of an assignment.
    /// </summary>
    private static void EnsureExtractableExpression(ExpressionSyntax expression, SemanticModel semanticModel)
    {
        var typeInfo = semanticModel.GetTypeInfo(expression);
        var type = typeInfo.Type ?? typeInfo.ConvertedType;
        if (type == null || type.SpecialType == SpecialType.System_Void || type is IErrorTypeSymbol || type.IsAnonymousType)
            throw new McpException("Error: The selected expression has no value a method could return");

        var assigned = expression.Parent switch
        {
            AssignmentExpressionSyntax assignment => assignment.Left == expression,
            ArgumentSyntax argument => !argument.RefKindKeyword.IsKind(SyntaxKind.None),
            PrefixUnaryExpressionSyntax or PostfixUnaryExpressionSyntax => LocalVariableTarget.IsWrite(expression),
            _ => false,
        };
        if (assigned)
            throw new McpException("Error: The selected expression is assigned to, so it cannot become a method call");
    }

    /// <summary>
    /// A bare return in the middle of the statements leaves the containing method; in
    /// the new method it would only leave that, and the caller would carry on.
    /// </summary>
    private static void EnsureNoEarlyReturn(MethodDeclarationSyntax containingMethod, List<StatementSyntax> statements, SemanticModel semanticModel)
    {
        var lastStatement = statements.Last();
        var early = statements
            .SelectMany(s => s.DescendantNodesAndSelf(n => n is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax))
            .OfType<ReturnStatementSyntax>()
            .FirstOrDefault(r => r.Expression == null && !(r == lastStatement && statements.Count > 1));
        if (early == null)
            return;

        var line = early.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        throw new McpException(
            $"Error: The extracted block returns from '{containingMethod.Identifier.ValueText}' at line {line}, which the new method cannot do for it");
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
    /// A value the extracted code assigns to a local or parameter from outside it, and
    /// that the rest of the method reads, would be assigned to a copy in the new method
    /// and lost, so the extraction is refused. In a loop the read can come before the
    /// extracted code, on the next iteration.
    /// </summary>
    private static void EnsureAssignedLocalsStayInside(
        MethodDeclarationSyntax containingMethod,
        TextSpan extractedSpan,
        DataFlowAnalysis? dataFlow,
        SemanticModel? semanticModel)
    {
        if (semanticModel == null)
            return;

        if (dataFlow == null || !dataFlow.Succeeded || dataFlow.DataFlowsOut.IsEmpty)
            return;

        var outside = containingMethod.DescendantNodes()
            .Where(n => n.SpanStart >= extractedSpan.End)
            .Concat(containingMethod.DescendantNodes().Where(n => n.Span.End <= extractedSpan.Start));
        foreach (var node in outside)
        {
            if (node is not SimpleNameSyntax name)
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
