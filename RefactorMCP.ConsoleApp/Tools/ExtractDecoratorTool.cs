using ModelContextProtocol.Server;
using ModelContextProtocol;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;


[McpServerToolType]
public static class ExtractDecoratorTool
{
    [McpServerTool, Description("Create a simple decorator class for a method")]
    public static async Task<string> ExtractDecorator(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Name of the class containing the method")] string className,
        [Description("Name of the method to decorate")] string methodName)
    {
        try
        {
            return await RefactoringHelpers.RunWithSolutionOrFile(
                solutionPath,
                filePath,
                doc => DecorateWithSolution(doc, className, methodName),
                path => DecorateSingleFile(path, className, methodName));
        }
        catch (Exception ex)
        {
            throw new McpException($"Error extracting decorator: {ex.Message}", ex);
        }
    }

    private static async Task<string> DecorateWithSolution(Document document, string className, string methodName)
    {
        var sourceText = await document.GetTextAsync();
        var newText = ExtractDecoratorInSource(sourceText.ToString(), className, methodName);
        var encoding = await RefactoringHelpers.GetFileEncodingAsync(document.FilePath!);
        await File.WriteAllTextAsync(document.FilePath!, newText, encoding);
        RefactoringHelpers.UpdateSolutionCache(document.WithText(SourceText.From(newText, encoding)));
        return $"Created decorator for {className}.{methodName} in {document.FilePath} (solution mode)";
    }

    private static Task<string> DecorateSingleFile(string filePath, string className, string methodName)
    {
        return RefactoringHelpers.ApplySingleFileEdit(
            filePath,
            text => ExtractDecoratorInSource(text, className, methodName),
            $"Created decorator for {className}.{methodName} in {filePath} (single file mode)");
    }

    public static string ExtractDecoratorInSource(string sourceText, string className, string methodName)
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

        var decorator = CreateDecoratorClass(className, method);

        SyntaxNode newRoot;
        if (classNode.Parent is BaseNamespaceDeclarationSyntax ns)
            newRoot = root.ReplaceNode(ns, ns.AddMembers(decorator));
        else
            newRoot = root.AddMembers(decorator);

        var formatted = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }

    private static ClassDeclarationSyntax CreateDecoratorClass(string className, MethodDeclarationSyntax method)
    {
        var decoratorName = className + "Decorator";
        var fieldName = "_inner";

        var field = SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName(className))
                    .AddVariables(SyntaxFactory.VariableDeclarator(fieldName)))
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword), SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));

        var ctor = SyntaxFactory.ConstructorDeclaration(decoratorName)
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

        var decoratedMethod = method.WithBody(SyntaxFactory.Block(callStmt))
            .WithSemicolonToken(default);

        return SyntaxFactory.ClassDeclaration(decoratorName)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .AddMembers(field, ctor, decoratedMethod);
    }
}
