using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System.Linq;

[McpServerToolType]
public static class IntroduceConstantTool
{
    [McpServerTool, Description("Introduce a constant from a selected literal or constant expression, optionally replacing every occurrence of the same value in the type")]
    public static async Task<string> IntroduceConstant(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Range in format 'startLine:startColumn-endLine:endColumn'")] string selectionRange,
        [Description("Name for the new constant")] string constantName,
        [Description("Also replace every other occurrence of the same value in the containing type")] bool replaceAll = false,
        [Description("Access modifier (private, public, protected, internal)")] string accessModifier = "private")
    {
        try
        {
            var document = await FieldPropertyRefactoring.GetDocumentAsync(solutionPath, filePath);
            var text = await document.GetTextAsync();
            var root = (await document.GetSyntaxRootAsync())!;
            var model = (await document.GetSemanticModelAsync())!;

            var expression = FieldPropertyRefactoring.SelectedExpression(root, text, RefactoringHelpers.ParseSelectionRange(text, selectionRange))
                ?? throw new McpException("Error: The selection is not an expression");
            var type = model.GetTypeInfo(expression).Type;
            if (!model.GetConstantValue(expression).HasValue || type is null)
                throw new McpException("Error: The selected expression is not a compile-time constant");

            var local = expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
                .Select(name => model.GetSymbolInfo(name).Symbol)
                .OfType<ILocalSymbol>()
                .FirstOrDefault();
            if (local is not null)
                throw new McpException($"Error: The expression uses the local constant '{local.Name}', which a constant of the type could not see");

            var containingType = expression.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()
                ?? throw new McpException("Error: The expression is not inside a type");
            if (FieldPropertyRefactoring.HasMemberNamed(model.GetDeclaredSymbol(containingType)!, constantName))
                throw new McpException($"Error: The type already has a member named '{constantName}'");

            var occurrences = replaceAll
                ? Occurrences(containingType, expression, type, model)
                : new[] { expression };

            var editor = await DocumentEditor.CreateAsync(document);
            foreach (var occurrence in occurrences)
                editor.ReplaceNode(occurrence, SyntaxFactory.IdentifierName(constantName).WithTriviaFrom(occurrence));

            var constant = ConstantDeclaration(type, constantName, accessModifier, expression, model);
            editor.ReplaceNode(containingType, (node, _) => FieldPropertyRefactoring.AddField((TypeDeclarationSyntax)node, constant));

            await FieldPropertyRefactoring.WriteChangesAsync(document.Project.Solution, editor.GetChangedDocument().Project.Solution);
            return $"Successfully introduced constant '{constantName}' replacing {occurrences.Count} occurrence(s) in {document.FilePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error introducing constant: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Expressions in the type written the same way as the selected one and
    /// of the same type, so <c>60</c> does not match <c>60.0</c> or <c>"60"</c>.
    /// </summary>
    private static IReadOnlyList<ExpressionSyntax> Occurrences(TypeDeclarationSyntax type, ExpressionSyntax selected, ITypeSymbol valueType, SemanticModel model)
    {
        return type.DescendantNodes()
            .OfType<ExpressionSyntax>()
            .Where(e => SyntaxFactory.AreEquivalent(e, selected)
                && SymbolEqualityComparer.Default.Equals(model.GetTypeInfo(e).Type, valueType))
            .Where(e => !e.Ancestors().OfType<ExpressionSyntax>().Any(a => SyntaxFactory.AreEquivalent(a, selected)))
            .ToList();
    }

    private static FieldDeclarationSyntax ConstantDeclaration(ITypeSymbol type, string name, string accessModifier, ExpressionSyntax value, SemanticModel model)
    {
        while (value is ParenthesizedExpressionSyntax parenthesized)
            value = parenthesized.Expression;

        var typeSyntax = SyntaxFactory.ParseTypeName(type.ToMinimalDisplayString(model, value.SpanStart));
        return SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration(
                    typeSyntax,
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.VariableDeclarator(name)
                            .WithInitializer(SyntaxFactory.EqualsValueClause(value.WithoutTrivia())))))
            .WithModifiers(SyntaxFactory.TokenList(
                FieldPropertyRefactoring.AccessibilityToken(accessModifier),
                SyntaxFactory.Token(SyntaxKind.ConstKeyword)));
    }
}
