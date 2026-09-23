using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Editing;

[McpServerToolType]
public static class IntroduceVariableTool
{
    [McpServerTool, Description("Introduce a new variable from selected expression (preferred for large C# file refactoring)")]
    public static async Task<string> IntroduceVariable(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Range in format 'startLine:startColumn-endLine:endColumn'")] string selectionRange,
        [Description("Name for the new variable")] string variableName)
    {
        try
        {
            return await RefactoringHelpers.RunWithSolutionOrFile(
                solutionPath,
                filePath,
                doc => IntroduceVariableWithSolution(doc, selectionRange, variableName),
                path => IntroduceVariableSingleFile(path, selectionRange, variableName));
        }
        catch (Exception ex)
        {
            throw new McpException($"Error introducing variable: {ex.Message}", ex);
        }
    }

    private static async Task<string> IntroduceVariableWithSolution(Document document, string selectionRange, string variableName)
    {
        var sourceText = await document.GetTextAsync();
        var syntaxRoot = await document.GetSyntaxRootAsync();
        var semanticModel = await document.GetSemanticModelAsync();
        var span = RefactoringHelpers.ParseSelectionRange(sourceText, selectionRange);

        var newRoot = Introduce(syntaxRoot!, span, variableName, semanticModel, document.Project.Solution.Workspace);
        var formatted = await Formatter.FormatAsync(document.WithSyntaxRoot(newRoot), Formatter.Annotation);
        await RefactoringHelpers.WriteAndUpdateCachesAsync(document, (await formatted.GetSyntaxRootAsync())!);

        return $"Successfully introduced variable '{variableName}' from {selectionRange} in {document.FilePath} (solution mode)";
    }

    private static async Task<string> IntroduceVariableSingleFile(string filePath, string selectionRange, string variableName)
    {
        filePath = RefactoringHelpers.ResolvePath(filePath)!;

        if (!File.Exists(filePath))
            throw new McpException($"Error: File {filePath} not found");

        var (sourceText, encoding) = await RefactoringHelpers.ReadFileWithEncodingAsync(filePath);
        var model = await RefactoringHelpers.GetOrCreateSemanticModelAsync(filePath);
        var newText = IntroduceVariableInSource(sourceText, selectionRange, variableName, model);
        await File.WriteAllTextAsync(filePath, newText, encoding);
        RefactoringHelpers.UpdateFileCaches(filePath, newText);
        return $"Successfully introduced variable '{variableName}' from {selectionRange} in {filePath} (single file mode)";
    }

    public static string IntroduceVariableInSource(string sourceText, string selectionRange, string variableName, SemanticModel? model = null)
    {
        var syntaxTree = model?.SyntaxTree ?? CSharpSyntaxTree.ParseText(sourceText);
        var span = RefactoringHelpers.ParseSelectionRange(syntaxTree.GetText(), selectionRange);

        var newRoot = Introduce(syntaxTree.GetRoot(), span, variableName, model, RefactoringHelpers.SharedWorkspace);
        return Formatter.Format(newRoot, Formatter.Annotation, RefactoringHelpers.SharedWorkspace).ToFullString();
    }

