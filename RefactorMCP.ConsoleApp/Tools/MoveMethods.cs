using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

public static partial class RefactoringTools
{

    private static async Task<string> MoveInstanceMethodWithSolution(Document document, string sourceClass, string methodName, string targetClass, string accessMemberName, string accessMemberType, string? targetFilePath)
    {
        // Delegate to the single file implementation using the document path.
        return await MoveInstanceMethodSingleFile(document.FilePath!, sourceClass, methodName, targetClass, accessMemberName, accessMemberType, targetFilePath);
    }


    private static async Task<string> MoveInstanceMethodSingleFile(string filePath, string sourceClass, string methodName, string targetClass, string accessMemberName, string accessMemberType, string? targetFilePath)
    {
        if (!File.Exists(filePath))
            return $"Error: File {filePath} not found (current dir: {Directory.GetCurrentDirectory()})";

        var sourceText = await File.ReadAllTextAsync(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = await syntaxTree.GetRootAsync();

        var originClass = syntaxRoot.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == sourceClass);
        if (originClass == null)
            return $"Error: Source class '{sourceClass}' not found";

        var method = originClass.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            return $"Error: No method named '{methodName}' found";

        var targetPath = targetFilePath ?? Path.Combine(Path.GetDirectoryName(filePath)!, $"{targetClass}.cs");
        var sameFile = Path.GetFullPath(targetPath) == Path.GetFullPath(filePath);

        ClassDeclarationSyntax? targetClassDecl = null;
        SyntaxNode targetRoot = syntaxRoot;
        if (sameFile)
        {
            targetClassDecl = syntaxRoot.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.ValueText == targetClass);
            if (targetClassDecl == null)
                return $"Error: Target class '{targetClass}' not found";
        }
        else if (File.Exists(targetPath))
        {
            var targetText = await File.ReadAllTextAsync(targetPath);
            targetRoot = await CSharpSyntaxTree.ParseText(targetText).GetRootAsync();
            targetClassDecl = targetRoot.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.ValueText == targetClass);
        }
        else
        {
            targetRoot = SyntaxFactory.CompilationUnit();
        }

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

        var updatedMethod = method;
        if (!method.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
        {
            var mods = method.Modifiers.Where(m => !m.IsKind(SyntaxKind.PrivateKeyword) && !m.IsKind(SyntaxKind.ProtectedKeyword) && !m.IsKind(SyntaxKind.InternalKeyword));
            updatedMethod = method.WithModifiers(SyntaxFactory.TokenList(mods).Add(SyntaxFactory.Token(SyntaxKind.PublicKeyword)));
        }

        CompilationUnitSyntax targetCompilation = (CompilationUnitSyntax)targetRoot;
        if (!sameFile)
        {
            var sourceCompilation = (CompilationUnitSyntax)syntaxRoot;
            var usingsToAdd = sourceCompilation.Usings
                .Where(u => !targetCompilation.Usings.Any(t => t.Name.ToString() == u.Name.ToString()));
            targetCompilation = targetCompilation.AddUsings(usingsToAdd.ToArray());
        }

        if (targetClassDecl == null)
        {
            var newClass = SyntaxFactory.ClassDeclaration(targetClass)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddMembers(updatedMethod.WithLeadingTrivia());
            targetCompilation = targetCompilation.AddMembers(newClass);
        }
        else
        {
            var newTargetClass = targetClassDecl.AddMembers(updatedMethod.WithLeadingTrivia());
            targetCompilation = targetCompilation.ReplaceNode(targetClassDecl, newTargetClass);
        }

        var formattedTarget = Formatter.Format(targetCompilation, SharedWorkspace);

        if (sameFile)
        {
            var newRoot = formattedTarget;
            await File.WriteAllTextAsync(filePath, newRoot.ToFullString());
        }
        else
        {
            var newSourceRoot = syntaxRoot.ReplaceNode(originClass, newOriginClass);
            var formattedSource = Formatter.Format(newSourceRoot, SharedWorkspace);
            await File.WriteAllTextAsync(filePath, formattedSource.ToFullString());

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await File.WriteAllTextAsync(targetPath, formattedTarget.ToFullString());
        }

        return $"Successfully moved instance method to {targetClass} in {targetPath}";
    }

    [McpServerTool, Description("Move a static method to another class (preferred for large C# file refactoring)")]
    public static async Task<string> MoveStaticMethod(
        [Description("Path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file containing the method")] string filePath,
        [Description("Name of the static method to move")] string methodName,
        [Description("Name of the target class")] string targetClass,
        [Description("Path to the target file (optional, will create if doesn't exist)")] string? targetFilePath = null)
    {
        try
        {
            if (!File.Exists(filePath))
                return $"Error: File {filePath} not found (current dir: {Directory.GetCurrentDirectory()})";

            var sourceText = await File.ReadAllTextAsync(filePath);
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
            var syntaxRoot = await syntaxTree.GetRootAsync();

            var method = syntaxRoot.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.ValueText == methodName &&
                                    m.Modifiers.Any(SyntaxKind.StaticKeyword));
            if (method == null)
                return $"Error: Static method '{methodName}' not found";

            var targetPath = targetFilePath ?? Path.Combine(Path.GetDirectoryName(filePath)!, $"{targetClass}.cs");
            var sameFile = targetPath == filePath;

            // Remove the original method
            var newSourceRoot = syntaxRoot.RemoveNode(method, SyntaxRemoveOptions.KeepNoTrivia);

            SyntaxNode targetRoot;
            if (sameFile)
            {
                targetRoot = newSourceRoot;
            }
            else if (File.Exists(targetPath))
            {
                var targetText = await File.ReadAllTextAsync(targetPath);
                targetRoot = await CSharpSyntaxTree.ParseText(targetText).GetRootAsync();
            }
            else
            {
                targetRoot = SyntaxFactory.CompilationUnit();
            }

            var targetClassDecl = targetRoot.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.ValueText == targetClass);

            if (targetClassDecl == null)
            {
                var newClass = SyntaxFactory.ClassDeclaration(targetClass)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                    .AddMembers(method.WithLeadingTrivia());

                targetRoot = ((CompilationUnitSyntax)targetRoot).AddMembers(newClass);
            }
            else
            {
                var newTarget = targetClassDecl.AddMembers(method.WithLeadingTrivia());
                targetRoot = targetRoot.ReplaceNode(targetClassDecl, newTarget);
            }

            var formattedTarget = Formatter.Format(targetRoot, SharedWorkspace);

            if (!sameFile)
            {
                var formattedSource = Formatter.Format(newSourceRoot, SharedWorkspace);
                await File.WriteAllTextAsync(filePath, formattedSource.ToFullString());
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await File.WriteAllTextAsync(targetPath, formattedTarget.ToFullString());

            return $"Successfully moved static method '{methodName}' to {targetClass} in {targetPath}";
        }
        catch (Exception ex)
        {
            return $"Error moving static method: {ex.Message}";
        }
    }
    [McpServerTool, Description("Move an instance method to another class (preferred for large C# file refactoring)")]
    public static async Task<string> MoveInstanceMethod(
        [Description("Path to the C# file containing the method")] string filePath,
        [Description("Name of the source class containing the method")] string sourceClass,
        [Description("Name of the method to move")] string methodName,
        [Description("Name of the target class")] string targetClass,
        [Description("Name for the access member")] string accessMemberName,
        [Description("Type of access member (field, property, variable)")] string accessMemberType = "field",
        [Description("Path to the target file (optional, will create if doesn't exist)")] string? targetFilePath = null,
        [Description("Path to the solution file (.sln)")] string? solutionPath = null)
    {
        try
        {
            if (solutionPath != null)
            {
                var solution = await GetOrLoadSolution(solutionPath);
                var document = GetDocumentByPath(solution, filePath);
                if (document != null)
                    return await MoveInstanceMethodWithSolution(document, sourceClass, methodName, targetClass, accessMemberName, accessMemberType, targetFilePath);

                // Fallback to single file mode when file isn't part of the solution
                return await MoveInstanceMethodSingleFile(filePath, sourceClass, methodName, targetClass, accessMemberName, accessMemberType, targetFilePath);
            }

            return await MoveInstanceMethodSingleFile(filePath, sourceClass, methodName, targetClass, accessMemberName, accessMemberType, targetFilePath);
        }
        catch (Exception ex)
        {
            return $"Error moving instance method: {ex.Message}";
        }
    }
}
