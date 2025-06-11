using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Linq;
using System.Collections.Generic;

public static partial class RefactoringTools
{
    [McpServerTool, Description("Convert an instance method to an extension method in a static class")]
    public static async Task<string> ConvertToExtensionMethod(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Name of the instance method to convert")] string methodName,
        [Description("Name of the extension class - optional")] string? extensionClass = null)
    {
        try
        {
            var solution = await GetOrLoadSolution(solutionPath);
            var document = GetDocumentByPath(solution, filePath);
            if (document != null)
                return await ConvertToExtensionMethodWithSolution(document, methodName, extensionClass);

            return await ConvertToExtensionMethodSingleFile(filePath, methodName, extensionClass);
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
            return ThrowMcpException($"Error: No method named '{methodName}' found");

        var semanticModel = await document.GetSemanticModelAsync();
        var classDecl = method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (classDecl == null)
            return ThrowMcpException($"Error: Method '{methodName}' is not inside a class");

        var className = classDecl.Identifier.ValueText;
        var extClassName = extensionClass ?? className + "Extensions";
        var paramName = char.ToLower(className[0]) + className.Substring(1);

        var thisParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier(paramName))
            .WithType(SyntaxFactory.ParseTypeName(className))
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.ThisKeyword));

        var updatedMethod = method.WithParameterList(method.ParameterList.AddParameters(thisParam));

        updatedMethod = updatedMethod.ReplaceNodes(
            updatedMethod.DescendantNodes().OfType<ThisExpressionSyntax>(),
            (_, _) => SyntaxFactory.IdentifierName(paramName));

        updatedMethod = updatedMethod.ReplaceNodes(
            updatedMethod.DescendantNodes().OfType<IdentifierNameSyntax>().Where(id =>
            {
                var sym = semanticModel!.GetSymbolInfo(id).Symbol;
                return sym is IFieldSymbol or IPropertySymbol or IMethodSymbol &&
                       SymbolEqualityComparer.Default.Equals(sym.ContainingType, semanticModel.GetDeclaredSymbol(classDecl)) &&
                       !sym.IsStatic && id.Parent is not MemberAccessExpressionSyntax;
            }),
            (old, _) => SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(paramName),
                SyntaxFactory.IdentifierName(old.Identifier)));

        var modifiers = updatedMethod.Modifiers;
        if (!modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
            modifiers = modifiers.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));

        updatedMethod = updatedMethod.WithModifiers(modifiers);

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

        var formatted = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        var newDocument = document.WithSyntaxRoot(formatted);
        var newText = await newDocument.GetTextAsync();
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString());

        return $"Successfully converted method '{methodName}' to extension method in {document.FilePath} (solution mode)";
    }

    private static async Task<string> ConvertToExtensionMethodSingleFile(string filePath, string methodName, string? extensionClass)
    {
        if (!File.Exists(filePath))
            return ThrowMcpException($"Error: File {filePath} not found (current dir: {Directory.GetCurrentDirectory()})");

        var sourceText = await File.ReadAllTextAsync(filePath);
        var newText = ConvertToExtensionMethodInSource(sourceText, methodName, extensionClass);
        await File.WriteAllTextAsync(filePath, newText);

        return $"Successfully converted method '{methodName}' to extension method in {filePath} (single file mode)";
    }

    public static string ConvertToExtensionMethodInSource(string sourceText, string methodName, string? extensionClass)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = syntaxTree.GetRoot();

        var method = syntaxRoot.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            return ThrowMcpException($"Error: No method named '{methodName}' found");

        var classDecl = method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (classDecl == null)
            return ThrowMcpException($"Error: Method '{methodName}' is not inside a class");

        var className = classDecl.Identifier.ValueText;
        var extClassName = extensionClass ?? className + "Extensions";
        var paramName = char.ToLower(className[0]) + className.Substring(1);

        var thisParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier(paramName))
            .WithType(SyntaxFactory.ParseTypeName(className))
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.ThisKeyword));

        var updatedMethod = method.WithParameterList(method.ParameterList.AddParameters(thisParam));

        updatedMethod = updatedMethod.ReplaceNodes(
            updatedMethod.DescendantNodes().OfType<ThisExpressionSyntax>(),
            (_, _) => SyntaxFactory.IdentifierName(paramName));

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

        updatedMethod = updatedMethod.ReplaceNodes(
            updatedMethod.DescendantNodes().OfType<IdentifierNameSyntax>().Where(id =>
                instanceMembers.Contains(id.Identifier.ValueText) && id.Parent is not MemberAccessExpressionSyntax),
            (old, _) => SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(paramName),
                SyntaxFactory.IdentifierName(old.Identifier)));

        var modifiers = updatedMethod.Modifiers;
        if (!modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
            modifiers = modifiers.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));

        updatedMethod = updatedMethod.WithModifiers(modifiers);

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

        var formatted = Formatter.Format(newRoot, SharedWorkspace);
        return formatted.ToFullString();
    }
}
