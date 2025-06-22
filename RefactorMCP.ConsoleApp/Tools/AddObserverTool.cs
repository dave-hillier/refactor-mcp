using ModelContextProtocol.Server;
using ModelContextProtocol;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

[McpServerToolType]
public static class AddObserverTool
{
    [McpServerTool, Description("Introduce a simple observer event and raise it in a method")]
    public static async Task<string> AddObserver(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Name of the class containing the method")] string className,
        [Description("Name of the method to raise the event from")] string methodName,
        [Description("Name of the event to create")] string eventName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await RefactoringHelpers.RunWithSolutionOrFile(
                solutionPath,
                filePath,
                doc => AddObserverWithSolution(doc, className, methodName, eventName),
                path => AddObserverSingleFile(path, className, methodName, eventName));
        }
        catch (Exception ex)
        {
            throw new McpException($"Error adding observer: {ex.Message}", ex);
        }
    }

    private static async Task<string> AddObserverWithSolution(Document document, string className, string methodName, string eventName)
    {
        var text = await document.GetTextAsync();
        var newText = AddObserverInSource(text.ToString(), className, methodName, eventName);
        var enc = await RefactoringHelpers.GetFileEncodingAsync(document.FilePath!);
        await File.WriteAllTextAsync(document.FilePath!, newText, enc);
        RefactoringHelpers.UpdateSolutionCache(document.WithText(SourceText.From(newText, enc)));
        return $"Added observer {eventName} to {document.FilePath} (solution mode)";
    }

    private static Task<string> AddObserverSingleFile(string filePath, string className, string methodName, string eventName)
    {
        return RefactoringHelpers.ApplySingleFileEdit(
            filePath,
            text => AddObserverInSource(text, className, methodName, eventName),
            $"Added observer {eventName} to {filePath} (single file mode)");
    }

    public static string AddObserverInSource(string sourceText, string className, string methodName, string eventName)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = (CompilationUnitSyntax)tree.GetRoot();
        var classNode = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == className);
        if (classNode == null)
            throw new McpException($"Error: Class '{className}' not found");
        var method = classNode.Members.OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            throw new McpException($"Error: Method '{methodName}' not found");

        var param = method.ParameterList.Parameters.FirstOrDefault();
        var eventType = param != null ? $"Action<{param.Type}>" : "Action";
        var eventField = SyntaxFactory.EventFieldDeclaration(
                SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName(eventType))
                    .AddVariables(SyntaxFactory.VariableDeclarator(eventName)))
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));

        var invocationArgs = param != null ? param.Identifier.ValueText : string.Empty;
        var invokeStmt = SyntaxFactory.ParseStatement($"{eventName}?.Invoke({invocationArgs});");

        var newMethod = method.WithBody(method.Body!.AddStatements(invokeStmt));
        var newClass = classNode.ReplaceNode(method, newMethod).AddMembers(eventField);

        var newRoot = root.ReplaceNode(classNode, newClass);
        var formatted = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }
}
