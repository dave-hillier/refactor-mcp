using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Linq;
using System.Collections.Generic;
using System.Threading;

[McpServerToolType]
public static class ConvertToExtensionMethodTool
{
    [McpServerTool, Description("Convert an instance method to an extension method in a static class. " +
        "A wrapper method remains so existing call sites continue to work." +
        "The extension class will be automatically created if it doesn't exist.")]
    public static async Task<string> ConvertToExtensionMethod(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Name of the instance method to convert")] string methodName,
        [Description("Name of the extension class - optional, class will be automatically created if it doesn't exist or us unspecified")] string? extensionClass = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await RefactoringHelpers.RunWithSolutionOrFile(
                solutionPath,
                filePath,
                doc => ConvertToExtensionMethodWithSolution(doc, methodName, extensionClass),
                path => ConvertToExtensionMethodSingleFile(path, methodName, extensionClass));
        }
        catch (Exception ex)
        {
            throw new McpException($"Error converting to extension method: {ex.Message}", ex);
        }
    }

    private static async Task<string> ConvertToExtensionMethodWithSolution(Document document, string methodName, string? extensionClass)
    {
        var sourceText = await document.GetTextAsync();
        var syntaxRoot = await document.GetSyntaxRootAsync();

        var method = syntaxRoot!.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            return $"Error: No method named '{methodName}' found";

        var semanticModel = await document.GetSemanticModelAsync();
        var classDecl = method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (classDecl == null)
            throw new McpException($"Error: Method '{methodName}' is not inside a class");

        var className = classDecl.Identifier.ValueText;
        var extClassName = extensionClass ?? className + "Extensions";
        var paramName = char.ToLower(className[0]) + className.Substring(1);

        var typeSymbol = (INamedTypeSymbol)semanticModel!.GetDeclaredSymbol(classDecl)!;
        var rewriter = new ExtensionMethodRewriter(paramName, className, semanticModel!, typeSymbol);
        var updatedMethod = rewriter.Rewrite(method);

        // Replace the original method with a wrapper that calls the new extension
        var wrapperArgs = new List<ArgumentSyntax> { SyntaxFactory.Argument(SyntaxFactory.ThisExpression()) };
        wrapperArgs.AddRange(method.ParameterList.Parameters.Select(p =>
            SyntaxFactory.Argument(SyntaxFactory.IdentifierName(p.Identifier))));

        var extensionInvocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(extClassName),
                SyntaxFactory.IdentifierName(method.Identifier)))
            .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(wrapperArgs)));

        StatementSyntax callStatement = method.ReturnType is PredefinedTypeSyntax pts &&
                                         pts.Keyword.IsKind(SyntaxKind.VoidKeyword)
            ? SyntaxFactory.ExpressionStatement(extensionInvocation)
            : SyntaxFactory.ReturnStatement(extensionInvocation);

        var wrapperMethod = method.WithBody(SyntaxFactory.Block(callStatement))
            .WithExpressionBody(null)
            .WithSemicolonToken(default);

        var newRoot = syntaxRoot.ReplaceNode(method, wrapperMethod);

        var extClass = newRoot.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == extClassName);
        if (extClass != null)
        {
            var updatedClass = extClass.AddMembers(updatedMethod);
            newRoot = newRoot.ReplaceNode(extClass, updatedClass);
        }
        else
        {
            var duplicateDoc = await RefactoringHelpers.FindClassInSolution(document.Project.Solution, extClassName, document.FilePath!);
            if (duplicateDoc != null)
                throw new McpException($"Error: Class {extClassName} already exists in {duplicateDoc.FilePath}");
            var extensionClassDecl = SyntaxFactory.ClassDeclaration(extClassName)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword))
                .AddMembers(updatedMethod);

            if (classDecl.Parent is NamespaceDeclarationSyntax ns)
            {
                var updatedNs = ns.AddMembers(extensionClassDecl);
                newRoot = newRoot.ReplaceNode(ns, updatedNs);
            }
            else
            {
                newRoot = ((CompilationUnitSyntax)newRoot).AddMembers(extensionClassDecl);
            }
        }

        var formatted = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        var newDocument = document.WithSyntaxRoot(formatted);
        var newText = await newDocument.GetTextAsync();
        var encoding = await RefactoringHelpers.GetFileEncodingAsync(document.FilePath!);
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString(), encoding);
        RefactoringHelpers.UpdateSolutionCache(newDocument);

        return $"Successfully converted method '{methodName}' to extension method in {document.FilePath} (solution mode)";
    }

    private static Task<string> ConvertToExtensionMethodSingleFile(string filePath, string methodName, string? extensionClass)
    {
        return RefactoringHelpers.ApplySingleFileEdit(
            filePath,
            text => ConvertToExtensionMethodInSource(text, methodName, extensionClass),
            $"Successfully converted method '{methodName}' to extension method in {filePath} (single file mode)");
    }

    public static string ConvertToExtensionMethodInSource(string sourceText, string methodName, string? extensionClass)
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

        var className = classDecl.Identifier.ValueText;
        var extClassName = extensionClass ?? className + "Extensions";
        var paramName = char.ToLower(className[0]) + className.Substring(1);

        var instanceMembers = classDecl.Members
            .Where(m => m is FieldDeclarationSyntax or PropertyDeclarationSyntax)
            .Select(m => m switch
            {
                FieldDeclarationSyntax f => f.Declaration.Variables.First().Identifier.ValueText,
                PropertyDeclarationSyntax p => p.Identifier.ValueText,
                _ => string.Empty
            })
            .Where(n => !string.IsNullOrEmpty(n))
            .ToHashSet();

        var rewriter = new ExtensionMethodRewriter(paramName, className, instanceMembers);
        var updatedMethod = rewriter.Rewrite(method);

        // Replace the original method with a wrapper that calls the new extension
        var wrapperArgs = new List<ArgumentSyntax> { SyntaxFactory.Argument(SyntaxFactory.ThisExpression()) };
        wrapperArgs.AddRange(method.ParameterList.Parameters.Select(p =>
            SyntaxFactory.Argument(SyntaxFactory.IdentifierName(p.Identifier))));

        var extensionInvocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(extClassName),
                SyntaxFactory.IdentifierName(method.Identifier)))
            .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(wrapperArgs)));

        StatementSyntax callStatement = method.ReturnType is PredefinedTypeSyntax pts &&
                                         pts.Keyword.IsKind(SyntaxKind.VoidKeyword)
            ? SyntaxFactory.ExpressionStatement(extensionInvocation)
            : SyntaxFactory.ReturnStatement(extensionInvocation);

        var wrapperMethod = method.WithBody(SyntaxFactory.Block(callStatement))
            .WithExpressionBody(null)
            .WithSemicolonToken(default);

        var newRoot = syntaxRoot.ReplaceNode(method, wrapperMethod);

        var extClass = newRoot.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == extClassName);
        if (extClass != null)
        {
            var updatedClass = extClass.AddMembers(updatedMethod);
            newRoot = newRoot.ReplaceNode(extClass, updatedClass);
        }
        else
        {
            var extensionClassDecl = SyntaxFactory.ClassDeclaration(extClassName)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword))
                .AddMembers(updatedMethod);

            if (classDecl.Parent is NamespaceDeclarationSyntax ns)
            {
                var updatedNs = ns.AddMembers(extensionClassDecl);
                newRoot = newRoot.ReplaceNode(ns, updatedNs);
            }
            else
            {
                newRoot = ((CompilationUnitSyntax)newRoot).AddMembers(extensionClassDecl);
            }
        }

        var formatted = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }
}
