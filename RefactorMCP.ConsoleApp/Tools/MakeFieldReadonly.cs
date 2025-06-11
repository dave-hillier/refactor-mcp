using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using System.Linq;

[McpServerToolType]
public static class MakeFieldReadonlyTool
{
    public static async Task<string> MakeFieldReadonly(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Name of the field to make readonly")] string fieldName)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath);
            var document = RefactoringHelpers.GetDocumentByPath(solution, filePath);
            if (document != null)
                return await MakeFieldReadonlyWithSolution(document, fieldName);

            return await MakeFieldReadonlySingleFile(filePath, fieldName);
        }
        catch (Exception ex)
        {
            throw new McpException($"Error: {ex.Message}", ex);
        }
    }

    private static async Task<string> MakeFieldReadonlyWithSolution(Document document, string fieldName)
    {
        var sourceText = await document.GetTextAsync();
        var syntaxRoot = await document.GetSyntaxRootAsync();

        var fieldDeclaration = syntaxRoot!.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .FirstOrDefault(f => f.Declaration.Variables.Any(v => v.Identifier.ValueText == fieldName));

        if (fieldDeclaration == null)
            return RefactoringHelpers.ThrowMcpException($"Error: No field named '{fieldName}' found");

        // Add readonly modifier
        var readonlyModifier = SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword);
        var newModifiers = fieldDeclaration.Modifiers.Add(readonlyModifier);
        var newFieldDeclaration = fieldDeclaration.WithModifiers(newModifiers);

        // Remove initializer if present (will be moved to constructor)
        var variable = fieldDeclaration.Declaration.Variables.First();
        var initializer = variable.Initializer;

        if (initializer != null)
        {
            var newVariable = variable.WithInitializer(null);
            var newDeclaration = fieldDeclaration.Declaration.WithVariables(
                SyntaxFactory.SingletonSeparatedList(newVariable));
            newFieldDeclaration = newFieldDeclaration.WithDeclaration(newDeclaration);

            // Find constructors and add initialization
            var containingClass = fieldDeclaration.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (containingClass != null)
            {
                var constructors = containingClass.Members.OfType<ConstructorDeclarationSyntax>().ToList();

                if (constructors.Any())
                {
                    var updatedConstructors = new List<ConstructorDeclarationSyntax>();
                    foreach (var constructor in constructors)
                    {
                        var assignment = SyntaxFactory.ExpressionStatement(
                            SyntaxFactory.AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                SyntaxFactory.IdentifierName(variable.Identifier.ValueText),
                                initializer.Value));

                        var newBody = constructor.Body?.AddStatements(assignment) ??
                            SyntaxFactory.Block(assignment);

                        updatedConstructors.Add(constructor.WithBody(newBody));
                    }

                    var newMembers = containingClass.Members.ToList();
                    foreach (var (oldCtor, newCtor) in constructors.Zip(updatedConstructors))
                    {
                        var index = newMembers.IndexOf(oldCtor);
                        newMembers[index] = newCtor;
                    }

                    var fieldIndex = newMembers.IndexOf(fieldDeclaration);
                    newMembers[fieldIndex] = newFieldDeclaration;

                    var updatedClass = containingClass.WithMembers(SyntaxFactory.List(newMembers));
                    var newRoot = syntaxRoot.ReplaceNode(containingClass, updatedClass);

                    var formattedRoot = Formatter.Format(newRoot, document.Project.Solution.Workspace);
                    var newDocument = document.WithSyntaxRoot(formattedRoot);
                    var newText = await newDocument.GetTextAsync();
                    await File.WriteAllTextAsync(document.FilePath!, newText.ToString());

                    return $"Successfully made field '{fieldName}' readonly and moved initialization to constructors in {document.FilePath}";
                }
            }
        }
        else
        {
            var newRoot = syntaxRoot.ReplaceNode(fieldDeclaration, newFieldDeclaration);
            var formattedRoot = Formatter.Format(newRoot, document.Project.Solution.Workspace);
            var newDocument = document.WithSyntaxRoot(formattedRoot);
            var newText = await newDocument.GetTextAsync();
            await File.WriteAllTextAsync(document.FilePath!, newText.ToString());

            return $"Successfully made field '{fieldName}' readonly in {document.FilePath}";
        }

        return $"Field '{fieldName}' made readonly, but no constructors found for initialization";
    }

    private static async Task<string> MakeFieldReadonlySingleFile(string filePath, string fieldName)
    {
        if (!File.Exists(filePath))
            return RefactoringHelpers.ThrowMcpException($"Error: File {filePath} not found (current dir: {Directory.GetCurrentDirectory()})");

        var sourceText = await File.ReadAllTextAsync(filePath);
        var newText = MakeFieldReadonlyInSource(sourceText, fieldName);
        await File.WriteAllTextAsync(filePath, newText);

        return $"Successfully made field '{fieldName}' readonly in {filePath} (single file mode)";
    }

    public static string MakeFieldReadonlyInSource(string sourceText, string fieldName)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = syntaxTree.GetRoot();
        var fieldDeclaration = syntaxRoot.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .FirstOrDefault(f => f.Declaration.Variables.Any(v => v.Identifier.ValueText == fieldName));

        if (fieldDeclaration == null)
            return RefactoringHelpers.ThrowMcpException($"Error: No field named '{fieldName}' found");

        var readonlyModifier = SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword);
        var newModifiers = fieldDeclaration.Modifiers.Add(readonlyModifier);
        var newFieldDeclaration = fieldDeclaration.WithModifiers(newModifiers);

        var variable = fieldDeclaration.Declaration.Variables.First();
        var initializer = variable.Initializer;

        if (initializer != null)
        {
            var newVariable = variable.WithInitializer(null);
            var newDeclaration = fieldDeclaration.Declaration.WithVariables(
                SyntaxFactory.SingletonSeparatedList(newVariable));
            newFieldDeclaration = newFieldDeclaration.WithDeclaration(newDeclaration);

            var containingClass = fieldDeclaration.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (containingClass != null)
            {
                var constructors = containingClass.Members.OfType<ConstructorDeclarationSyntax>().ToList();

                if (constructors.Any())
                {
                    var updatedConstructors = new List<ConstructorDeclarationSyntax>();
                    foreach (var constructor in constructors)
                    {
                        var assignment = SyntaxFactory.ExpressionStatement(
                            SyntaxFactory.AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                SyntaxFactory.IdentifierName(variable.Identifier.ValueText),
                                initializer.Value));

                        var newBody = constructor.Body?.AddStatements(assignment) ??
                            SyntaxFactory.Block(assignment);

                        updatedConstructors.Add(constructor.WithBody(newBody));
                    }

                    var newMembers = containingClass.Members.ToList();
                    foreach (var (oldCtor, newCtor) in constructors.Zip(updatedConstructors))
                    {
                        var index = newMembers.IndexOf(oldCtor);
                        newMembers[index] = newCtor;
                    }

                    var fieldIndex = newMembers.IndexOf(fieldDeclaration);
                    newMembers[fieldIndex] = newFieldDeclaration;

                    var updatedClass = containingClass.WithMembers(SyntaxFactory.List(newMembers));
                    var newRoot = syntaxRoot.ReplaceNode(containingClass, updatedClass);
                    var formattedRoot = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
                    return formattedRoot.ToFullString();
                }
            }
        }
        else
        {
            var newRoot = syntaxRoot.ReplaceNode(fieldDeclaration, newFieldDeclaration);
            var formattedRoot = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
            return formattedRoot.ToFullString();
        }

        return Formatter.Format(syntaxRoot, RefactoringHelpers.SharedWorkspace).ToFullString();
    }

}
