using Microsoft.CodeAnalysis;
using ModelContextProtocol.Server;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.ComponentModel;

public static partial class RefactoringTools
{
    private static async Task<string> MoveInstanceMethodWithSolution(Document document, int methodLine, string targetClass, string accessMemberName, string accessMemberType)
    {
        var sourceText = await document.GetTextAsync();
        var syntaxRoot = await document.GetSyntaxRootAsync();
        var textLines = sourceText.Lines;

        var method = syntaxRoot!.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => textLines.GetLineFromPosition(m.SpanStart).LineNumber + 1 == methodLine);
        if (method == null)
            return $"Error: No method found at line {methodLine}";

        var originClass = method.Parent as ClassDeclarationSyntax;
        if (originClass == null)
            return $"Error: Method at line {methodLine} is not inside a class";

        var targetClassDecl = syntaxRoot.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == targetClass);
        if (targetClassDecl == null)
            return $"Error: Target class '{targetClass}' not found";

        ClassDeclarationSyntax newOriginClass = originClass.RemoveNode(method, SyntaxRemoveOptions.KeepNoTrivia);

        if (accessMemberType == "field")
        {
            var field = SyntaxFactory.FieldDeclaration(
                    SyntaxFactory.VariableDeclaration(
                        SyntaxFactory.ParseTypeName(targetClass),
                        SyntaxFactory.SeparatedList(new[] { SyntaxFactory.VariableDeclarator(accessMemberName)
                            .WithInitializer(SyntaxFactory.EqualsValueClause(
                                SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName(targetClass))
                                    .WithArgumentList(SyntaxFactory.ArgumentList()))) }))
                ).AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));

            newOriginClass = newOriginClass.AddMembers(field);
        }
        else if (accessMemberType == "property")
        {
            var prop = SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName(targetClass), accessMemberName)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword))
                .AddAccessorListAccessors(
                    SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                    SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
            newOriginClass = newOriginClass.AddMembers(prop);
        }

        var newTargetClass = targetClassDecl.AddMembers(method.WithLeadingTrivia());

        var newRoot = syntaxRoot.ReplaceNode(originClass, newOriginClass).ReplaceNode(targetClassDecl, newTargetClass);
        var formatted = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        var newDocument = document.WithSyntaxRoot(formatted);
        var newText = await newDocument.GetTextAsync();
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString());

        return $"Successfully moved instance method to {targetClass} in {document.FilePath}";
    }
    [McpServerTool, Description("Move an instance method to another class")]
    public static async Task<string> MoveInstanceMethod(
        [Description("Path to the C# file containing the method")] string filePath,
        [Description("Line number of the instance method to move")] int methodLine,
        [Description("Name of the target class")] string targetClass,
        [Description("Name for the access member")] string accessMemberName,
        [Description("Type of access member (field, property, variable)")] string accessMemberType = "field",
        [Description("Path to the solution file (.sln)")] string? solutionPath = null)
    {
        if (solutionPath == null)
            return "Error: Solution path is required for moving instance methods";

        try
        {
            var solution = await GetOrLoadSolution(solutionPath);
            var document = GetDocumentByPath(solution, filePath);
            if (document == null)
                return $"Error: File {filePath} not found in solution";

            return await MoveInstanceMethodWithSolution(document, methodLine, targetClass, accessMemberName, accessMemberType);
        }
        catch (Exception ex)
        {
            return $"Error moving instance method: {ex.Message}";
        }
    }
}
