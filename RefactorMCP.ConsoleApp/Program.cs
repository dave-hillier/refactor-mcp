using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;

// Parse command line arguments
if (args.Length > 0 && args[0] == "--test")
{
    await RunTestMode(args);
    return;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(consoleLogOptions =>
{
    // Configure all logs to go to stderr
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();

static async Task RunTestMode(string[] args)
{
    if (args.Length < 2)
    {
        ShowTestModeHelp();
        return;
    }

    var command = args[1].ToLower();
    
    try
    {
        var result = command switch
        {
            "load-solution" => await TestLoadSolution(args),
            "extract-method" => await TestExtractMethod(args),
            "introduce-field" => await TestIntroduceField(args),
            "introduce-variable" => await TestIntroduceVariable(args),
            "make-field-readonly" => await TestMakeFieldReadonly(args),
            "list-tools" => ListAvailableTools(),
            _ => $"Unknown command: {command}. Use --test list-tools to see available commands."
        };
        
        Console.WriteLine(result);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        Environment.Exit(1);
    }
}

static void ShowTestModeHelp()
{
    Console.WriteLine("RefactorMCP Test Mode");
    Console.WriteLine("Usage: RefactorMCP.ConsoleApp --test <command> [arguments]");
    Console.WriteLine();
    Console.WriteLine("Available commands:");
    Console.WriteLine("  list-tools                                    - List all available refactoring tools");
    Console.WriteLine("  load-solution <solutionPath>                 - Test loading a solution file");
    Console.WriteLine("  extract-method <filePath> <range> <methodName> [solutionPath]");
    Console.WriteLine("  introduce-field <filePath> <range> <fieldName> [accessModifier] [solutionPath]");
    Console.WriteLine("  introduce-variable <filePath> <range> <variableName> [solutionPath]");
    Console.WriteLine("  make-field-readonly <filePath> <fieldLine> [solutionPath]");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  --test load-solution ./MySolution.sln");
    Console.WriteLine("  --test extract-method ./MyFile.cs \"10:5-15:20\" \"ExtractedMethod\"");
    Console.WriteLine("  --test extract-method ./MyFile.cs \"10:5-15:20\" \"ExtractedMethod\" ./MySolution.sln");
    Console.WriteLine("  --test introduce-field ./MyFile.cs \"12:10-12:25\" \"_myField\" \"private\"");
    Console.WriteLine("  --test make-field-readonly ./MyFile.cs 15");
    Console.WriteLine();
    Console.WriteLine("Range format: \"startLine:startColumn-endLine:endColumn\" (1-based)");
    Console.WriteLine("Note: Solution path is optional. When omitted, single file mode is used with limited semantic analysis.");
}

static string ListAvailableTools()
{
    var tools = new[]
    {
        "load-solution - Load a solution file for refactoring operations",
        "extract-method - Extract selected code into a new method",
        "introduce-field - Create a new field from selected code",
        "introduce-variable - Create a new variable from selected code",
        "make-field-readonly - Make a field readonly and move initialization to constructors",
        "introduce-parameter - Create a new parameter from selected code (TODO)",
        "convert-to-static-with-parameters - Transform instance method to static (TODO)",
        "convert-to-static-with-instance - Transform instance method to static with instance parameter (TODO)",
        "move-static-method - Move a static method to another class (TODO)",
        "move-instance-method - Move an instance method to another class (TODO)",
        "transform-setter-to-init - Convert property setter to init-only setter (TODO)",
        "safe-delete - Safely delete a field, parameter, or variable (TODO)"
    };
    
    return "Available refactoring tools:\n" + string.Join("\n", tools);
}

static async Task<string> TestLoadSolution(string[] args)
{
    if (args.Length < 3)
        return "Error: Missing solution path. Usage: --test load-solution <solutionPath>";
    
    var solutionPath = args[2];
    return await RefactoringTools.LoadSolution(solutionPath);
}

static async Task<string> TestExtractMethod(string[] args)
{
    if (args.Length < 5)
        return "Error: Missing arguments. Usage: --test extract-method <filePath> <range> <methodName> [solutionPath]";
    
    var filePath = args[2];
    var range = args[3];
    var methodName = args[4];
    var solutionPath = args.Length > 5 ? args[5] : null;
    
    return await RefactoringTools.ExtractMethod(filePath, range, methodName, solutionPath);
}

static async Task<string> TestIntroduceField(string[] args)
{
    if (args.Length < 5)
        return "Error: Missing arguments. Usage: --test introduce-field <filePath> <range> <fieldName> [accessModifier] [solutionPath]";
    
    var filePath = args[2];
    var range = args[3];
    var fieldName = args[4];
    var accessModifier = args.Length > 5 ? args[5] : "private";
    var solutionPath = args.Length > 6 ? args[6] : null;
    
    return await RefactoringTools.IntroduceField(filePath, range, fieldName, accessModifier, solutionPath);
}

static async Task<string> TestIntroduceVariable(string[] args)
{
    if (args.Length < 5)
        return "Error: Missing arguments. Usage: --test introduce-variable <filePath> <range> <variableName> [solutionPath]";
    
    var filePath = args[2];
    var range = args[3];
    var variableName = args[4];
    var solutionPath = args.Length > 5 ? args[5] : null;
    
    return await RefactoringTools.IntroduceVariable(filePath, range, variableName, solutionPath);
}

static async Task<string> TestMakeFieldReadonly(string[] args)
{
    if (args.Length < 4)
        return "Error: Missing arguments. Usage: --test make-field-readonly <filePath> <fieldLine> [solutionPath]";
    
    var filePath = args[2];
    if (!int.TryParse(args[3], out var fieldLine))
        return "Error: Invalid field line number";
    var solutionPath = args.Length > 4 ? args[4] : null;
    
    return await RefactoringTools.MakeFieldReadonly(filePath, fieldLine, solutionPath);
}

[McpServerToolType]
public static class RefactoringTools
{
    private static readonly Dictionary<string, Solution> _loadedSolutions = new();

    [McpServerTool, Description("Load a solution file for refactoring operations")]
    public static async Task<string> LoadSolution(
        [Description("Path to the solution file (.sln)")] string solutionPath)
    {
        try
        {
            if (!File.Exists(solutionPath))
            {
                return $"Error: Solution file not found at {solutionPath}";
            }

            using var workspace = MSBuildWorkspace.Create();
            var solution = await workspace.OpenSolutionAsync(solutionPath);
            
            _loadedSolutions[solutionPath] = solution;
            
            var projects = solution.Projects.Select(p => p.Name).ToList();
            return $"Successfully loaded solution '{Path.GetFileName(solutionPath)}' with {projects.Count} projects: {string.Join(", ", projects)}";
        }
        catch (Exception ex)
        {
            return $"Error loading solution: {ex.Message}";
        }
    }

    [McpServerTool, Description("Extract selected code into a new method")]
    public static async Task<string> ExtractMethod(
        [Description("Path to the C# file")] string filePath,
        [Description("Range in format 'startLine:startColumn-endLine:endColumn'")] string selectionRange,
        [Description("Name for the new method")] string methodName,
        [Description("Path to the solution file (.sln) - optional for single file mode")] string? solutionPath = null)
    {
        try
        {
            if (solutionPath != null)
            {
                // Solution mode - full semantic analysis
                var solution = await GetOrLoadSolution(solutionPath);
                var document = GetDocumentByPath(solution, filePath);
                if (document != null)
                    return await ExtractMethodWithSolution(document, selectionRange, methodName);

                // Fallback to single file mode when file isn't part of the solution
                return await ExtractMethodSingleFile(filePath, selectionRange, methodName);
            }

            // Single file mode - direct syntax tree manipulation
            return await ExtractMethodSingleFile(filePath, selectionRange, methodName);
        }
        catch (Exception ex)
        {
            return $"Error extracting method: {ex.Message}";
        }
    }

    private static async Task<string> ExtractMethodWithSolution(Document document, string selectionRange, string methodName)
    {
        var sourceText = await document.GetTextAsync();
        var syntaxRoot = await document.GetSyntaxRootAsync();
        
        if (!TryParseRange(selectionRange, out var startLine, out var startColumn, out var endLine, out var endColumn))
            return "Error: Invalid selection range format. Use 'startLine:startColumn-endLine:endColumn'";

        var startPosition = sourceText.Lines[startLine - 1].Start + startColumn - 1;
        var endPosition = sourceText.Lines[endLine - 1].Start + endColumn - 1;
        var span = TextSpan.FromBounds(startPosition, endPosition);

        var selectedNodes = syntaxRoot!.DescendantNodes()
            .Where(n => span.Contains(n.Span))
            .ToList();

        if (!selectedNodes.Any())
            return "Error: No valid code selected";

        var statementsToExtract = selectedNodes
            .OfType<StatementSyntax>()
            .Where(s => span.IntersectsWith(s.Span))
            .ToList();

        if (!statementsToExtract.Any())
            return "Error: Selected code does not contain extractable statements";

        // Find the containing method
        var containingMethod = selectedNodes.First().Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (containingMethod == null)
            return "Error: Selected code is not within a method";

        // Create the new method
        var newMethod = SyntaxFactory.MethodDeclaration(
            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
            methodName)
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword)))
            .WithBody(SyntaxFactory.Block(statementsToExtract));

        // Replace selected statements with method call
        var methodCall = SyntaxFactory.ExpressionStatement(
            SyntaxFactory.InvocationExpression(
                SyntaxFactory.IdentifierName(methodName)));

        var newRoot = syntaxRoot;
        foreach (var statement in statementsToExtract.Skip(1))
        {
            newRoot = newRoot?.RemoveNode(statement, SyntaxRemoveOptions.KeepNoTrivia);
        }
        if (statementsToExtract.Any() && newRoot != null)
        {
            newRoot = newRoot.ReplaceNode(statementsToExtract.First(), methodCall);
        }

        // Add the new method to the class
        var containingClass = containingMethod.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (containingClass != null)
        {
            var updatedClass = containingClass.AddMembers(newMethod);
            newRoot = newRoot!.ReplaceNode(containingClass, updatedClass);
        }

        var formattedRoot = Formatter.Format(newRoot!, document.Project.Solution.Workspace);
        var newDocument = document.WithSyntaxRoot(formattedRoot);
        
        // Write the changes back to the file
        var newText = await newDocument.GetTextAsync();
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString());

        return $"Successfully extracted method '{methodName}' from {selectionRange} in {document.FilePath} (solution mode)";
    }

    private static async Task<string> ExtractMethodSingleFile(string filePath, string selectionRange, string methodName)
    {
        if (!File.Exists(filePath))
            return $"Error: File {filePath} not found";

        var sourceText = await File.ReadAllTextAsync(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = await syntaxTree.GetRootAsync();
        var textLines = SourceText.From(sourceText).Lines;
        
        if (!TryParseRange(selectionRange, out var startLine, out var startColumn, out var endLine, out var endColumn))
            return "Error: Invalid selection range format. Use 'startLine:startColumn-endLine:endColumn'";

        var startPosition = textLines[startLine - 1].Start + startColumn - 1;
        var endPosition = textLines[endLine - 1].Start + endColumn - 1;
        var span = TextSpan.FromBounds(startPosition, endPosition);

        var selectedNodes = syntaxRoot.DescendantNodes()
            .Where(n => span.Contains(n.Span))
            .ToList();

        if (!selectedNodes.Any())
            return "Error: No valid code selected";

        var statementsToExtract = selectedNodes
            .OfType<StatementSyntax>()
            .Where(s => span.IntersectsWith(s.Span))
            .ToList();

        if (!statementsToExtract.Any())
            return "Error: Selected code does not contain extractable statements";

        // Find the containing method
        var containingMethod = selectedNodes.First().Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (containingMethod == null)
            return "Error: Selected code is not within a method";

        // Create the new method
        var newMethod = SyntaxFactory.MethodDeclaration(
            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
            methodName)
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword)))
            .WithBody(SyntaxFactory.Block(statementsToExtract));

        // Replace selected statements with method call
        var methodCall = SyntaxFactory.ExpressionStatement(
            SyntaxFactory.InvocationExpression(
                SyntaxFactory.IdentifierName(methodName)));

        var newRoot = syntaxRoot;
        foreach (var statement in statementsToExtract.Skip(1))
        {
            newRoot = newRoot.RemoveNode(statement, SyntaxRemoveOptions.KeepNoTrivia);
        }
        if (statementsToExtract.Any())
        {
            newRoot = newRoot.ReplaceNode(statementsToExtract.First(), methodCall);
        }

        // Add the new method to the class
        var containingClass = containingMethod.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (containingClass != null)
        {
            var updatedClass = containingClass.AddMembers(newMethod);
            newRoot = newRoot.ReplaceNode(containingClass, updatedClass);
        }

        // Format and write back to file
        var workspace = new AdhocWorkspace();
        var formattedRoot = Formatter.Format(newRoot, workspace);
        await File.WriteAllTextAsync(filePath, formattedRoot.ToFullString());

        return $"Successfully extracted method '{methodName}' from {selectionRange} in {filePath} (single file mode)";
    }

    [McpServerTool, Description("Create a new field from selected code")]
    public static async Task<string> IntroduceField(
        [Description("Path to the C# file")] string filePath,
        [Description("Range in format 'startLine:startColumn-endLine:endColumn'")] string selectionRange,
        [Description("Name for the new field")] string fieldName,
        [Description("Access modifier (private, public, protected, internal)")] string accessModifier = "private",
        [Description("Path to the solution file (.sln) - optional for single file mode")] string? solutionPath = null)
    {
        try
        {
            if (solutionPath != null)
            {
                // Solution mode - full semantic analysis
                var solution = await GetOrLoadSolution(solutionPath);
                var document = GetDocumentByPath(solution, filePath);
                if (document != null)
                    return await IntroduceFieldWithSolution(document, selectionRange, fieldName, accessModifier);

                // Fallback to single file mode when file isn't part of the solution
                return await IntroduceFieldSingleFile(filePath, selectionRange, fieldName, accessModifier);
            }

            // Single file mode - direct syntax tree manipulation
            return await IntroduceFieldSingleFile(filePath, selectionRange, fieldName, accessModifier);
        }
        catch (Exception ex)
        {
            return $"Error introducing field: {ex.Message}";
        }
    }

    private static async Task<string> IntroduceFieldWithSolution(Document document, string selectionRange, string fieldName, string accessModifier)
    {
        var sourceText = await document.GetTextAsync();
        var syntaxRoot = await document.GetSyntaxRootAsync();
        
        if (!TryParseRange(selectionRange, out var startLine, out var startColumn, out var endLine, out var endColumn))
            return "Error: Invalid selection range format";

        var startPosition = sourceText.Lines[startLine - 1].Start + startColumn - 1;
        var endPosition = sourceText.Lines[endLine - 1].Start + endColumn - 1;
        var span = TextSpan.FromBounds(startPosition, endPosition);

        var selectedExpression = syntaxRoot!.DescendantNodes()
            .OfType<ExpressionSyntax>()
            .FirstOrDefault(e => span.Contains(e.Span) || e.Span.Contains(span));

        if (selectedExpression == null)
            return "Error: Selected code is not a valid expression";

        // Get the semantic model to determine the type
        var semanticModel = await document.GetSemanticModelAsync();
        var typeInfo = semanticModel!.GetTypeInfo(selectedExpression);
        var typeName = typeInfo.Type?.ToDisplayString() ?? "var";

        // Create the field declaration
        var accessModifierToken = accessModifier.ToLower() switch
        {
            "public" => SyntaxFactory.Token(SyntaxKind.PublicKeyword),
            "protected" => SyntaxFactory.Token(SyntaxKind.ProtectedKeyword),
            "internal" => SyntaxFactory.Token(SyntaxKind.InternalKeyword),
            _ => SyntaxFactory.Token(SyntaxKind.PrivateKeyword)
        };

        var fieldDeclaration = SyntaxFactory.FieldDeclaration(
            SyntaxFactory.VariableDeclaration(
                SyntaxFactory.IdentifierName(typeName))
            .WithVariables(SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.VariableDeclarator(fieldName)
                .WithInitializer(SyntaxFactory.EqualsValueClause(selectedExpression)))))
            .WithModifiers(SyntaxFactory.TokenList(accessModifierToken));

        // Replace the selected expression with the field reference
        var fieldReference = SyntaxFactory.IdentifierName(fieldName);
        var newRoot = syntaxRoot.ReplaceNode(selectedExpression, fieldReference);

        // Add the field to the class
        var containingClass = selectedExpression.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (containingClass != null)
        {
            var updatedClass = containingClass.WithMembers(
                containingClass.Members.Insert(0, fieldDeclaration));
            newRoot = newRoot.ReplaceNode(containingClass, updatedClass);
        }

        var formattedRoot = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        var newDocument = document.WithSyntaxRoot(formattedRoot);
        var newText = await newDocument.GetTextAsync();
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString());

        return $"Successfully introduced {accessModifier} field '{fieldName}' from {selectionRange} in {document.FilePath} (solution mode)";
    }

    private static async Task<string> IntroduceFieldSingleFile(string filePath, string selectionRange, string fieldName, string accessModifier)
    {
        if (!File.Exists(filePath))
            return $"Error: File {filePath} not found";

        var sourceText = await File.ReadAllTextAsync(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = await syntaxTree.GetRootAsync();
        var textLines = SourceText.From(sourceText).Lines;
        
        if (!TryParseRange(selectionRange, out var startLine, out var startColumn, out var endLine, out var endColumn))
            return "Error: Invalid selection range format";

        var startPosition = textLines[startLine - 1].Start + startColumn - 1;
        var endPosition = textLines[endLine - 1].Start + endColumn - 1;
        var span = TextSpan.FromBounds(startPosition, endPosition);

        var selectedExpression = syntaxRoot.DescendantNodes()
            .OfType<ExpressionSyntax>()
            .FirstOrDefault(e => span.Contains(e.Span) || e.Span.Contains(span));

        if (selectedExpression == null)
            return "Error: Selected code is not a valid expression";

        // In single file mode, use 'var' for type since we don't have semantic analysis
        var typeName = "var";

        // Create the field declaration
        var accessModifierToken = accessModifier.ToLower() switch
        {
            "public" => SyntaxFactory.Token(SyntaxKind.PublicKeyword),
            "protected" => SyntaxFactory.Token(SyntaxKind.ProtectedKeyword),
            "internal" => SyntaxFactory.Token(SyntaxKind.InternalKeyword),
            _ => SyntaxFactory.Token(SyntaxKind.PrivateKeyword)
        };

        var fieldDeclaration = SyntaxFactory.FieldDeclaration(
            SyntaxFactory.VariableDeclaration(
                SyntaxFactory.IdentifierName(typeName))
            .WithVariables(SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.VariableDeclarator(fieldName)
                .WithInitializer(SyntaxFactory.EqualsValueClause(selectedExpression)))))
            .WithModifiers(SyntaxFactory.TokenList(accessModifierToken));

        // Replace the selected expression with the field reference
        var fieldReference = SyntaxFactory.IdentifierName(fieldName);
        var newRoot = syntaxRoot.ReplaceNode(selectedExpression, fieldReference);

        // Add the field to the class
        var containingClass = selectedExpression.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (containingClass != null)
        {
            var updatedClass = containingClass.WithMembers(
                containingClass.Members.Insert(0, fieldDeclaration));
            newRoot = newRoot.ReplaceNode(containingClass, updatedClass);
        }

        // Format and write back to file
        var workspace = new AdhocWorkspace();
        var formattedRoot = Formatter.Format(newRoot, workspace);
        await File.WriteAllTextAsync(filePath, formattedRoot.ToFullString());

        return $"Successfully introduced {accessModifier} field '{fieldName}' from {selectionRange} in {filePath} (single file mode)";
    }

    [McpServerTool, Description("Create a new variable from selected code")]
    public static async Task<string> IntroduceVariable(
        [Description("Path to the C# file")] string filePath,
        [Description("Range in format 'startLine:startColumn-endLine:endColumn'")] string selectionRange,
        [Description("Name for the new variable")] string variableName,
        [Description("Path to the solution file (.sln) - optional for single file mode")] string? solutionPath = null)
    {
        try
        {
            if (solutionPath != null)
            {
                // Solution mode - full semantic analysis
                var solution = await GetOrLoadSolution(solutionPath);
                var document = GetDocumentByPath(solution, filePath);
                if (document != null)
                    return await IntroduceVariableWithSolution(document, selectionRange, variableName);

                // Fallback to single file mode when file isn't part of the solution
                return await IntroduceVariableSingleFile(filePath, selectionRange, variableName);
            }

            // Single file mode - direct syntax tree manipulation
            return await IntroduceVariableSingleFile(filePath, selectionRange, variableName);
        }
        catch (Exception ex)
        {
            return $"Error introducing variable: {ex.Message}";
        }
    }

    private static async Task<string> IntroduceVariableWithSolution(Document document, string selectionRange, string variableName)
    {
        var sourceText = await document.GetTextAsync();
        var syntaxRoot = await document.GetSyntaxRootAsync();
        
        if (!TryParseRange(selectionRange, out var startLine, out var startColumn, out var endLine, out var endColumn))
            return "Error: Invalid selection range format";

        var startPosition = sourceText.Lines[startLine - 1].Start + startColumn - 1;
        var endPosition = sourceText.Lines[endLine - 1].Start + endColumn - 1;
        var span = TextSpan.FromBounds(startPosition, endPosition);

        var selectedExpression = syntaxRoot!.DescendantNodes()
            .OfType<ExpressionSyntax>()
            .FirstOrDefault(e => span.Contains(e.Span) || e.Span.Contains(span));

        if (selectedExpression == null)
            return "Error: Selected code is not a valid expression";

        // Get the semantic model to determine the type
        var semanticModel = await document.GetSemanticModelAsync();
        var typeInfo = semanticModel!.GetTypeInfo(selectedExpression);
        var typeName = typeInfo.Type?.ToDisplayString() ?? "var";

        // Create the variable declaration
        var variableDeclaration = SyntaxFactory.LocalDeclarationStatement(
            SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName(typeName))
            .WithVariables(SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.VariableDeclarator(variableName)
                .WithInitializer(SyntaxFactory.EqualsValueClause(selectedExpression)))));

        // Replace the selected expression with the variable reference
        var variableReference = SyntaxFactory.IdentifierName(variableName);
        var newRoot = syntaxRoot.ReplaceNode(selectedExpression, variableReference);

        // Find the containing statement to insert the variable declaration before it
        var containingStatement = selectedExpression.Ancestors().OfType<StatementSyntax>().FirstOrDefault();
        if (containingStatement != null)
        {
            var containingBlock = containingStatement.Parent as BlockSyntax;
            if (containingBlock != null)
            {
                var statementIndex = containingBlock.Statements.IndexOf(containingStatement);
                var newStatements = containingBlock.Statements.Insert(statementIndex, variableDeclaration);
                var newBlock = containingBlock.WithStatements(newStatements);
                newRoot = newRoot.ReplaceNode(containingBlock, newBlock);
            }
        }

        var formattedRoot = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        var newDocument = document.WithSyntaxRoot(formattedRoot);
        var newText = await newDocument.GetTextAsync();
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString());

        return $"Successfully introduced variable '{variableName}' from {selectionRange} in {document.FilePath} (solution mode)";
    }

    private static async Task<string> IntroduceVariableSingleFile(string filePath, string selectionRange, string variableName)
    {
        if (!File.Exists(filePath))
            return $"Error: File {filePath} not found";

        var sourceText = await File.ReadAllTextAsync(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = await syntaxTree.GetRootAsync();
        var textLines = SourceText.From(sourceText).Lines;
        
        if (!TryParseRange(selectionRange, out var startLine, out var startColumn, out var endLine, out var endColumn))
            return "Error: Invalid selection range format";

        var startPosition = textLines[startLine - 1].Start + startColumn - 1;
        var endPosition = textLines[endLine - 1].Start + endColumn - 1;
        var span = TextSpan.FromBounds(startPosition, endPosition);

        var selectedExpression = syntaxRoot.DescendantNodes()
            .OfType<ExpressionSyntax>()
            .FirstOrDefault(e => span.Contains(e.Span) || e.Span.Contains(span));

        if (selectedExpression == null)
            return "Error: Selected code is not a valid expression";

        // In single file mode, use 'var' for type since we don't have semantic analysis
        var typeName = "var";

        // Create the variable declaration
        var variableDeclaration = SyntaxFactory.LocalDeclarationStatement(
            SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName(typeName))
            .WithVariables(SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.VariableDeclarator(variableName)
                .WithInitializer(SyntaxFactory.EqualsValueClause(selectedExpression)))));

        // Replace the selected expression with the variable reference
        var variableReference = SyntaxFactory.IdentifierName(variableName);
        var newRoot = syntaxRoot.ReplaceNode(selectedExpression, variableReference);

        // Find the containing statement to insert the variable declaration before it
        var containingStatement = selectedExpression.Ancestors().OfType<StatementSyntax>().FirstOrDefault();
        if (containingStatement != null)
        {
            var containingBlock = containingStatement.Parent as BlockSyntax;
            if (containingBlock != null)
            {
                var statementIndex = containingBlock.Statements.IndexOf(containingStatement);
                var newStatements = containingBlock.Statements.Insert(statementIndex, variableDeclaration);
                var newBlock = containingBlock.WithStatements(newStatements);
                newRoot = newRoot.ReplaceNode(containingBlock, newBlock);
            }
        }

        // Format and write back to file
        var workspace = new AdhocWorkspace();
        var formattedRoot = Formatter.Format(newRoot, workspace);
        await File.WriteAllTextAsync(filePath, formattedRoot.ToFullString());

        return $"Successfully introduced variable '{variableName}' from {selectionRange} in {filePath} (single file mode)";
    }

    [McpServerTool, Description("Make a field readonly and move initialization to constructors")]
    public static async Task<string> MakeFieldReadonly(
        [Description("Path to the C# file")] string filePath,
        [Description("Line number of the field to make readonly")] int fieldLine,
        [Description("Path to the solution file (.sln) - optional for single file mode")] string? solutionPath = null)
    {
        try
        {
            if (solutionPath != null)
            {
                // Solution mode - full semantic analysis
                var solution = await GetOrLoadSolution(solutionPath);
                var document = GetDocumentByPath(solution, filePath);
                if (document != null)
                    return await MakeFieldReadonlyWithSolution(document, fieldLine);

                // Fallback to single file mode when file isn't part of the solution
                return await MakeFieldReadonlySingleFile(filePath, fieldLine);
            }

            // Single file mode - direct syntax tree manipulation
            return await MakeFieldReadonlySingleFile(filePath, fieldLine);
        }
        catch (Exception ex)
        {
            return $"Error making field readonly: {ex.Message}";
        }
    }

    private static async Task<string> MakeFieldReadonlyWithSolution(Document document, int fieldLine)
    {
        var sourceText = await document.GetTextAsync();
        var syntaxRoot = await document.GetSyntaxRootAsync();
        
        var linePosition = sourceText.Lines[fieldLine - 1].Start;
        var fieldDeclaration = syntaxRoot!.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .FirstOrDefault(f => f.Span.Contains(linePosition));

        if (fieldDeclaration == null)
            return $"Error: No field found at line {fieldLine}";

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

                    return $"Successfully made field readonly and moved initialization to constructors at line {fieldLine} in {document.FilePath}";
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

            return $"Successfully made field readonly at line {fieldLine} in {document.FilePath}";
        }

        return $"Field at line {fieldLine} made readonly, but no constructors found for initialization";
    }

    private static async Task<string> MakeFieldReadonlySingleFile(string filePath, int fieldLine)
    {
        if (!File.Exists(filePath))
            return $"Error: File {filePath} not found";

        var sourceText = await File.ReadAllTextAsync(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = await syntaxTree.GetRootAsync();
        var textLines = SourceText.From(sourceText).Lines;
        
        var linePosition = textLines[fieldLine - 1].Start;
        var fieldDeclaration = syntaxRoot.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .FirstOrDefault(f => f.Span.Contains(linePosition));

        if (fieldDeclaration == null)
            return $"Error: No field found at line {fieldLine}";

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

                    // Format and write back to file
                    var workspace = new AdhocWorkspace();
                    var formattedRoot = Formatter.Format(newRoot, workspace);
                    await File.WriteAllTextAsync(filePath, formattedRoot.ToFullString());

                    return $"Successfully made field readonly and moved initialization to constructors at line {fieldLine} in {filePath}";
                }
            }
        }
        else
        {
            var newRoot = syntaxRoot.ReplaceNode(fieldDeclaration, newFieldDeclaration);
            
            // Format and write back to file
            var workspace = new AdhocWorkspace();
            var formattedRoot = Formatter.Format(newRoot, workspace);
            await File.WriteAllTextAsync(filePath, formattedRoot.ToFullString());

            return $"Successfully made field readonly at line {fieldLine} in {filePath} (single file mode)";
        }

        return $"Field at line {fieldLine} made readonly, but no constructors found for initialization";
    }

    private static async Task<string> IntroduceParameterWithSolution(Document document, int methodLine, string selectionRange, string parameterName)
    {
        var sourceText = await document.GetTextAsync();
        var syntaxRoot = await document.GetSyntaxRootAsync();
        var textLines = sourceText.Lines;

        var method = syntaxRoot!.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => textLines.GetLineFromPosition(m.SpanStart).LineNumber + 1 == methodLine);
        if (method == null)
            return $"Error: No method found at line {methodLine}";

        if (!TryParseRange(selectionRange, out var startLine, out var startColumn, out var endLine, out var endColumn))
            return "Error: Invalid selection range format";

        var startPosition = textLines[startLine - 1].Start + startColumn - 1;
        var endPosition = textLines[endLine - 1].Start + endColumn - 1;
        var span = TextSpan.FromBounds(startPosition, endPosition);

        var selectedExpression = syntaxRoot.DescendantNodes(span).OfType<ExpressionSyntax>().FirstOrDefault();
        if (selectedExpression == null)
            return "Error: Selected code is not a valid expression";

        var semanticModel = await document.GetSemanticModelAsync();
        var typeInfo = semanticModel!.GetTypeInfo(selectedExpression);
        var typeName = typeInfo.Type?.ToDisplayString() ?? "object";

        var parameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameterName))
            .WithType(SyntaxFactory.ParseTypeName(typeName));
        var newMethod = method.AddParameterListParameters(parameter);

        var newRoot = syntaxRoot.ReplaceNode(method, newMethod);
        newRoot = newRoot.ReplaceNode(selectedExpression, SyntaxFactory.IdentifierName(parameterName));

        var formattedRoot = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        var newDocument = document.WithSyntaxRoot(formattedRoot);
        var newText = await newDocument.GetTextAsync();
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString());

        return $"Successfully introduced parameter '{parameterName}' from {selectionRange} in method at line {methodLine} in {document.FilePath} (solution mode)";
    }

    private static async Task<string> IntroduceParameterSingleFile(string filePath, int methodLine, string selectionRange, string parameterName)
    {
        if (!File.Exists(filePath))
            return $"Error: File {filePath} not found";

        var sourceText = await File.ReadAllTextAsync(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = await syntaxTree.GetRootAsync();
        var textLines = SourceText.From(sourceText).Lines;

        var method = syntaxRoot.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => textLines.GetLineFromPosition(m.SpanStart).LineNumber + 1 == methodLine);
        if (method == null)
            return $"Error: No method found at line {methodLine}";

        if (!TryParseRange(selectionRange, out var startLine, out var startColumn, out var endLine, out var endColumn))
            return "Error: Invalid selection range format";

        var startPosition = textLines[startLine - 1].Start + startColumn - 1;
        var endPosition = textLines[endLine - 1].Start + endColumn - 1;
        var span = TextSpan.FromBounds(startPosition, endPosition);

        var selectedExpression = syntaxRoot.DescendantNodes()
            .OfType<ExpressionSyntax>()
            .FirstOrDefault(e => span.Contains(e.Span) || e.Span.Contains(span));
        if (selectedExpression == null)
            return "Error: Selected code is not a valid expression";

        var parameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameterName))
            .WithType(SyntaxFactory.ParseTypeName("object"));
        var newMethod = method.AddParameterListParameters(parameter);

        var newRoot = syntaxRoot.ReplaceNode(method, newMethod);
        newRoot = newRoot.ReplaceNode(selectedExpression, SyntaxFactory.IdentifierName(parameterName));

        var workspace = new AdhocWorkspace();
        var formattedRoot = Formatter.Format(newRoot, workspace);
        await File.WriteAllTextAsync(filePath, formattedRoot.ToFullString());

        return $"Successfully introduced parameter '{parameterName}' from {selectionRange} in method at line {methodLine} in {filePath} (single file mode)";
    }

    // Helper methods
    private static async Task<Solution> GetOrLoadSolution(string solutionPath)
    {
        if (_loadedSolutions.TryGetValue(solutionPath, out var cachedSolution))
            return cachedSolution;

        using var workspace = MSBuildWorkspace.Create();
        var solution = await workspace.OpenSolutionAsync(solutionPath);
        _loadedSolutions[solutionPath] = solution;
        return solution;
    }

    private static Document? GetDocumentFromSingleFile(string filePath)
    {
        // For single file mode, we can create a simple document without a full solution
        // This is mainly used for consistency in the API, but we'll use direct file operations instead
        return null;
    }

    private static Document? GetDocumentByPath(Solution solution, string filePath)
    {
        var normalizedPath = Path.GetFullPath(filePath);
        return solution.Projects
            .SelectMany(p => p.Documents)
            .FirstOrDefault(d => Path.GetFullPath(d.FilePath ?? "") == normalizedPath);
    }

    private static bool TryParseRange(string range, out int startLine, out int startColumn, out int endLine, out int endColumn)
    {
        startLine = startColumn = endLine = endColumn = 0;
        
        // Expected format: "startLine:startColumn-endLine:endColumn"
        var parts = range.Split('-');
        if (parts.Length != 2) return false;

        var startParts = parts[0].Split(':');
        var endParts = parts[1].Split(':');
        
        if (startParts.Length != 2 || endParts.Length != 2) return false;

        return int.TryParse(startParts[0], out startLine) &&
               int.TryParse(startParts[1], out startColumn) &&
               int.TryParse(endParts[0], out endLine) &&
               int.TryParse(endParts[1], out endColumn);
    }

    // Placeholder implementations for remaining tools
    [McpServerTool, Description("Create a new parameter from selected code")]
    public static async Task<string> IntroduceParameter(
        [Description("Path to the C# file")] string filePath,
        [Description("Line number of the method to add parameter to")] int methodLine,
        [Description("Range in format 'startLine:startColumn-endLine:endColumn'")] string selectionRange,
        [Description("Name for the new parameter")] string parameterName,
        [Description("Path to the solution file (.sln) - optional for single file mode")] string? solutionPath = null)
    {
        try
        {
            if (solutionPath != null)
            {
                var solution = await GetOrLoadSolution(solutionPath);
                var document = GetDocumentByPath(solution, filePath);
                if (document == null)
                    return $"Error: File {filePath} not found in solution";

                return await IntroduceParameterWithSolution(document, methodLine, selectionRange, parameterName);
            }
            else
            {
                return await IntroduceParameterSingleFile(filePath, methodLine, selectionRange, parameterName);
            }
        }
        catch (Exception ex)
        {
            return $"Error introducing parameter: {ex.Message}";
        }
    }

    [McpServerTool, Description("Transform instance method to static by converting dependencies to parameters")]
    public static async Task<string> ConvertToStaticWithParameters(
        [Description("Path to the C# file")] string filePath,
        [Description("Line number of the method to convert")] int methodLine,
        [Description("Path to the solution file (.sln) - optional for single file mode")] string? solutionPath = null)
    {
        try
        {
            if (solutionPath != null)
            {
                var solution = await GetOrLoadSolution(solutionPath);
                var document = GetDocumentByPath(solution, filePath);
                if (document == null)
                    return $"Error: File {filePath} not found in solution"; 

                return await ConvertToStaticWithParametersWithSolution(document, methodLine);
            }
            else
            {
                return await ConvertToStaticWithParametersSingleFile(filePath, methodLine);
            }
        }
        catch (Exception ex)
        {
            return $"Error converting method to static: {ex.Message}";
        }
    }

    [McpServerTool, Description("Transform instance method to static by adding instance parameter")]
    public static async Task<string> ConvertToStaticWithInstance(
        [Description("Path to the C# file")] string filePath,
        [Description("Line number of the method to convert")] int methodLine,
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
                    return await ConvertToStaticWithInstanceWithSolution(document, methodLine, instanceParameterName);

                // Fallback to single file mode when file isn't part of the solution
                return await ConvertToStaticWithInstanceSingleFile(filePath, methodLine, instanceParameterName);
            }

            return await ConvertToStaticWithInstanceSingleFile(filePath, methodLine, instanceParameterName);
        }
        catch (Exception ex)
        {
            return $"Error converting method to static: {ex.Message}";
        }
    }

    private static async Task<string> ConvertToStaticWithParametersWithSolution(Document document, int methodLine)
    {
        var sourceText = await document.GetTextAsync();
        var syntaxRoot = await document.GetSyntaxRootAsync();
        var textLines = sourceText.Lines;

        var method = syntaxRoot!.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => textLines.GetLineFromPosition(m.SpanStart).LineNumber + 1 == methodLine);
        if (method == null)
            return $"Error: No method found at line {methodLine}";

        var semanticModel = await document.GetSemanticModelAsync();
        var typeDecl = method.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        if (typeDecl == null)
            return $"Error: Method at line {methodLine} is not inside a type";

        var typeSymbol = semanticModel!.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
        if (typeSymbol == null)
            return $"Error: Unable to determine containing type";

        var parameterList = method.ParameterList;
        var paramMap = new Dictionary<ISymbol, string>();

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

        return $"Successfully converted method to static with parameters at line {methodLine} in {document.FilePath} (solution mode)";
    }

    private static async Task<string> ConvertToStaticWithParametersSingleFile(string filePath, int methodLine)
    {
        if (!File.Exists(filePath))
            return $"Error: File {filePath} not found";

        var sourceText = await File.ReadAllTextAsync(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = await syntaxTree.GetRootAsync();
        var textLines = SourceText.From(sourceText).Lines;

        var method = syntaxRoot.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => textLines.GetLineFromPosition(m.SpanStart).LineNumber + 1 == methodLine);
        if (method == null)
            return $"Error: No method found at line {methodLine}";

        var classDecl = method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (classDecl == null)
            return $"Error: Method at line {methodLine} is not inside a class";

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
        var workspace = new AdhocWorkspace();
        var formattedRoot = Formatter.Format(newRoot, workspace);
        await File.WriteAllTextAsync(filePath, formattedRoot.ToFullString());

        return $"Successfully converted method to static with parameters at line {methodLine} in {filePath} (single file mode)";
    }

    private static async Task<string> ConvertToStaticWithInstanceWithSolution(Document document, int methodLine, string instanceParameterName)
    {
        var sourceText = await document.GetTextAsync();
        var syntaxRoot = await document.GetSyntaxRootAsync();
        var textLines = sourceText.Lines;

        var method = syntaxRoot!.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => textLines.GetLineFromPosition(m.SpanStart).LineNumber + 1 == methodLine);
        if (method == null)
            return $"Error: No method found at line {methodLine}";

        var semanticModel = await document.GetSemanticModelAsync();
        var typeDecl = method.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        if (typeDecl == null)
            return $"Error: Method at line {methodLine} is not inside a type";

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

        return $"Successfully converted method to static with instance parameter at line {methodLine} in {document.FilePath} (solution mode)";
    }

    private static async Task<string> ConvertToStaticWithInstanceSingleFile(string filePath, int methodLine, string instanceParameterName)
    {
        if (!File.Exists(filePath))
            return $"Error: File {filePath} not found";

        var sourceText = await File.ReadAllTextAsync(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = await syntaxTree.GetRootAsync();
        var textLines = SourceText.From(sourceText).Lines;

        var method = syntaxRoot.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => textLines.GetLineFromPosition(m.SpanStart).LineNumber + 1 == methodLine);
        if (method == null)
            return $"Error: No method found at line {methodLine}";

        var classDecl = method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (classDecl == null)
            return $"Error: Method at line {methodLine} is not inside a class";

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
        var workspace = new AdhocWorkspace();
        var formatted = Formatter.Format(newRoot, workspace);
        await File.WriteAllTextAsync(filePath, formatted.ToFullString());

        return $"Successfully converted method to static with instance parameter at line {methodLine} in {filePath} (single file mode)";
    }

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

    private static async Task<string> MoveInstanceMethodSingleFile(string filePath, int methodLine, string targetClass, string accessMemberName, string accessMemberType)
    {
        if (!File.Exists(filePath))
            return $"Error: File {filePath} not found";

        var sourceText = await File.ReadAllTextAsync(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = await syntaxTree.GetRootAsync();
        var textLines = SourceText.From(sourceText).Lines;

        var method = syntaxRoot.DescendantNodes()
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
        var workspace = new AdhocWorkspace();
        var formatted = Formatter.Format(newRoot, workspace);
        await File.WriteAllTextAsync(filePath, formatted.ToFullString());

        return $"Successfully moved instance method to {targetClass} in {filePath} (single file mode)";
    }

    [McpServerTool, Description("Move a static method to another class")]
    public static async Task<string> MoveStaticMethod(
        [Description("Path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file containing the method")] string filePath,
        [Description("Line number of the static method to move")] int methodLine,
        [Description("Name of the target class")] string targetClass,
        [Description("Path to the target file (optional, will create if doesn't exist)")] string? targetFilePath = null)
    {
        // TODO: Implement move static method refactoring using Roslyn
        return $"Move static method at line {methodLine} from {filePath} to class '{targetClass}' - Implementation in progress";
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
        try
        {
            if (solutionPath != null)
            {
                var solution = await GetOrLoadSolution(solutionPath);
                var document = GetDocumentByPath(solution, filePath);
                if (document != null)
                    return await MoveInstanceMethodWithSolution(document, methodLine, targetClass, accessMemberName, accessMemberType);

                // Fallback to single file mode when file isn't part of the solution
                return await MoveInstanceMethodSingleFile(filePath, methodLine, targetClass, accessMemberName, accessMemberType);
            }

            return await MoveInstanceMethodSingleFile(filePath, methodLine, targetClass, accessMemberName, accessMemberType);
        }
        catch (Exception ex)
        {
            return $"Error moving instance method: {ex.Message}";
        }
    }

    [McpServerTool, Description("Convert property setter to init-only setter")]
    public static async Task<string> TransformSetterToInit(
        [Description("Path to the C# file")] string filePath,
        [Description("Line number of the property to transform")] int propertyLine,
        [Description("Path to the solution file (.sln) - optional for single file mode")] string? solutionPath = null)
    {
        try
        {
            if (solutionPath != null)
            {
                var solution = await GetOrLoadSolution(solutionPath);
                var document = GetDocumentByPath(solution, filePath);
                if (document == null)
                    return $"Error: File {filePath} not found in solution";

                return await TransformSetterToInitWithSolution(document, propertyLine);
            }
            else
            {
                return await TransformSetterToInitSingleFile(filePath, propertyLine);
            }
        }
        catch (Exception ex)
        {
            return $"Error transforming setter: {ex.Message}";
        }
    }

    private static async Task<string> TransformSetterToInitWithSolution(Document document, int propertyLine)
    {
        var sourceText = await document.GetTextAsync();
        var syntaxRoot = await document.GetSyntaxRootAsync();
        var linePos = sourceText.Lines[propertyLine - 1].Start;

        var property = syntaxRoot!.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .FirstOrDefault(p => p.Span.Contains(linePos));
        if (property == null)
            return $"Error: No property found at line {propertyLine}";

        var setter = property.AccessorList?.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.SetAccessorDeclaration));
        if (setter == null)
            return $"Error: Property at line {propertyLine} has no setter";

        var initAccessor = SyntaxFactory.AccessorDeclaration(SyntaxKind.InitAccessorDeclaration)
            .WithSemicolonToken(setter.SemicolonToken);
        var newProperty = property.ReplaceNode(setter, initAccessor);

        var newRoot = syntaxRoot.ReplaceNode(property, newProperty);
        var formatted = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        var newDocument = document.WithSyntaxRoot(formatted);
        var newText = await newDocument.GetTextAsync();
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString());

        return $"Successfully converted setter to init at line {propertyLine} in {document.FilePath} (solution mode)";
    }

    private static async Task<string> TransformSetterToInitSingleFile(string filePath, int propertyLine)
    {
        if (!File.Exists(filePath))
            return $"Error: File {filePath} not found";

        var sourceText = await File.ReadAllTextAsync(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = await syntaxTree.GetRootAsync();
        var textLines = SourceText.From(sourceText).Lines;
        var linePos = textLines[propertyLine - 1].Start;

        var property = syntaxRoot.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .FirstOrDefault(p => p.Span.Contains(linePos));
        if (property == null)
            return $"Error: No property found at line {propertyLine}";

        var setter = property.AccessorList?.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.SetAccessorDeclaration));
        if (setter == null)
            return $"Error: Property at line {propertyLine} has no setter";

        var initAccessor = SyntaxFactory.AccessorDeclaration(SyntaxKind.InitAccessorDeclaration)
            .WithSemicolonToken(setter.SemicolonToken);
        var newProperty = property.ReplaceNode(setter, initAccessor);

        var newRoot = syntaxRoot.ReplaceNode(property, newProperty);
        var workspace = new AdhocWorkspace();
        var formatted = Formatter.Format(newRoot, workspace);
        await File.WriteAllTextAsync(filePath, formatted.ToFullString());

        return $"Successfully converted setter to init at line {propertyLine} in {filePath} (single file mode)";
    }

    [McpServerTool, Description("Safely delete a field, parameter, or variable with dependency warnings")]
    public static async Task<string> SafeDelete(
        [Description("Path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line number of the element to delete")] int targetLine,
        [Description("Type of element (field, parameter, variable)")] string elementType)
    {
        // TODO: Implement safe delete refactoring using Roslyn
        return $"Safe delete {elementType} at line {targetLine} in {filePath} - Implementation in progress";
    }
}
