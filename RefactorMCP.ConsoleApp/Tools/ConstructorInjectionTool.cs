using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

[McpServerToolType]
public static class ConstructorInjectionTool
{
    public readonly record struct MethodParameterPair(string MethodName, string ParameterName);

    [McpServerTool, Description("Convert method parameters to constructor injection (preferred for large C# file refactoring)")]
    public static async Task<string> ConvertToConstructorInjection(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Method and parameter pairs in the format Method:Parameter;...")] MethodParameterPair[] methodParameters,
        [Description("Use a public property instead of a private field")] bool useProperty = false)
    {
        try
        {
            return await RefactoringHelpers.RunWithSolutionOrFile(
                solutionPath,
                filePath,
                doc => ConvertWithSolution(doc, methodParameters, useProperty),
                path => ConvertSingleFile(path, methodParameters, useProperty));
        }
        catch (Exception ex)
        {
            throw new McpException($"Error performing constructor injection: {ex.Message}", ex);
        }
    }


    private static async Task<string> ConvertWithSolution(Document document, MethodParameterPair[] methodParameters, bool useProperty)
    {
        var sourceText = (await document.GetTextAsync()).ToString();
        var newText = ConvertInSource(sourceText, methodParameters, useProperty);
        if (newText.StartsWith("Error:"))
            return newText;

        var encoding = await RefactoringHelpers.GetFileEncodingAsync(document.FilePath!);
        await File.WriteAllTextAsync(document.FilePath!, newText, encoding);
        var newDoc = document.WithText(SourceText.From(newText, encoding));
        RefactoringHelpers.UpdateSolutionCache(newDoc);
        return $"Successfully injected parameters via constructor in {document.FilePath} (solution mode)";
    }

    private static async Task<string> ConvertSingleFile(string filePath, MethodParameterPair[] methodParameters, bool useProperty)
    {
        if (!File.Exists(filePath))
            throw new McpException($"Error: File {filePath} not found");
        var (sourceText, encoding) = await RefactoringHelpers.ReadFileWithEncodingAsync(filePath);
        var newText = ConvertInSource(sourceText, methodParameters, useProperty);
        if (newText.StartsWith("Error:"))
            return newText;
        await File.WriteAllTextAsync(filePath, newText, encoding);
        RefactoringHelpers.UpdateFileCaches(filePath, newText);
        return $"Successfully injected parameters via constructor in {filePath} (single file mode)";
    }

    public static string ConvertInSource(string sourceText, MethodParameterPair[] methodParameters, bool useProperty)
    {
        var text = sourceText;
        foreach (var pair in methodParameters)
        {
            text = ConvertInSource(text, pair.MethodName, pair.ParameterName, useProperty);
            if (text.StartsWith("Error:"))
                return text;
        }
        return text;
    }

    public static string ConvertInSource(string sourceText, string methodName, string parameterName, bool useProperty, SemanticModel? model = null)
    {
        var tree = model?.SyntaxTree ?? CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            return $"Error: Method '{methodName}' not found";
        var parameter = method.ParameterList.Parameters.FirstOrDefault(p => p.Identifier.ValueText == parameterName);
        if (parameter == null)
            return $"Error: Parameter '{parameterName}' not found";
        var index = method.ParameterList.Parameters.IndexOf(parameter);
        var type = parameter.Type ?? SyntaxFactory.ParseTypeName("object");
        var fieldName = useProperty ? char.ToUpper(parameterName[0]) + parameterName.Substring(1) : "_" + parameterName;
        var rewriter = new ConstructorInjectionRewriter(methodName, parameterName, index, type, fieldName, useProperty);
        var newRoot = rewriter.Visit(root)!;
        var formatted = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }
}

