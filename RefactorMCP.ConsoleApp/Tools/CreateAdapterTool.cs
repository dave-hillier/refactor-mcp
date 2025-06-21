using ModelContextProtocol.Server;
using ModelContextProtocol;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

[McpServerToolType]
public static class CreateAdapterTool
{
    [McpServerTool, Description("Generate a simple adapter class that delegates to an existing method")]
    public static async Task<string> CreateAdapter(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Name of the class containing the method")] string className,
        [Description("Name of the method to adapt")] string methodName,
        [Description("Name of the adapter class to create")] string adapterName)
    {
        try
        {
            return await RefactoringHelpers.RunWithSolutionOrFile(
                solutionPath,
                filePath,
                doc => AdaptWithSolution(doc, className, methodName, adapterName),
                path => AdaptSingleFile(path, className, methodName, adapterName));
        }
        catch (Exception ex)
        {
            throw new McpException($"Error creating adapter: {ex.Message}", ex);
        }
    }

    private static async Task<string> AdaptWithSolution(Document document, string className, string methodName, string adapterName)
    {
        var text = await document.GetTextAsync();
        var newText = CreateAdapterInSource(text.ToString(), className, methodName, adapterName);
        var enc = await RefactoringHelpers.GetFileEncodingAsync(document.FilePath!);
        await File.WriteAllTextAsync(document.FilePath!, newText, enc);
        RefactoringHelpers.UpdateSolutionCache(document.WithText(SourceText.From(newText, enc)));
        return $"Created adapter {adapterName} in {document.FilePath} (solution mode)";
    }

    private static Task<string> AdaptSingleFile(string filePath, string className, string methodName, string adapterName)
    {
        return RefactoringHelpers.ApplySingleFileEdit(
            filePath,
            text => CreateAdapterInSource(text, className, methodName, adapterName),
            $"Created adapter {adapterName} in {filePath} (single file mode)");
    }

    public static string CreateAdapterInSource(string sourceText, string className, string methodName, string adapterName)
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

        var adapter = CreateAdapterClass(className, adapterName, method);

        SyntaxNode newRoot;
        if (classNode.Parent is BaseNamespaceDeclarationSyntax ns)
            newRoot = root.ReplaceNode(ns, ns.AddMembers(adapter));
        else
            newRoot = root.AddMembers(adapter);

        var formatted = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }

    private static ClassDeclarationSyntax CreateAdapterClass(string className, string adapterName, MethodDeclarationSyntax method)
    {
        var fieldName = "_inner";
        var field = SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName(className))
                    .AddVariables(SyntaxFactory.VariableDeclarator(fieldName)))
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword), SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));

        var ctor = SyntaxFactory.ConstructorDeclaration(adapterName)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .AddParameterListParameters(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("inner"))
                    .WithType(SyntaxFactory.IdentifierName(className)))
            .WithBody(
                SyntaxFactory.Block(
                    SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            SyntaxFactory.IdentifierName(fieldName),
                            SyntaxFactory.IdentifierName("inner")))));

        var args = method.ParameterList.Parameters
            .Select(p => SyntaxFactory.Argument(SyntaxFactory.IdentifierName(p.Identifier)));
        var call = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(fieldName),
                    SyntaxFactory.IdentifierName(method.Identifier)))
            .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(args)));

        StatementSyntax callStmt;
        var isVoid = method.ReturnType is PredefinedTypeSyntax pts && pts.Keyword.IsKind(SyntaxKind.VoidKeyword);
        if (isVoid)
            callStmt = SyntaxFactory.ExpressionStatement(call);
        else
            callStmt = SyntaxFactory.ReturnStatement(call);

        var adaptedMethod = method.WithIdentifier(SyntaxFactory.Identifier("Adapt"))
            .WithBody(SyntaxFactory.Block(callStmt))
            .WithSemicolonToken(default);

        return SyntaxFactory.ClassDeclaration(adapterName)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .AddMembers(field, ctor, adaptedMethod);
    }
}
