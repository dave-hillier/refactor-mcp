using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using System.Threading;

[McpServerToolType]
public static class InlineLocalVariableTool
{
    [McpServerTool, Description("Replace every use of a local variable with its initializer and remove the declaration")]
    public static async Task<string> InlineLocalVariable(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line of the local's declaration or of a use of it (1-based)")] int line,
        [Description("Column of the local's name on that line (1-based)")] int column,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var target = await LocalVariableTarget.FindAsync(solutionPath, filePath, line, column, cancellationToken);
            var statement = target.DeclarationStatement
                ?? throw new McpException($"Error: '{target.Name}' is not declared by a local declaration statement");

            if (target.Declarator.Initializer is not { } initializer)
                throw new McpException($"Error: '{target.Name}' has no initializer to inline");
            if (statement.UsingKeyword != default)
                throw new McpException($"Error: '{target.Name}' is a using declaration, which would no longer be disposed");
            if (target.Local.IsRef)
                throw new McpException($"Error: '{target.Name}' is a ref local");
            if (target.SiblingStatements() is null)
                throw new McpException($"Error: The declaration of '{target.Name}' is not in a block");

            var references = target.References().ToList();
            var write = references.FirstOrDefault(LocalVariableTarget.IsWrite);
            if (write != null)
                throw new McpException($"Error: '{target.Name}' is assigned after its declaration, at line {LineOf(write)}");
            if (references.Count == 0)
                throw new McpException($"Error: '{target.Name}' is never used");
            if (references.Count > 1 && ExpressionFacts.HasSideEffects(initializer.Value))
                throw new McpException(
                    $"Error: The initializer of '{target.Name}' has side effects, which would run at each of its {references.Count} uses");

            EnsureInputsUnchanged(target, initializer.Value, references);

            var replacement = Replacement(target, initializer.Value);
            var editor = await target.EditorAsync();
            foreach (var reference in references)
            {
                editor.ReplaceNode(reference, replacement
                    .WithTriviaFrom(reference)
                    .WithAdditionalAnnotations(Formatter.Annotation));
            }

            if (target.Declaration.Variables.Count == 1)
                target.RemoveDeclarationStatement(editor);
            else
                editor.RemoveNode(target.Declarator);

            await target.WriteAsync(editor);

            return $"Successfully inlined '{target.Name}' at {references.Count} use(s) in {filePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error inlining local variable: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// The initializer as it reads at a use. It is parenthesised for the simplifier to
    /// remove where precedence allows. A conversion the declaration performed is kept as
    /// a cast, which the simplifier also removes when it changes nothing, and an
    /// initializer that took its type from the declaration names the type.
    /// </summary>
    private static ExpressionSyntax Replacement(LocalVariableTarget target, ExpressionSyntax value)
    {
        var expression = value.WithoutTrivia();
        switch (expression)
        {
            case ImplicitObjectCreationExpressionSyntax implicitCreation:
                return SyntaxFactory.ObjectCreationExpression(
                    target.TypeSyntax(),
                    implicitCreation.ArgumentList,
                    implicitCreation.Initializer);
            case InitializerExpressionSyntax arrayInitializer when target.TypeSyntax() is ArrayTypeSyntax arrayType:
                return SyntaxFactory.ArrayCreationExpression(arrayType, arrayInitializer);
        }

        var parenthesized = SyntaxFactory.ParenthesizedExpression(expression)
            .WithAdditionalAnnotations(Simplifier.Annotation);
        var type = target.Model.GetTypeInfo(value).Type;
        if (type != null && SymbolEqualityComparer.Default.Equals(type, target.Local.Type))
            return parenthesized;

        return SyntaxFactory.ParenthesizedExpression(
                SyntaxFactory.CastExpression(target.TypeSyntax(), parenthesized)
                    .WithAdditionalAnnotations(Simplifier.Annotation))
            .WithAdditionalAnnotations(Simplifier.Annotation);
    }

    /// <summary>
    /// The initializer is evaluated at each use instead of at the declaration, so a
    /// variable it reads must not be assigned in between. A use inside a loop that does
    /// not contain the declaration is evaluated again on each iteration, so the whole
    /// loop counts as in between.
    /// </summary>
    private static void EnsureInputsUnchanged(LocalVariableTarget target, ExpressionSyntax value, List<IdentifierNameSyntax> references)
    {
        var inputs = value.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Select(i => target.Model.GetSymbolInfo(i).Symbol)
            .Where(s => s is ILocalSymbol or IParameterSymbol or IFieldSymbol)
            .ToHashSet(SymbolEqualityComparer.Default);
        if (inputs.Count == 0)
            return;

        var start = target.DeclarationStatement!.Span.End;
        var end = references.Max(r => EvaluatedUntil(r, target.DeclarationStatement!));
        foreach (var name in target.EnclosingMember().DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (name.SpanStart < start || name.SpanStart > end || !LocalVariableTarget.IsWrite(name))
                continue;

            var symbol = target.Model.GetSymbolInfo(name).Symbol;
            if (symbol != null && inputs.Contains(symbol))
            {
                throw new McpException(
                    $"Error: '{name.Identifier.ValueText}', which the initializer of '{target.Name}' reads, " +
                    $"is assigned at line {LineOf(name)} before a use");
            }
        }
    }

    private static int EvaluatedUntil(IdentifierNameSyntax reference, StatementSyntax declaration)
    {
        var loop = reference.Ancestors()
            .Where(a => a is ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax)
            .LastOrDefault(a => !a.Span.Contains(declaration.Span));
        return loop?.Span.End ?? reference.SpanStart;
    }

    private static int LineOf(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
}
