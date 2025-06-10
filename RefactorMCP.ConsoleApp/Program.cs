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
            "unload-solution" => TestUnloadSolution(args),
            "clear-solution-cache" => ClearCacheCommand(),
            "convert-to-extension-method" => await TestConvertToExtensionMethod(args),
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
    Console.WriteLine("  unload-solution <solutionPath>               - Remove a loaded solution from cache");
    Console.WriteLine("  clear-solution-cache                         - Clear all cached solutions");
    Console.WriteLine("  extract-method <filePath> <range> <methodName> [solutionPath]");
    Console.WriteLine("  introduce-field <filePath> <range> <fieldName> [accessModifier] [solutionPath]");
    Console.WriteLine("  introduce-variable <filePath> <range> <variableName> [solutionPath]");
    Console.WriteLine("  make-field-readonly <filePath> <fieldLine> [solutionPath]");
    Console.WriteLine("  convert-to-extension-method <filePath> <methodLine> [solutionPath]");
    Console.WriteLine("  move-instance-method <filePath> <sourceClass> <methodName> <targetClass> <accessMember> [memberType] [solutionPath]");
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
        "convert-to-extension-method - Convert an instance method to an extension method",
        "introduce-parameter - Create a new parameter from selected code (TODO)",
        "convert-to-static-with-parameters - Transform instance method to static (TODO)",
        "convert-to-static-with-instance - Transform instance method to static with instance parameter (TODO)",
        "move-static-method - Move a static method to another class (TODO)",
        "move-instance-method - Move an instance method to another class",
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

static string TestUnloadSolution(string[] args)
{
    if (args.Length < 3)
        return "Error: Missing solution path. Usage: --test unload-solution <solutionPath>";

    var solutionPath = args[2];
    return RefactoringTools.UnloadSolution(solutionPath);
}

static string ClearCacheCommand()
{
    return RefactoringTools.ClearSolutionCache();
}

static async Task<string> TestConvertToExtensionMethod(string[] args)
{
    if (args.Length < 4)
        return "Error: Missing arguments. Usage: --test convert-to-extension-method <filePath> <methodLine> [solutionPath]";

    var filePath = args[2];
    if (!int.TryParse(args[3], out var methodLine))
        return "Error: Invalid method line number";
    var solutionPath = args.Length > 4 ? args[4] : null;

    return await RefactoringTools.ConvertToExtensionMethod(filePath, methodLine, null, solutionPath);
}

[McpServerToolType]
public static partial class RefactoringTools
{
    [McpServerTool, Description("Convert an instance method to an extension method in a static class")]
    public static async Task<string> ConvertToExtensionMethod(
        [Description("Path to the C# file")] string filePath,
        [Description("Line number of the instance method to convert")] int methodLine,
        [Description("Name of the extension class - optional")] string? extensionClass = null,
        [Description("Path to the solution file (.sln) - optional for single file mode")] string? solutionPath = null)
    {
        try
        {
            if (solutionPath != null)
            {
                var solution = await GetOrLoadSolution(solutionPath);
                var document = GetDocumentByPath(solution, filePath);
                if (document != null)
                    return await ConvertToExtensionMethodWithSolution(document, methodLine, extensionClass);

                return await ConvertToExtensionMethodSingleFile(filePath, methodLine, extensionClass);
            }

            return await ConvertToExtensionMethodSingleFile(filePath, methodLine, extensionClass);
        }
        catch (Exception ex)
        {
            return $"Error converting to extension method: {ex.Message}";
        }
    }

    private static async Task<string> ConvertToExtensionMethodWithSolution(Document document, int methodLine, string? extensionClass)
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
        var classDecl = method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (classDecl == null)
            return $"Error: Method at line {methodLine} is not inside a class";

        var className = classDecl.Identifier.ValueText;
        var extClassName = extensionClass ?? className + "Extensions";
        var paramName = char.ToLower(className[0]) + className.Substring(1);

