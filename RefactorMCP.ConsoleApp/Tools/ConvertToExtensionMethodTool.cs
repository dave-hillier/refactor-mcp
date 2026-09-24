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
using RefactorMCP.ConsoleApp.Tools.Moving;

[McpServerToolType]
public static class ConvertToExtensionMethodTool
{
    [McpServerTool, Description("Convert a method to an extension method. A static method of a static class " +
        "gains 'this' on its first parameter and static calls take the extension form where they can. " +
        "An instance method moves to a static extension class, and a wrapper method remains so existing call sites continue to work. " +
        "The extension class will be automatically created if it doesn't exist.")]
    public static async Task<string> ConvertToExtensionMethod(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Name of the method to convert")] string methodName,
        [Description("Name of the extension class - optional, class will be automatically created if it doesn't exist or us unspecified")] string? extensionClass = null,
        [Description("Line of the method's declaration (1-based, optional), to choose between overloads")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // A static method of a static class becomes an extension in place.
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var document = RefactoringHelpers.GetDocumentByPath(solution, filePath);
            if (document != null)
            {
                var method = (IMethodSymbol)await MovingSupport.FindDeclaredSymbolAsync(
                    document, methodName, line, s => s is IMethodSymbol { MethodKind: MethodKind.Ordinary }, "method", cancellationToken);
                if (method.IsStatic)
                    return await ExtensionMethodConversions.ToExtensionAsync(solution, method, cancellationToken);
            }

            return await RefactoringHelpers.RunWithSolution(
                solutionPath,
                filePath,
                doc => ConvertToExtensionMethodWithSolution(doc, methodName, extensionClass));
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

            // The namespace starts before the replaced method, so its position
            // finds it in the updated tree.
            if (classDecl.Parent is BaseNamespaceDeclarationSyntax oldNs)
            {
                var ns = newRoot.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>()
                    .First(n => n.SpanStart == oldNs.SpanStart);
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

}
