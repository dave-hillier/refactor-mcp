using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using System.Collections.Generic;
using System.Threading;

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

        HashSet<string>? members = null;
        INamedTypeSymbol? typeSymbol = null;
        if (semanticModel != null)
        {
            typeSymbol = (INamedTypeSymbol)semanticModel.GetDeclaredSymbol(classDecl)!;
        }
        else
        {
            members = classDecl.Members
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
        }

        var rewriter = new StaticConversionRewriter(
            new[] { (instanceParameterName, typeName) },
            instanceParameterName,
            members,
            semanticModel,
            typeSymbol);

        var updatedMethod = rewriter.Rewrite(method);
        return root.ReplaceNode(method, updatedMethod);
    }
    [McpServerTool, Description("Transform instance method to static by adding instance parameter (preferred for large C# file refactoring)")]
    public static async Task<string> ConvertToStaticWithInstance(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Name of the method to convert")] string methodName,
        [Description("Name for the instance parameter")] string instanceParameterName = "instance",
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await RefactoringHelpers.RunWithSolutionOrFile(
                solutionPath,
                filePath,
                doc => ConvertToStaticWithInstanceWithSolution(doc, methodName, instanceParameterName),
                path => ConvertToStaticWithInstanceSingleFile(path, methodName, instanceParameterName));
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
            return $"Error: No method named '{methodName}' found";

        var semanticModel = await document.GetSemanticModelAsync();
        var newRoot = ConvertToStaticWithInstanceAst(syntaxRoot!, method, instanceParameterName, semanticModel);
        var formatted = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        var newDocument = document.WithSyntaxRoot(formatted);
        var newText = await newDocument.GetTextAsync();
        var encoding = await RefactoringHelpers.GetFileEncodingAsync(document.FilePath!);
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString(), encoding);
        RefactoringHelpers.UpdateSolutionCache(newDocument);

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
            return $"Error: No method named '{methodName}' found";

        var classDecl = method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (classDecl == null)
            throw new McpException($"Error: Method '{methodName}' is not inside a class");

        var newRoot = ConvertToStaticWithInstanceAst(syntaxRoot, method, instanceParameterName);
        var formatted = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }
}
