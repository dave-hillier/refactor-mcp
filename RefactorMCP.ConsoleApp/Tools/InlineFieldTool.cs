using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using System.Linq;

[McpServerToolType]
public static class InlineFieldTool
{
    [McpServerTool, Description("Replace every read of a field that is only assigned by its initializer with that initializer, then remove the field")]
    public static async Task<string> InlineField(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the field")] string filePath,
        [Description("Name of the field to inline")] string fieldName)
    {
        try
        {
            var document = await FieldPropertyRefactoring.GetDocumentAsync(solutionPath, filePath);
            var field = await FieldPropertyRefactoring.FindFieldAsync(document, fieldName);
            fieldName = field.Name;
            var variable = await FieldPropertyRefactoring.DeclarationAsync<VariableDeclaratorSyntax>(field);
            if (variable.Initializer is null)
                throw new McpException($"Error: Field '{fieldName}' has no initializer to inline");

            var solution = document.Project.Solution;
            var references = (await SymbolFinder.FindReferencesAsync(field, solution)).ToList();
            foreach (var location in references.SelectMany(r => r.Locations))
            {
                var root = await location.Document.GetSyntaxRootAsync();
                var name = (ExpressionSyntax)root!.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true);
                if (FieldPropertyRefactoring.IsWrite(name))
                    throw new McpException($"Error: Field '{fieldName}' is assigned outside its initializer, at {location.Location.GetLineSpan()}");
            }

            var model = (await solution.GetDocument(variable.SyntaxTree)!.GetSemanticModelAsync())!;
            if (!IsStable(variable.Initializer.Value, model))
                throw new McpException($"Error: The initializer of '{fieldName}' may give a different value each time it is evaluated, so it cannot be inlined");

            var inlined = await FieldPropertyRefactoring.InlineFieldValueAsync(solution, field, references);
            await FieldPropertyRefactoring.WriteChangesAsync(solution, inlined);
            return $"Successfully inlined field '{fieldName}'";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error inlining field: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// True when evaluating the expression again anywhere gives the same value
    /// with no side effects: constants, literals, and operators applied to
    /// constant or static readonly fields. Object creation and calls are not.
    /// </summary>
    private static bool IsStable(ExpressionSyntax expression, SemanticModel model)
    {
        if (model.GetConstantValue(expression).HasValue)
            return true;

        return expression switch
        {
            LiteralExpressionSyntax or DefaultExpressionSyntax or TypeOfExpressionSyntax => true,
            ParenthesizedExpressionSyntax parenthesized => IsStable(parenthesized.Expression, model),
            CastExpressionSyntax cast => IsBuiltIn(cast, model) && IsStable(cast.Expression, model),
            PrefixUnaryExpressionSyntax unary => IsBuiltIn(unary, model) && IsStable(unary.Operand, model),
            BinaryExpressionSyntax binary => IsBuiltIn(binary, model) && IsStable(binary.Left, model) && IsStable(binary.Right, model),
            ConditionalExpressionSyntax conditional => IsStable(conditional.Condition, model)
                && IsStable(conditional.WhenTrue, model) && IsStable(conditional.WhenFalse, model),
            IdentifierNameSyntax or MemberAccessExpressionSyntax =>
                model.GetSymbolInfo(expression).Symbol is IFieldSymbol { IsConst: true } or IFieldSymbol { IsStatic: true, IsReadOnly: true },
            _ => false,
        };
    }

    /// <summary>A user-defined operator or conversion could do anything, so only built-in ones are stable.</summary>
    private static bool IsBuiltIn(ExpressionSyntax operation, SemanticModel model) =>
        model.GetSymbolInfo(operation).Symbol is not IMethodSymbol { MethodKind: MethodKind.UserDefinedOperator or MethodKind.Conversion };
}
