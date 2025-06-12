using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

[McpServerToolType]
public static class ConvertToStaticWithInstanceTool
{
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
        var typeDecl = method.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        if (typeDecl == null)
            return RefactoringHelpers.ThrowMcpException($"Error: Method '{methodName}' is not inside a type");

        var typeSymbol = semanticModel!.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
        if (typeSymbol == null)
            return RefactoringHelpers.ThrowMcpException($"Error: Unable to determine containing type");

        var parameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier(instanceParameterName))
            .WithType(SyntaxFactory.ParseTypeName(typeSymbol.ToDisplayString()));

        var updatedMethod = method.WithParameterList(method.ParameterList.AddParameters(parameter));

        updatedMethod = updatedMethod.ReplaceNodes(
            updatedMethod.DescendantNodes().OfType<ThisExpressionSyntax>(),
            (_, _) => SyntaxFactory.IdentifierName(instanceParameterName));

        updatedMethod = updatedMethod.ReplaceNodes(
            updatedMethod.DescendantNodes().OfType<IdentifierNameSyntax>().Where(id =>
            {
                var sym = semanticModel.GetSymbolInfo(id).Symbol;
                return sym is IFieldSymbol or IPropertySymbol or IMethodSymbol &&
                       SymbolEqualityComparer.Default.Equals(sym.ContainingType, typeSymbol) &&
                       !sym.IsStatic && id.Parent is not MemberAccessExpressionSyntax;
            }),
            (old, _) => SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(instanceParameterName),
                SyntaxFactory.IdentifierName(old.Identifier)));

        var modifiers = updatedMethod.Modifiers;
        if (!modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
            modifiers = modifiers.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));

        updatedMethod = updatedMethod.WithModifiers(modifiers);

        var newRoot = syntaxRoot.ReplaceNode(method, updatedMethod);
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

        var parameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier(instanceParameterName))
            .WithType(SyntaxFactory.ParseTypeName(classDecl.Identifier.ValueText));

        var updatedMethod = method.WithParameterList(method.ParameterList.AddParameters(parameter));

        updatedMethod = updatedMethod.ReplaceNodes(
            updatedMethod.DescendantNodes().OfType<ThisExpressionSyntax>(),
            (_, _) => SyntaxFactory.IdentifierName(instanceParameterName));

        var instanceMembers = classDecl.Members
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

        updatedMethod = updatedMethod.ReplaceNodes(
            updatedMethod.DescendantNodes().OfType<IdentifierNameSyntax>().Where(id =>
                instanceMembers.Contains(id.Identifier.ValueText) && id.Parent is not MemberAccessExpressionSyntax),
            (old, _) => SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(instanceParameterName),
                SyntaxFactory.IdentifierName(old.Identifier)));

        var modifiers = updatedMethod.Modifiers;
        if (!modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
            modifiers = modifiers.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));

        updatedMethod = updatedMethod.WithModifiers(modifiers);

        var newRoot = syntaxRoot.ReplaceNode(method, updatedMethod);
        var formatted = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }
}
