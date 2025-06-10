using ModelContextProtocol.Server;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

public static partial class RefactoringTools
{
    [McpServerTool, Description("Transform instance method to static by converting dependencies to parameters (preferred for large-file refactoring)")]
    public static async Task<string> ConvertToStaticWithParameters(
        [Description("Path to the C# file")] string filePath,
        [Description("Name of the method to convert")] string methodName,
        [Description("Path to the solution file (.sln) - optional for single file mode")] string? solutionPath = null)
    {
        try
        {
            if (solutionPath != null)
            {
                var solution = await GetOrLoadSolution(solutionPath);
                var document = GetDocumentByPath(solution, filePath);
                if (document == null)
                    return $"Error: File {filePath} not found in solution (current dir: {Directory.GetCurrentDirectory()})";

                return await ConvertToStaticWithParametersWithSolution(document, methodName);
            }
            else
            {
                return await ConvertToStaticWithParametersSingleFile(filePath, methodName);
            }
        }
        catch (Exception ex)
        {
            return $"Error converting method to static: {ex.Message}";
        }
    }


    private static async Task<string> ConvertToStaticWithParametersWithSolution(Document document, string methodName)
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

        var parameterList = method.ParameterList;
        var paramMap = new Dictionary<ISymbol, string>(SymbolEqualityComparer.Default);

        foreach (var id in method.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            var symbol = semanticModel.GetSymbolInfo(id).Symbol;
            if (symbol is IFieldSymbol or IPropertySymbol &&
                SymbolEqualityComparer.Default.Equals(symbol.ContainingType, typeSymbol) &&
                !symbol.IsStatic)
            {
                if (!paramMap.ContainsKey(symbol))
                {
                    var name = symbol.Name;
                    if (parameterList.Parameters.Any(p => p.Identifier.ValueText == name))
                        name += "Param";
                    paramMap[symbol] = name;

                    var typeName = symbol switch
                    {
                        IFieldSymbol f => f.Type.ToDisplayString(),
                        IPropertySymbol p => p.Type.ToDisplayString(),
                        _ => "object"
                    };

                    var param = SyntaxFactory.Parameter(SyntaxFactory.Identifier(name))
                        .WithType(SyntaxFactory.ParseTypeName(typeName));
                    parameterList = parameterList.AddParameters(param);
                }
            }
        }

        var updatedMethod = method.ReplaceNodes(
            method.DescendantNodes().OfType<IdentifierNameSyntax>().Where(id =>
            {
                var sym = semanticModel.GetSymbolInfo(id).Symbol;
                return sym != null && paramMap.ContainsKey(sym);
            }),
            (old, _) =>
            {
                var sym = semanticModel.GetSymbolInfo(old).Symbol!;
                return SyntaxFactory.IdentifierName(paramMap[sym]);
            });

        var modifiers = updatedMethod.Modifiers;
        if (!modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
            modifiers = modifiers.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));

        updatedMethod = updatedMethod.WithModifiers(modifiers)
            .WithParameterList(parameterList);

        var newRoot = syntaxRoot.ReplaceNode(method, updatedMethod);
        var formattedRoot = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        var newDocument = document.WithSyntaxRoot(formattedRoot);
        var newText = await newDocument.GetTextAsync();
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString());

        return $"Successfully converted method '{methodName}' to static with parameters in {document.FilePath} (solution mode)";
    }

    private static async Task<string> ConvertToStaticWithParametersSingleFile(string filePath, string methodName)
    {
        if (!File.Exists(filePath))
            return $"Error: File {filePath} not found (current dir: {Directory.GetCurrentDirectory()})";

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

        var instanceMembers = new Dictionary<string, string>();
        foreach (var field in classDecl.Members.OfType<FieldDeclarationSyntax>())
        {
            if (field.Modifiers.Any(SyntaxKind.StaticKeyword)) continue;
            var typeName = field.Declaration.Type.ToString();
            foreach (var variable in field.Declaration.Variables)
            {
                instanceMembers[variable.Identifier.ValueText] = typeName;
            }
        }

        foreach (var prop in classDecl.Members.OfType<PropertyDeclarationSyntax>())
        {
            if (prop.Modifiers.Any(SyntaxKind.StaticKeyword)) continue;
            instanceMembers[prop.Identifier.ValueText] = prop.Type.ToString();
        }

        var usedMembers = method.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(id => instanceMembers.ContainsKey(id.Identifier.ValueText))
            .Select(id => id.Identifier.ValueText)
            .Distinct()
            .ToList();

        var parameterList = method.ParameterList;
        var renameMap = new Dictionary<string, string>();
        foreach (var name in usedMembers)
        {
            var paramName = name;
            if (parameterList.Parameters.Any(p => p.Identifier.ValueText == paramName))
                paramName += "Param";
            renameMap[name] = paramName;
            var param = SyntaxFactory.Parameter(SyntaxFactory.Identifier(paramName))
                .WithType(SyntaxFactory.ParseTypeName(instanceMembers[name]));
            parameterList = parameterList.AddParameters(param);
        }

        var updatedMethod = method.ReplaceNodes(
            method.DescendantNodes().OfType<IdentifierNameSyntax>()
                .Where(id => renameMap.ContainsKey(id.Identifier.ValueText)),
            (old, _) => SyntaxFactory.IdentifierName(renameMap[old.Identifier.ValueText]));

        var modifiers = updatedMethod.Modifiers;
        if (!modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
            modifiers = modifiers.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));

        updatedMethod = updatedMethod.WithModifiers(modifiers)
            .WithParameterList(parameterList);

        var newRoot = syntaxRoot.ReplaceNode(method, updatedMethod);
        var formattedRoot = Formatter.Format(newRoot, SharedWorkspace);
        await File.WriteAllTextAsync(filePath, formattedRoot.ToFullString());

        return $"Successfully converted method '{methodName}' to static with parameters in {filePath} (single file mode)";
    }
}