        var thisParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier(paramName))
            .WithType(SyntaxFactory.ParseTypeName(className))
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.ThisKeyword));

        var updatedMethod = method.WithParameterList(method.ParameterList.AddParameters(thisParam));

        updatedMethod = updatedMethod.ReplaceNodes(
            updatedMethod.DescendantNodes().OfType<ThisExpressionSyntax>(),
            (_, _) => SyntaxFactory.IdentifierName(paramName));

        updatedMethod = updatedMethod.ReplaceNodes(
            updatedMethod.DescendantNodes().OfType<IdentifierNameSyntax>().Where(id =>
            {
                var sym = semanticModel!.GetSymbolInfo(id).Symbol;
                return sym is IFieldSymbol or IPropertySymbol or IMethodSymbol &&
                       SymbolEqualityComparer.Default.Equals(sym.ContainingType, semanticModel.GetDeclaredSymbol(classDecl)) &&
                       !sym.IsStatic && id.Parent is not MemberAccessExpressionSyntax;
            }),
            (old, _) => SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(paramName),
                SyntaxFactory.IdentifierName(old.Identifier)));

        var modifiers = updatedMethod.Modifiers;
        if (!modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
            modifiers = modifiers.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));

        updatedMethod = updatedMethod.WithModifiers(modifiers);

        var newRoot = syntaxRoot.RemoveNode(method, SyntaxRemoveOptions.KeepNoTrivia);

        var extClass = newRoot.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == extClassName);
        if (extClass != null)
        {
            var updatedClass = extClass.AddMembers(updatedMethod);
            newRoot = newRoot.ReplaceNode(extClass, updatedClass);
        }
        else
        {
            var extensionClassDecl = SyntaxFactory.ClassDeclaration(extClassName)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword))
                .AddMembers(updatedMethod);

            if (classDecl.Parent is NamespaceDeclarationSyntax ns)
            {
                var updatedNs = ns.AddMembers(extensionClassDecl);
                newRoot = newRoot.ReplaceNode(ns, updatedNs);
            }
            else
            {
                newRoot = ((CompilationUnitSyntax)newRoot).AddMembers(extensionClassDecl);
            }
        }

        var formatted = Formatter.Format(newRoot, document.Project.Solution.Workspace);
        var newDocument = document.WithSyntaxRoot(formatted);
        var newText = await newDocument.GetTextAsync();
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString());

        return $"Successfully converted method to extension method at line {methodLine} in {document.FilePath} (solution mode)";
    }

    private static async Task<string> ConvertToExtensionMethodSingleFile(string filePath, int methodLine, string? extensionClass)
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

        var className = classDecl.Identifier.ValueText;
        var extClassName = extensionClass ?? className + "Extensions";
        var paramName = char.ToLower(className[0]) + className.Substring(1);

        var thisParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier(paramName))
            .WithType(SyntaxFactory.ParseTypeName(className))
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.ThisKeyword));

        var updatedMethod = method.WithParameterList(method.ParameterList.AddParameters(thisParam));

        updatedMethod = updatedMethod.ReplaceNodes(
            updatedMethod.DescendantNodes().OfType<ThisExpressionSyntax>(),
            (_, _) => SyntaxFactory.IdentifierName(paramName));

        var instanceMembers = classDecl.Members
            .Where(m => m is FieldDeclarationSyntax or PropertyDeclarationSyntax)
            .Select(m => m switch
            {
                FieldDeclarationSyntax f => f.Declaration.Variables.First().Identifier.ValueText,
                PropertyDeclarationSyntax p => p.Identifier.ValueText,
                _ => string.Empty
            })
            .Where(n => !string.IsNullOrEmpty(n))
            .ToHashSet();

        updatedMethod = updatedMethod.ReplaceNodes(
            updatedMethod.DescendantNodes().OfType<IdentifierNameSyntax>().Where(id =>
                instanceMembers.Contains(id.Identifier.ValueText) && id.Parent is not MemberAccessExpressionSyntax),
            (old, _) => SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(paramName),
                SyntaxFactory.IdentifierName(old.Identifier)));

        var modifiers = updatedMethod.Modifiers;
        if (!modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
            modifiers = modifiers.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));

        updatedMethod = updatedMethod.WithModifiers(modifiers);

        var newRoot = syntaxRoot.RemoveNode(method, SyntaxRemoveOptions.KeepNoTrivia);

        var extClass = newRoot.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == extClassName);
        if (extClass != null)
        {
            var updatedClass = extClass.AddMembers(updatedMethod);
            newRoot = newRoot.ReplaceNode(extClass, updatedClass);
        }
        else
        {
            var extensionClassDecl = SyntaxFactory.ClassDeclaration(extClassName)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword))
                .AddMembers(updatedMethod);

            if (classDecl.Parent is NamespaceDeclarationSyntax ns)
            {
                var updatedNs = ns.AddMembers(extensionClassDecl);
                newRoot = newRoot.ReplaceNode(ns, updatedNs);
            }
            else
            {
                newRoot = ((CompilationUnitSyntax)newRoot).AddMembers(extensionClassDecl);
            }
        }

        var workspace = new AdhocWorkspace();
        var formatted = Formatter.Format(newRoot, workspace);
        await File.WriteAllTextAsync(filePath, formatted.ToFullString());

        return $"Successfully converted method to extension method at line {methodLine} in {filePath} (single file mode)";
    }
}
