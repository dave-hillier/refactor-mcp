using ModelContextProtocol.Server;
using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.ComponentModel;

[McpServerToolType]
public static class ExtractInterfaceTool
{
    [McpServerTool, Description("Extract a simple interface from a class")]
    public static async Task<string> ExtractInterface(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file containing the class")] string filePath,
        [Description("Name of the class to extract from")] string className,
        [Description("Comma separated list of member names to include")] string memberList,
        [Description("Path to write the generated interface file")] string interfaceFilePath)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath);
            var document = RefactoringHelpers.GetDocumentByPath(solution, filePath);
            if (document == null)
                return RefactoringHelpers.ThrowMcpException($"Error: File {filePath} not found in solution");

            var root = (CompilationUnitSyntax)await document.GetSyntaxRootAsync();
            var classNode = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.ValueText == className);
            if (classNode == null)
                return RefactoringHelpers.ThrowMcpException($"Error: Class {className} not found");

            var chosen = memberList.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(m => m.Trim()).ToHashSet(StringComparer.Ordinal);

            var members = new List<MemberDeclarationSyntax>();
            foreach (var member in classNode.Members)
            {
                var name = member switch
                {
                    MethodDeclarationSyntax m => m.Identifier.ValueText,
                    PropertyDeclarationSyntax p => p.Identifier.ValueText,
                    _ => null
                };
                if (name != null && chosen.Contains(name))
                {
                    switch (member)
                    {
                        case MethodDeclarationSyntax m:
                            members.Add(m.WithBody(null)
                                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                                .WithModifiers(new SyntaxTokenList()));
                            break;
                        case PropertyDeclarationSyntax p:
                            var accessors = p.AccessorList ?? SyntaxFactory.AccessorList();
                            accessors = SyntaxFactory.AccessorList(SyntaxFactory.List(
                                accessors.Accessors.Select(a => a.WithBody(null)
                                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)))));
                            members.Add(p.WithAccessorList(accessors).WithModifiers(new SyntaxTokenList()));
                            break;
                    }
                }
            }

            if (members.Count == 0)
                return RefactoringHelpers.ThrowMcpException("Error: No matching members found");

            var interfaceName = Path.GetFileNameWithoutExtension(interfaceFilePath);
            var iface = SyntaxFactory.InterfaceDeclaration(interfaceName)
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                .WithMembers(SyntaxFactory.List(members));

            MemberDeclarationSyntax interfaceNode = iface;
            if (classNode.Parent is NamespaceDeclarationSyntax ns)
            {
                interfaceNode = SyntaxFactory.NamespaceDeclaration(ns.Name)
                    .WithMembers(SyntaxFactory.SingletonList(interfaceNode));
            }

            var ifaceUnit = SyntaxFactory.CompilationUnit()
                .WithUsings(root.Usings)
                .WithMembers(SyntaxFactory.SingletonList(interfaceNode))
                .NormalizeWhitespace();

            var encoding = await RefactoringHelpers.GetFileEncodingAsync(filePath);
            await File.WriteAllTextAsync(interfaceFilePath, ifaceUnit.ToFullString(), encoding);

            var baseList = SyntaxFactory.BaseList(
                    SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(
                        SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(interfaceName))))
                .WithColonToken(SyntaxFactory.Token(SyntaxKind.ColonToken).WithTrailingTrivia(SyntaxFactory.Space));
            var updatedClass = classNode.WithBaseList(baseList);
            var newRoot = root.ReplaceNode(classNode, updatedClass);
            var formatted = newRoot.NormalizeWhitespace().ToFullString();
            await File.WriteAllTextAsync(filePath, formatted, encoding);
            RefactoringHelpers.UpdateFileCaches(filePath, formatted);
            RefactoringHelpers.AddDocumentToProject(document.Project, interfaceFilePath);
            return $"Successfully extracted interface '{interfaceName}' to {interfaceFilePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error extracting interface: {ex.Message}", ex);
        }
    }
}
