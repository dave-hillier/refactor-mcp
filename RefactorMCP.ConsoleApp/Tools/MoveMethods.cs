using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

public static partial class RefactoringTools
{
    public static string MoveStaticMethodInSource(string sourceText, string methodName, string targetClass)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetRoot();

        var method = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName &&
                                 m.Modifiers.Any(SyntaxKind.StaticKeyword));
        if (method == null)
            return ThrowMcpException($"Error: Static method '{methodName}' not found");

        var withoutMethod = root.RemoveNode(method, SyntaxRemoveOptions.KeepNoTrivia);
        var targetClassDecl = withoutMethod.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == targetClass);

        SyntaxNode newRoot;
        if (targetClassDecl == null)
        {
            var newClass = SyntaxFactory.ClassDeclaration(targetClass)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddMembers(method.WithLeadingTrivia());
            newRoot = ((CompilationUnitSyntax)withoutMethod).AddMembers(newClass);
        }
        else
        {
            var updatedClass = targetClassDecl.AddMembers(method.WithLeadingTrivia());
            newRoot = withoutMethod.ReplaceNode(targetClassDecl, updatedClass);
        }

        var formatted = Formatter.Format(newRoot, SharedWorkspace);
        return formatted.ToFullString();
    }

    public static string MoveInstanceMethodInSource(string sourceText, string sourceClass, string methodName, string targetClass, string accessMemberName, string accessMemberType)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetRoot();

        var originClass = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == sourceClass);
        if (originClass == null)
            return ThrowMcpException($"Error: Source class '{sourceClass}' not found");

        var method = originClass.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            return ThrowMcpException($"Error: No method named '{methodName}' found");

        var withoutMethod = root.RemoveNode(method, SyntaxRemoveOptions.KeepNoTrivia);

        var currentOriginClass = withoutMethod.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == sourceClass);

        var newOriginClass = currentOriginClass;
        if (accessMemberType == "field")
        {
            var field = SyntaxFactory.FieldDeclaration(
                    SyntaxFactory.VariableDeclaration(
                        SyntaxFactory.ParseTypeName(targetClass),
                        SyntaxFactory.SeparatedList(new[]
                        {
                            SyntaxFactory.VariableDeclarator(accessMemberName)
                                .WithInitializer(SyntaxFactory.EqualsValueClause(
                                    SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName(targetClass))
                                        .WithArgumentList(SyntaxFactory.ArgumentList())))
                        }))
                )
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));
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

        var withOriginReplaced = withoutMethod.ReplaceNode(currentOriginClass, newOriginClass);

        var targetClassDecl = withOriginReplaced.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == targetClass);

        SyntaxNode newRoot;
        if (targetClassDecl == null)
        {
            var newClass = SyntaxFactory.ClassDeclaration(targetClass)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddMembers(updatedMethod.WithLeadingTrivia());
            newRoot = ((CompilationUnitSyntax)withOriginReplaced).AddMembers(newClass);
        }
        else
        {
            var replaced = targetClassDecl.AddMembers(updatedMethod.WithLeadingTrivia());
            newRoot = withOriginReplaced.ReplaceNode(targetClassDecl, replaced);
        }

        var formatted = Formatter.Format(newRoot, SharedWorkspace);
        return formatted.ToFullString();
    }

    private static async Task<string> MoveInstanceMethodWithSolution(Document document, string sourceClass, string methodName, string targetClass, string accessMemberName, string accessMemberType)
    {
        var sourceText = await document.GetTextAsync();
        var syntaxRoot = await document.GetSyntaxRootAsync();
        var originClass = syntaxRoot!.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == sourceClass);
        if (originClass == null)
            return ThrowMcpException($"Error: Source class '{sourceClass}' not found");

        var method = originClass.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            return ThrowMcpException($"Error: No method named '{methodName}' found");

        var targetClassDecl = syntaxRoot.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == targetClass);
        SyntaxNode workingRoot = syntaxRoot;
        if (targetClassDecl == null)
        {
            targetClassDecl = SyntaxFactory.ClassDeclaration(targetClass)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));

            if (originClass.Parent is NamespaceDeclarationSyntax ns)
            {
                var updatedNs = ns.AddMembers(targetClassDecl);
                workingRoot = workingRoot.ReplaceNode(ns, updatedNs);
            }
            else
            {
                workingRoot = ((CompilationUnitSyntax)workingRoot).AddMembers(targetClassDecl);
            }
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

        var newTargetClass = targetClassDecl.AddMembers(updatedMethod.WithLeadingTrivia());

        var newRoot = workingRoot.ReplaceNode(originClass, newOriginClass).ReplaceNode(targetClassDecl, newTargetClass);
        var formatted = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        var newDocument = document.WithSyntaxRoot(formatted);
        var newText = await newDocument.GetTextAsync();
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString());

        return $"Successfully moved instance method to {targetClass} in {document.FilePath}";
    }


    private static async Task<string> MoveInstanceMethodSingleFile(string filePath, string sourceClass, string methodName, string targetClass, string accessMemberName, string accessMemberType, string? targetFilePath)
    {
        if (!File.Exists(filePath))
            return ThrowMcpException($"Error: File {filePath} not found (current dir: {Directory.GetCurrentDirectory()})");

        var targetPath = targetFilePath ?? filePath;
        var sameFile = targetPath == filePath;

        var sourceText = await File.ReadAllTextAsync(filePath);
        if (sameFile)
        {
            var updated = MoveInstanceMethodInSource(sourceText, sourceClass, methodName, targetClass, accessMemberName, accessMemberType);
            await File.WriteAllTextAsync(targetPath, updated);
            return $"Successfully moved instance method to {targetClass} in {targetPath}";
        }

        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = await syntaxTree.GetRootAsync();

        var originClass = syntaxRoot.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == sourceClass);
        if (originClass == null)
            return ThrowMcpException($"Error: Source class '{sourceClass}' not found");

        var method = originClass.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            return ThrowMcpException($"Error: No method named '{methodName}' found");

        var targetClassDecl = syntaxRoot.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == targetClass);
        SyntaxNode workingRoot = syntaxRoot;
        if (targetClassDecl == null)
        {
            targetClassDecl = SyntaxFactory.ClassDeclaration(targetClass)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));

            if (originClass.Parent is NamespaceDeclarationSyntax ns)
            {
                var updatedNs = ns.AddMembers(targetClassDecl);
                workingRoot = workingRoot.ReplaceNode(ns, updatedNs);
            }
            else
            {
                workingRoot = ((CompilationUnitSyntax)workingRoot).AddMembers(targetClassDecl);
            }
        }

        // Get the updated reference to the origin class after any modifications to workingRoot
        var currentOriginClass = workingRoot.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == sourceClass);
        if (currentOriginClass == null)
            return ThrowMcpException($"Error: Could not find updated reference to source class '{sourceClass}'");

        // Find the method in the current origin class reference
        var currentMethod = currentOriginClass.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (currentMethod == null)
            return ThrowMcpException($"Error: No method named '{methodName}' found in updated class reference");

        ClassDeclarationSyntax newOriginClass = currentOriginClass.RemoveNode(currentMethod, SyntaxRemoveOptions.KeepNoTrivia);

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

        var updatedMethod = currentMethod;
        if (!currentMethod.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
        {
            var mods = currentMethod.Modifiers.Where(m => !m.IsKind(SyntaxKind.PrivateKeyword) && !m.IsKind(SyntaxKind.ProtectedKeyword) && !m.IsKind(SyntaxKind.InternalKeyword));
            updatedMethod = currentMethod.WithModifiers(SyntaxFactory.TokenList(mods).Add(SyntaxFactory.Token(SyntaxKind.PublicKeyword)));
        }

        workingRoot = workingRoot.ReplaceNode(currentOriginClass, newOriginClass);

        var sourceCompilationUnit = (CompilationUnitSyntax)syntaxRoot;
        var sourceUsings = sourceCompilationUnit.Usings.ToList();

        SyntaxNode targetRoot;
        if (sameFile)
        {
            targetRoot = workingRoot;
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

        var targetCompilationUnit = (CompilationUnitSyntax)targetRoot;
        var targetUsingNames = targetCompilationUnit.Usings
            .Select(u => u.Name.ToString())
            .ToHashSet();
        var missingUsings = sourceUsings
            .Where(u => !targetUsingNames.Contains(u.Name.ToString()))
            .ToArray();
        if (missingUsings.Length > 0)
        {
            targetCompilationUnit = targetCompilationUnit.AddUsings(missingUsings);
            targetRoot = targetCompilationUnit;
        }

        var newTargetClass = targetRoot.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == targetClass);
        if (newTargetClass == null)
        {
            var newClass = SyntaxFactory.ClassDeclaration(targetClass)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddMembers(updatedMethod.WithLeadingTrivia());
            targetRoot = ((CompilationUnitSyntax)targetRoot).AddMembers(newClass);
        }
        else
        {
            var replaced = newTargetClass.AddMembers(updatedMethod.WithLeadingTrivia());
            targetRoot = targetRoot.ReplaceNode(newTargetClass, replaced);
        }

        if (sameFile)
        {
            var formatted = Formatter.Format(targetRoot, SharedWorkspace);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await File.WriteAllTextAsync(targetPath, formatted.ToFullString());
        }
        else
        {
            var formattedSource = Formatter.Format(workingRoot, SharedWorkspace);
            await File.WriteAllTextAsync(filePath, formattedSource.ToFullString());

            var formattedTarget = Formatter.Format(targetRoot, SharedWorkspace);
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
                return ThrowMcpException($"Error: File {filePath} not found (current dir: {Directory.GetCurrentDirectory()})");

            var sourceText = await File.ReadAllTextAsync(filePath);
            var targetPath = targetFilePath ?? Path.Combine(Path.GetDirectoryName(filePath)!, $"{targetClass}.cs");
            var sameFile = targetPath == filePath;

            if (sameFile)
            {
                var updated = MoveStaticMethodInSource(sourceText, methodName, targetClass);
                await File.WriteAllTextAsync(targetPath, updated);
                return $"Successfully moved static method '{methodName}' to {targetClass} in {targetPath}";
            }

            var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
            var syntaxRoot = await syntaxTree.GetRootAsync();

            var sourceUsings = syntaxRoot.DescendantNodes()
                .OfType<UsingDirectiveSyntax>()
                .ToList();

            var method = syntaxRoot.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.ValueText == methodName &&
                                    m.Modifiers.Any(SyntaxKind.StaticKeyword));
            if (method == null)
                return ThrowMcpException($"Error: Static method '{methodName}' not found");

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

            var targetCompilationUnit = (CompilationUnitSyntax)targetRoot;
            var targetUsingNames = targetCompilationUnit.Usings
                .Select(u => u.Name.ToString())
                .ToHashSet();
            var missingUsings = sourceUsings
                .Where(u => !targetUsingNames.Contains(u.Name.ToString()))
                .ToArray();
            if (missingUsings.Length > 0)
            {
                targetCompilationUnit = targetCompilationUnit.AddUsings(missingUsings);
                targetRoot = targetCompilationUnit;
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
            throw new McpException($"Error moving static method: {ex.Message}", ex);
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
        [Description("Path to the solution file (.sln)")] string? solutionPath = null,
        [Description("Path to the target file (optional, will create if doesn't exist)")] string? targetFilePath = null)
    {
        try
        {
            if (solutionPath != null)
            {
                var solution = await GetOrLoadSolution(solutionPath);
                var document = GetDocumentByPath(solution, filePath);
                if (document != null)
                    return await MoveInstanceMethodWithSolution(document, sourceClass, methodName, targetClass, accessMemberName, accessMemberType);

                // Fallback to single file mode when file isn't part of the solution
                return await MoveInstanceMethodSingleFile(filePath, sourceClass, methodName, targetClass, accessMemberName, accessMemberType, targetFilePath);
            }

            return await MoveInstanceMethodSingleFile(filePath, sourceClass, methodName, targetClass, accessMemberName, accessMemberType, targetFilePath);
        }
        catch (Exception ex)
        {
            throw new McpException($"Error moving instance method: {ex.Message}", ex);
        }
    }
}
