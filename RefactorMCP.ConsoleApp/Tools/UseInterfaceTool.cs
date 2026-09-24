using ModelContextProtocol.Server;
using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.ComponentModel;

[McpServerToolType]
public static class UseInterfaceTool
{
    private static async Task<string> UseInterfaceWithSolution(Document document, string methodName, string parameterName, string interfaceName)
    {
        var root = await document.GetSyntaxRootAsync();
        var method = root!.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            return $"Error: No method named '{methodName}' found";
        var param = method.ParameterList.Parameters
            .FirstOrDefault(p => p.Identifier.ValueText == parameterName);
        if (param == null)
            return $"Error: No parameter named '{parameterName}' found";

        var newParam = param.WithType(SyntaxFactory.ParseTypeName(interfaceName));
        var newMethod = method.ReplaceNode(param, newParam);
        var newRoot = root.ReplaceNode(method, newMethod);
        var formatted = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        var newDoc = document.WithSyntaxRoot(formatted);
        var text = await newDoc.GetTextAsync();
        var encoding = await RefactoringHelpers.GetFileEncodingAsync(document.FilePath!);
        await File.WriteAllTextAsync(document.FilePath!, text.ToString(), encoding);
        RefactoringHelpers.UpdateSolutionCache(newDoc);
        return $"Successfully changed parameter '{parameterName}' to interface '{interfaceName}' in method '{methodName}' in {document.FilePath} (solution mode)";
    }

    [McpServerTool, Description("Change a method parameter type to an interface (preferred for large C# file refactoring)")]
    public static async Task<string> UseInterface(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Name of the method containing the parameter")] string methodName,
        [Description("Name of the parameter to change")] string parameterName,
        [Description("Interface type name to use")] string interfaceName)
    {
        try
        {
            return await RefactoringHelpers.RunWithSolution(
                solutionPath,
                filePath,
                doc => UseInterfaceWithSolution(doc, methodName, parameterName, interfaceName));
        }
        catch (Exception ex)
        {
            throw new McpException($"Error applying interface: {ex.Message}", ex);
        }
    }
}
