using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using System.Collections.Generic;

[McpServerToolType]
public static class ConvertToStaticWithParametersTool
{
    private static SyntaxNode ConvertToStaticWithParametersAst(
        SyntaxNode root,
        MethodDeclarationSyntax method,
        SemanticModel? semanticModel = null)
    {
        var classDecl = method.Ancestors().OfType<ClassDeclarationSyntax>().First();

        Dictionary<string, string> instanceMembers;
        Dictionary<ISymbol, string>? semanticMap = null;

        if (semanticModel != null)
        {
            var typeSymbol = (INamedTypeSymbol)semanticModel.GetDeclaredSymbol(classDecl)!;
            semanticMap = new(SymbolEqualityComparer.Default);
            instanceMembers = new();
            foreach (var id in method.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                var symbol = semanticModel.GetSymbolInfo(id).Symbol;
                if (symbol is IFieldSymbol or IPropertySymbol &&
                    SymbolEqualityComparer.Default.Equals(symbol.ContainingType, typeSymbol) &&
                    !symbol.IsStatic)
                {
                    if (!semanticMap.ContainsKey(symbol))
                    {
                        var name = symbol.Name;
                        if (method.ParameterList.Parameters.Any(p => p.Identifier.ValueText == name))
                            name += "Param";
                        semanticMap[symbol] = name;
                        var typeName = symbol switch
                        {
                            IFieldSymbol f => f.Type.ToDisplayString(),
                            IPropertySymbol p => p.Type.ToDisplayString(),
                            _ => "object"
                        };
                        instanceMembers[name] = typeName;
                    }
                }
            }
        }
        else
        {
            instanceMembers = new();
            foreach (var field in classDecl.Members.OfType<FieldDeclarationSyntax>())
            {
                if (field.Modifiers.Any(SyntaxKind.StaticKeyword)) continue;
                var typeName = field.Declaration.Type.ToString();
                foreach (var variable in field.Declaration.Variables)
                    instanceMembers[variable.Identifier.ValueText] = typeName;
            }

            foreach (var prop in classDecl.Members.OfType<PropertyDeclarationSyntax>())
            {
                if (prop.Modifiers.Any(SyntaxKind.StaticKeyword)) continue;
                instanceMembers[prop.Identifier.ValueText] = prop.Type.ToString();
            }
        }

        var parameters = new List<(string Name, string Type)>();
        Dictionary<string, string>? renameMap = null;
        Dictionary<ISymbol, string>? symbolMap = null;

        if (semanticMap != null)
        {
            symbolMap = semanticMap;
            foreach (var kvp in semanticMap)
            {
                var typeName = instanceMembers[kvp.Value];
                parameters.Add((kvp.Value, typeName));
            }
        }
        else
        {
            renameMap = new();
            var usedMembers = method.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Where(id => instanceMembers.ContainsKey(id.Identifier.ValueText))
                .Select(id => id.Identifier.ValueText)
                .Distinct()
                .ToList();

            foreach (var name in usedMembers)
            {
                var paramName = name;
                if (method.ParameterList.Parameters.Any(p => p.Identifier.ValueText == paramName))
                    paramName += "Param";
                renameMap[name] = paramName;
                parameters.Add((paramName, instanceMembers[name]));
            }
        }

        var rewriter = new StaticConversionRewriter(
            parameters,
            instanceParameterName: null,
            knownInstanceMembers: null,
            semanticModel: semanticModel,
            typeSymbol: null,
            symbolRenameMap: symbolMap,
            nameRenameMap: renameMap);

        var updatedMethod = rewriter.Rewrite(method);
        return root.ReplaceNode(method, updatedMethod);
    }
    [McpServerTool, Description("Transform instance method to static by converting dependencies to parameters (preferred for large C# file refactoring)")]
    public static async Task<string> ConvertToStaticWithParameters(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Name of the method to convert")] string methodName)
    {
        try
        {
            return await RefactoringHelpers.RunWithSolutionOrFile(
                solutionPath,
                filePath,
                doc => ConvertToStaticWithParametersWithSolution(doc, methodName),
                path => ConvertToStaticWithParametersSingleFile(path, methodName));
        }
        catch (Exception ex)
        {
            throw new McpException($"Error converting method to static: {ex.Message}", ex);
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
            return RefactoringHelpers.ThrowMcpException($"Error: No method named '{methodName}' found");

        var semanticModel = await document.GetSemanticModelAsync();
        var newRoot = ConvertToStaticWithParametersAst(syntaxRoot!, method, semanticModel);
        var formattedRoot = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        var newDocument = document.WithSyntaxRoot(formattedRoot);
        var newText = await newDocument.GetTextAsync();
        var encoding = await RefactoringHelpers.GetFileEncodingAsync(document.FilePath!);
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString(), encoding);
        RefactoringHelpers.UpdateSolutionCache(newDocument);

        return $"Successfully converted method '{methodName}' to static with parameters in {document.FilePath} (solution mode)";
    }

    private static Task<string> ConvertToStaticWithParametersSingleFile(string filePath, string methodName)
    {
        return RefactoringHelpers.ApplySingleFileEdit(
            filePath,
            text => ConvertToStaticWithParametersInSource(text, methodName),
            $"Successfully converted method '{methodName}' to static with parameters in {filePath} (single file mode)");
    }

    public static string ConvertToStaticWithParametersInSource(string sourceText, string methodName)
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

        var newRoot = ConvertToStaticWithParametersAst(syntaxRoot, method);
        var formattedRoot = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
        return formattedRoot.ToFullString();
    }
}
