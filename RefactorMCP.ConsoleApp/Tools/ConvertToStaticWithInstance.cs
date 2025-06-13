using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using System.Linq;

[McpServerToolType]
public static class ConvertToStaticWithInstanceTool
{
    private static SyntaxNode ConvertToStaticWithInstanceAst(
        SyntaxNode root,
        MethodDeclarationSyntax method,
        string instanceParameterName,
        SemanticModel? semanticModel = null)
    {
        var classDecl = method.Ancestors().OfType<ClassDeclarationSyntax>().First();

        var typeName = semanticModel != null
            ? ((INamedTypeSymbol)semanticModel.GetDeclaredSymbol(classDecl)!).ToDisplayString()
            : classDecl.Identifier.ValueText;

        var updatedMethod = AstTransformations.AddParameter(method, instanceParameterName, typeName);
        updatedMethod = AstTransformations.ReplaceThisReferences(updatedMethod, instanceParameterName);

        if (semanticModel != null)
        {
            var typeSymbol = (INamedTypeSymbol)semanticModel.GetDeclaredSymbol(classDecl)!;
            updatedMethod = AstTransformations.QualifyInstanceMembers(
                updatedMethod,
                instanceParameterName,
                semanticModel,
                typeSymbol);
        }
        else
        {
            var members = classDecl.Members
                .Where(m => m is FieldDeclarationSyntax or PropertyDeclarationSyntax or MethodDeclarationSyntax)
                .Select(m => m switch
                {
                    FieldDeclarationSyntax f => f.Declaration.Variables.First().Identifier.ValueText,
                    PropertyDeclarationSyntax p => p.Identifier.ValueText,
                    MethodDeclarationSyntax md => md.Identifier.ValueText,
                    _ => string.Empty
                })
                .Where(n => !string.IsNullOrEmpty(n))
                .ToHashSet();

            updatedMethod = AstTransformations.QualifyInstanceMembers(updatedMethod, instanceParameterName, members);
        }

        updatedMethod = AstTransformations.EnsureStaticModifier(updatedMethod);
        return root.ReplaceNode(method, updatedMethod);
    }
    [McpServerTool, Description("Transform instance method to static by adding instance parameter (preferred for large C# file refactoring)")]
    public static async Task<string> ConvertToStaticWithInstance(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Name of the method to convert")] string methodName,
        [Description("Name for the instance parameter")] string instanceParameterName = "instance")
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath);
            var document = RefactoringHelpers.GetDocumentByPath(solution, filePath);
            if (document != null)
                return await ConvertToStaticWithInstanceWithSolution(document, methodName, instanceParameterName);

            return await ConvertToStaticWithInstanceSingleFile(filePath, methodName, instanceParameterName);
        }
        catch (Exception ex)
        {
            throw new McpException($"Error converting method to static: {ex.Message}", ex);
        }
    }


    private static async Task<string> ConvertToStaticWithInstanceWithSolution(Document document, string methodName, string instanceParameterName)
    {
        var sourceText = await document.GetTextAsync();
        var syntaxRoot = await document.GetSyntaxRootAsync();

        var method = syntaxRoot!.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            return RefactoringHelpers.ThrowMcpException($"Error: No method named '{methodName}' found");

        var semanticModel = await document.GetSemanticModelAsync();
        var newRoot = ConvertToStaticWithInstanceAst(syntaxRoot!, method, instanceParameterName, semanticModel);
        var formatted = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        var newDocument = document.WithSyntaxRoot(formatted);
        var newText = await newDocument.GetTextAsync();
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString());

        return $"Successfully converted method '{methodName}' to static with instance parameter in {document.FilePath} (solution mode)";
    }

    private static Task<string> ConvertToStaticWithInstanceSingleFile(string filePath, string methodName, string instanceParameterName)
    {
        return RefactoringHelpers.ApplySingleFileEdit(
            filePath,
            text => ConvertToStaticWithInstanceInSource(text, methodName, instanceParameterName),
            $"Successfully converted method '{methodName}' to static with instance parameter in {filePath} (single file mode)");
    }

    public static string ConvertToStaticWithInstanceInSource(string sourceText, string methodName, string instanceParameterName)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = syntaxTree.GetRoot();

        var method = syntaxRoot.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            return RefactoringHelpers.ThrowMcpException($"Error: No method named '{methodName}' found");

        var classDecl = method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (classDecl == null)
            return RefactoringHelpers.ThrowMcpException($"Error: Method '{methodName}' is not inside a class");

        var newRoot = ConvertToStaticWithInstanceAst(syntaxRoot, method, instanceParameterName);
        var formatted = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }
}
