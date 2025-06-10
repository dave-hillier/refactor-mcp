using ModelContextProtocol.Server;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

public static partial class RefactoringTools
{
    [McpServerTool, Description("Transform instance method to static by adding instance parameter (preferred for large-file refactoring)")]
    public static async Task<string> ConvertToStaticWithInstance(
        [Description("Path to the C# file")] string filePath,
        [Description("Name of the method to convert")] string methodName,
        [Description("Name for the instance parameter")] string instanceParameterName = "instance",
        [Description("Path to the solution file (.sln) - optional for single file mode")] string? solutionPath = null)
    {
        try
        {
            if (solutionPath != null)
            {
                var solution = await GetOrLoadSolution(solutionPath);
                var document = GetDocumentByPath(solution, filePath);
                if (document != null)
                    return await ConvertToStaticWithInstanceWithSolution(document, methodName, instanceParameterName);

                // Fallback to single file mode when file isn't part of the solution
                return await ConvertToStaticWithInstanceSingleFile(filePath, methodName, instanceParameterName);
            }

            return await ConvertToStaticWithInstanceSingleFile(filePath, methodName, instanceParameterName);
        }
        catch (Exception ex)
        {
            return $"Error converting method to static: {ex.Message}";
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
        var typeDecl = method.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        if (typeDecl == null)
            return $"Error: Method '{methodName}' is not inside a type";

        var typeSymbol = semanticModel!.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
        if (typeSymbol == null)
            return $"Error: Unable to determine containing type";

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

    private static async Task<string> ConvertToStaticWithInstanceSingleFile(string filePath, string methodName, string instanceParameterName)
    {
        if (!File.Exists(filePath))
            return $"Error: File {filePath} not found";

        var sourceText = await File.ReadAllTextAsync(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = await syntaxTree.GetRootAsync();

        var method = syntaxRoot.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            return $"Error: No method named '{methodName}' found";

        var classDecl = method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (classDecl == null)
            return $"Error: Method '{methodName}' is not inside a class";

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
        var formatted = Formatter.Format(newRoot, SharedWorkspace);
        await File.WriteAllTextAsync(filePath, formatted.ToFullString());

        return $"Successfully converted method '{methodName}' to static with instance parameter in {filePath} (single file mode)";
    }
}
