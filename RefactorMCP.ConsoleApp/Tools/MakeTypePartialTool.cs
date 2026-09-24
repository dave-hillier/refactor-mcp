using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[McpServerToolType]
public static class MakeTypePartialTool
{
    [McpServerTool, Description("Add the partial modifier to a class, struct, record or interface")]
    public static async Task<string> MakeTypePartial(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the type")] string filePath,
        [Description("Name of the type")] string typeName,
        [Description("A line of the declaration (1-based), to choose between types of the same name")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var type = await TypeDeclarations.FindTypeAsync(solution, filePath, typeName, line, cancellationToken);
            var declaration = await TypeDeclarations.SingleDeclarationAsync(type, cancellationToken);

            if (declaration is not TypeDeclarationSyntax typeDeclaration)
                throw new McpException($"Error: '{typeName}' is {KindOf(declaration)}, which cannot be partial");

            if (typeDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword))
                throw new McpException($"Error: '{typeName}' is already partial");

            var document = solution.GetDocument(typeDeclaration.SyntaxTree)!;
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var changed = solution.WithDocumentSyntaxRoot(
                document.Id,
                root!.ReplaceNode(typeDeclaration, WithPartial(typeDeclaration)));

            await SolutionEdits.EnsureCompilesAsync(solution, changed, cancellationToken);
            await SolutionEdits.WriteAsync(solution, changed, cancellationToken);

            return $"Successfully made '{typeName}' partial";
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error making type partial: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Adds <c>partial</c> as the last modifier, which is where C# requires
    /// it. With no modifiers it takes the leading trivia of the keyword.
    /// </summary>
    internal static TypeDeclarationSyntax WithPartial(TypeDeclarationSyntax declaration)
    {
        var partial = SyntaxFactory.Token(SyntaxKind.PartialKeyword).WithTrailingTrivia(SyntaxFactory.Space);
        if (declaration.Modifiers.Count > 0)
            return declaration.AddModifiers(partial);

        var keyword = declaration.Keyword;
        return declaration
            .WithKeyword(keyword.WithLeadingTrivia())
            .WithModifiers(SyntaxFactory.TokenList(partial.WithLeadingTrivia(keyword.LeadingTrivia)));
    }

    private static string KindOf(MemberDeclarationSyntax declaration) => declaration switch
    {
        EnumDeclarationSyntax => "an enum",
        DelegateDeclarationSyntax => "a delegate",
        _ => "a declaration",
    };
}
