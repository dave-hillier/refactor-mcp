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
using System.Threading;

[McpServerToolType]
public static class ConvertToStaticWithParametersTool
{
    private static SyntaxNode ConvertToStaticWithParametersAst(
        SyntaxNode root,
        MethodDeclarationSyntax method,
        SemanticModel? semanticModel = null)
    {
        var classDecl = method.Ancestors().OfType<ClassDeclarationSyntax>().First();

        var parameters = new List<(string Name, string Type)>();
        Dictionary<string, string>? renameMap = null;
        Dictionary<ISymbol, string>? symbolMap = null;

        if (semanticModel != null)
        {
            var typeSymbol = (INamedTypeSymbol)semanticModel.GetDeclaredSymbol(classDecl)!;
            symbolMap = new(SymbolEqualityComparer.Default);

            foreach (var member in typeSymbol.GetMembers())
            {
                if (member is not IFieldSymbol && member is not IPropertySymbol)
                    continue;

                if (member is IFieldSymbol fieldSymbol && fieldSymbol.IsStatic)
                    continue;
                if (member is IPropertySymbol propSymbol && propSymbol.IsStatic)
                    continue;

                string memberName = member.Name;
                var checker = new InstanceMemberUsageChecker(new HashSet<string> { memberName });
                checker.Visit(method);
                if (!checker.HasInstanceMemberUsage)
                    continue;

                var paramName = memberName.TrimStart('_');
                if (method.ParameterList.Parameters.Any(p => p.Identifier.ValueText == paramName))
                    paramName += "Param";

                symbolMap[member] = paramName;

                var typeName = member switch
                {
                    IFieldSymbol f => f.Type.ToDisplayString(),
                    IPropertySymbol p => p.Type.ToDisplayString(),
                    _ => "object"
                };

                parameters.Add((paramName, typeName));
            }
        }
        else
        {
            renameMap = new();
            var classMembers = new Dictionary<string, string>();

            foreach (var field in classDecl.Members.OfType<FieldDeclarationSyntax>())
            {
                if (field.Modifiers.Any(SyntaxKind.StaticKeyword))
                    continue;
                var typeName = field.Declaration.Type.ToString();
                foreach (var variable in field.Declaration.Variables)
                    classMembers[variable.Identifier.ValueText] = typeName;
            }

            foreach (var prop in classDecl.Members.OfType<PropertyDeclarationSyntax>())
            {
                if (prop.Modifiers.Any(SyntaxKind.StaticKeyword))
                    continue;
                classMembers[prop.Identifier.ValueText] = prop.Type.ToString();
            }

            foreach (var kvp in classMembers)
            {
                var checker = new InstanceMemberUsageChecker(new HashSet<string> { kvp.Key });
                checker.Visit(method);
                if (!checker.HasInstanceMemberUsage)
                    continue;

                var paramName = kvp.Key.TrimStart('_');
                if (method.ParameterList.Parameters.Any(p => p.Identifier.ValueText == paramName))
                    paramName += "Param";
                renameMap[kvp.Key] = paramName;
                parameters.Add((paramName, kvp.Value));
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
        [Description("Name of the method to convert")] string methodName,
        CancellationToken cancellationToken = default)
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
            return $"Error: No method named '{methodName}' found";

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
            return $"Error: No method named '{methodName}' found";

        var classDecl = method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (classDecl == null)
            throw new McpException($"Error: Method '{methodName}' is not inside a class");

        var newRoot = ConvertToStaticWithParametersAst(syntaxRoot, method);
        var formattedRoot = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
        return formattedRoot.ToFullString();
    }
}