    /// <summary>
    /// Declares a local holding the selected expression just before the statement that
    /// contains it, and uses the local in its place. Without a semantic model the local
    /// is declared with var and the checks that need symbols are skipped.
    /// </summary>
    private static SyntaxNode Introduce(SyntaxNode root, TextSpan span, string variableName, SemanticModel? model, Workspace workspace)
    {
        var selected = SelectedExpression(root, span);
        var value = selected is ParenthesizedExpressionSyntax parenthesized ? parenthesized.Expression : selected;

        var statement = selected.Ancestors().OfType<StatementSyntax>().FirstOrDefault();
        if (statement == null)
        {
            if (selected.Ancestors().Any(a => a is ArrowExpressionClauseSyntax))
                throw new McpException("Error: Introducing a variable in an expression-bodied member is not supported");

            throw new McpException("Error: The selected expression is not inside a statement");
        }

        EnsureNotInLoopCondition(selected, statement);
        EnsureDeclaredBefore(selected, statement, model);
        EnsureAlwaysEvaluated(selected, statement);

        var type = model == null
            ? SyntaxFactory.IdentifierName("var")
            : DeclaredType(value, statement, model);
        EnsureNameIsFree(statement, variableName, model);

        var declaration = SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(
                    type,
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.VariableDeclarator(variableName)
                            .WithInitializer(SyntaxFactory.EqualsValueClause(value.WithoutTrivia())))))
            .WithAdditionalAnnotations(Formatter.Annotation);

        var editor = new SyntaxEditor(root, workspace.Services);
        foreach (var occurrence in Occurrences(selected, value, statement))
        {
            // Parentheses around the expression have nothing left to group.
            var replaced = occurrence.Parent is ParenthesizedExpressionSyntax parentheses ? parentheses : occurrence;
            editor.ReplaceNode(replaced, SyntaxFactory.IdentifierName(variableName).WithTriviaFrom(replaced));
        }

        if (statement.Parent is BlockSyntax or SwitchSectionSyntax)
        {
            // Comments above the statement introduce it, so they stay above the declaration.
            editor.InsertBefore(statement, declaration.WithLeadingTrivia(statement.GetLeadingTrivia()));
            editor.ReplaceNode(statement, (current, _) => current
                .WithLeadingTrivia(SyntaxFactory.ElasticMarker)
                .WithAdditionalAnnotations(Formatter.Annotation));
        }
        else
        {
            // An embedded statement, such as the body of an if without braces, becomes a
            // block so there is somewhere to declare the local.
            editor.ReplaceNode(statement, (current, _) => SyntaxFactory.Block(declaration, (StatementSyntax)current.WithoutTrivia())
                .WithTriviaFrom(current)
                .WithAdditionalAnnotations(Formatter.Annotation));
        }

        return editor.GetChangedRoot();
    }

    private static ExpressionSyntax SelectedExpression(SyntaxNode root, TextSpan span)
    {
        var selected = root.DescendantNodes()
            .OfType<ExpressionSyntax>()
            .Where(e => span.Contains(e.Span) || e.Span.Contains(span))
            .OrderBy(e => Math.Abs(e.Span.Length - span.Length))
            .ThenBy(e => e.Span.Length)
            .FirstOrDefault();
        if (selected?.Parent is ParenthesizedExpressionSyntax paren && paren.Span.Contains(span))
            selected = paren;

        return selected ?? throw new McpException("Error: Selected code is not a valid expression");
    }

    /// <summary>
    /// The expression's type as briefly as it can be written before the statement, with
    /// its nullable annotation. An anonymous type cannot be written, so it takes var.
    /// </summary>
    private static TypeSyntax DeclaredType(ExpressionSyntax value, StatementSyntax statement, SemanticModel model)
    {
        var type = model.GetTypeInfo(value).Type;
        if (type?.SpecialType == SpecialType.System_Void)
            throw new McpException("Error: The selected expression has no value to hold in a variable");
        if (type == null || type.TypeKind == TypeKind.Error || LocalVariableTarget.IsAnonymous(type))
            return SyntaxFactory.IdentifierName("var");

        return SyntaxFactory.ParseTypeName(type.ToMinimalDisplayString(model, statement.SpanStart));
    }

    /// <summary>
    /// A loop's condition and increments run on every iteration; a local declared before
    /// the loop would be computed once.
    /// </summary>
    private static void EnsureNotInLoopCondition(ExpressionSyntax selected, StatementSyntax statement)
    {
        var repeated = statement switch
        {
            WhileStatementSyntax whileLoop => whileLoop.Condition.Span.Contains(selected.Span),
            DoStatementSyntax doLoop => doLoop.Condition.Span.Contains(selected.Span),
            ForStatementSyntax forLoop => (forLoop.Condition?.Span.Contains(selected.Span) ?? false) ||
                                          forLoop.Incrementors.Any(i => i.Span.Contains(selected.Span)),
            _ => false,
        };

        if (repeated)
            throw new McpException("Error: The selected expression is part of a loop condition, which is evaluated on every iteration");
    }

    /// <summary>
    /// A lambda parameter, or a variable a pattern or out argument declares, outside the
    /// selection does not exist before the statement, so the declaration could not read it.
    /// </summary>
    private static void EnsureDeclaredBefore(ExpressionSyntax selected, StatementSyntax statement, SemanticModel? model)
    {
        if (model == null)
            return;

        foreach (var name in selected.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            var symbol = model.GetSymbolInfo(name).Symbol;
            if (symbol is not (ILocalSymbol or IParameterSymbol or IRangeVariableSymbol))
                continue;

            // A lambda inside the selection declares its parameters inside it too, and
            // they move with it.
            if (symbol.DeclaringSyntaxReferences.Any(r => r.SyntaxTree == statement.SyntaxTree &&
                                                          statement.Span.Contains(r.Span) &&
                                                          !selected.Span.Contains(r.Span)))
                throw new McpException($"Error: The selected expression uses '{symbol.Name}', which is declared inside the statement");
        }
    }

    /// <summary>
    /// An expression that runs only on some paths, or later, must not be hoisted to run
    /// unconditionally before the statement.
    /// </summary>
    private static void EnsureAlwaysEvaluated(ExpressionSyntax selected, StatementSyntax statement)
    {
        if (IsConditionallyEvaluated(selected, statement))
            throw new McpException("Error: The selected expression is only evaluated on some paths, or later, so it cannot be computed before the statement");
    }

    private static bool IsConditionallyEvaluated(SyntaxNode node, StatementSyntax statement)
    {
        for (var child = node; child.Parent != null && child != statement; child = child.Parent)
        {
            var conditional = child.Parent switch
            {
                BinaryExpressionSyntax binary => binary.Right == child && binary.Kind() is
                    SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression or SyntaxKind.CoalesceExpression,
                AssignmentExpressionSyntax assignment => assignment.Right == child && assignment.IsKind(SyntaxKind.CoalesceAssignmentExpression),
                ConditionalExpressionSyntax condition => condition.Condition != child,
                ConditionalAccessExpressionSyntax access => access.WhenNotNull == child,
                SwitchExpressionArmSyntax => true,
                AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax => true,
                _ => false,
            };
            if (conditional)
                return true;
        }

        return false;
    }

    /// <summary>
    /// The selected expression, and when it has no side effects, the identical
    /// expressions elsewhere in the same statement that are evaluated whenever it is.
    /// </summary>
    private static IEnumerable<ExpressionSyntax> Occurrences(ExpressionSyntax selected, ExpressionSyntax value, StatementSyntax statement)
    {
        yield return value;
        if (HasSideEffects(value))
            yield break;

        foreach (var other in statement.DescendantNodes().OfType<ExpressionSyntax>())
        {
            if (other == value || selected.Span.IntersectsWith(other.Span) || !SyntaxFactory.AreEquivalent(other, value))
                continue;
            if (!IsConditionallyEvaluated(other, statement))
                yield return other;
        }
    }

    private static bool HasSideEffects(ExpressionSyntax value)
    {
        return value.DescendantNodesAndSelf().Any(n => n is InvocationExpressionSyntax
            or BaseObjectCreationExpressionSyntax
            or AssignmentExpressionSyntax
            or AwaitExpressionSyntax
            or PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PreIncrementExpression or (int)SyntaxKind.PreDecrementExpression }
            or PostfixUnaryExpressionSyntax);
    }

    /// <summary>
    /// The name must not already mean something where the local is declared or in the
    /// rest of the block, where the local would hide or clash with it.
    /// </summary>
    private static void EnsureNameIsFree(StatementSyntax statement, string name, SemanticModel? model)
    {
        var scope = statement.Parent ?? statement;
        var clash = scope.DescendantNodes()
            .Where(n => n.SpanStart >= statement.SpanStart)
            .Any(n => n switch
            {
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText == name,
                VariableDeclaratorSyntax declarator => declarator.Identifier.ValueText == name,
                SingleVariableDesignationSyntax designation => designation.Identifier.ValueText == name,
                ParameterSyntax parameter => parameter.Identifier.ValueText == name,
                _ => false,
            });

        var visible = model?.LookupSymbols(statement.SpanStart, name: name)
            .Any(s => s is ILocalSymbol or IParameterSymbol or IRangeVariableSymbol) ?? false;

        if (clash || visible)
            throw new McpException($"Error: '{name}' is already declared or used where the variable would be declared");
    }
}
