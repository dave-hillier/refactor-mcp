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
if (args.Length > 0 && args[0] == "--cli")
{
    await RunCliMode(args);
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

static async Task RunCliMode(string[] args)
{
    if (args.Length < 2)
    {
        ShowCliHelp();
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
            "safe-delete-field" => await TestSafeDeleteField(args),
            "safe-delete-method" => await TestSafeDeleteMethod(args),
            "safe-delete-parameter" => await TestSafeDeleteParameter(args),
            "safe-delete-variable" => await TestSafeDeleteVariable(args),
            "version" => ShowVersionInfo(),
            "list-tools" => ListAvailableTools(),
            _ => $"Unknown command: {command}. Use --cli list-tools to see available commands."
        };

        Console.WriteLine(result);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        Environment.Exit(1);
    }
}

static void ShowCliHelp()
{
    Console.WriteLine("RefactorMCP CLI Mode");
    Console.WriteLine("Usage: RefactorMCP.ConsoleApp --cli <command> [arguments]");
    Console.WriteLine();
    Console.WriteLine("Available commands:");
    Console.WriteLine("  list-tools                                    - List all available refactoring tools");
    Console.WriteLine("  load-solution <solutionPath>                 - Test loading a solution file (not required)");
    Console.WriteLine("  unload-solution <solutionPath>               - Remove a loaded solution from cache");
    Console.WriteLine("  clear-solution-cache                         - Clear all cached solutions");
    Console.WriteLine("  version                                      - Show version information");
    Console.WriteLine("  extract-method <filePath> <range> <methodName> [solutionPath]");
    Console.WriteLine("  introduce-field <filePath> <range> <fieldName> [accessModifier] [solutionPath]");
    Console.WriteLine("  introduce-variable <filePath> <range> <variableName> [solutionPath]");
    Console.WriteLine("  safe-delete-field <filePath> <fieldName> [solutionPath]");
    Console.WriteLine("  safe-delete-method <filePath> <methodName> [solutionPath]");
    Console.WriteLine("  safe-delete-parameter <filePath> <methodName> <parameterName> [solutionPath]");
    Console.WriteLine("  safe-delete-variable <filePath> <range> [solutionPath]");
    Console.WriteLine("  make-field-readonly <filePath> <fieldName> [solutionPath]");
    Console.WriteLine("  convert-to-extension-method <filePath> <methodName> [solutionPath]");
    Console.WriteLine("  move-instance-method <filePath> <sourceClass> <methodName> <targetClass> <accessMember> [memberType] [solutionPath]");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  --cli load-solution ./MySolution.sln");
    Console.WriteLine("  --cli extract-method ./MyFile.cs \"10:5-15:20\" \"ExtractedMethod\"");
    Console.WriteLine("  --cli extract-method ./MyFile.cs \"10:5-15:20\" \"ExtractedMethod\" ./MySolution.sln");
    Console.WriteLine("  --cli introduce-field ./MyFile.cs \"12:10-12:25\" \"_myField\" \"private\"");
    Console.WriteLine("  --cli make-field-readonly ./MyFile.cs 15");
    Console.WriteLine("  --cli version");
    Console.WriteLine();
    Console.WriteLine("Range format: \"startLine:startColumn-endLine:endColumn\" (1-based)");
    Console.WriteLine("Note: Solution path is optional. When omitted, single file mode is used with limited semantic analysis.");
}

static string ListAvailableTools()
{
    var tools = new[]
    {
        "load-solution - Load a solution file for refactoring operations (not required)",
        "extract-method - Extract selected code into a new method",
        "introduce-field - Create a new field from selected code",
        "introduce-variable - Create a new variable from selected code",
        "make-field-readonly - Make a field readonly and move initialization to constructors",
        "convert-to-extension-method - Convert an instance method to an extension method",
        "introduce-parameter - Create a new parameter from selected code",
        "convert-to-static-with-parameters - Transform instance method to static",
        "convert-to-static-with-instance - Transform instance method to static with instance parameter",
        "move-static-method - Move a static method to another class",
        "move-instance-method - Move an instance method to another class",
        "transform-setter-to-init - Convert property setter to init-only setter",
        "safe-delete-field - Safely delete an unused field",
        "safe-delete-method - Safely delete an unused method",
        "safe-delete-parameter - Safely delete an unused parameter",
        "safe-delete-variable - Safely delete a local variable"
    };

    return "Available refactoring tools:\n" + string.Join("\n", tools);
}

static async Task<string> TestLoadSolution(string[] args)
{
    if (args.Length < 3)
        return "Error: Missing solution path. Usage: --cli load-solution <solutionPath>";

    var solutionPath = args[2];
    return await RefactoringTools.LoadSolution(solutionPath);
}

static async Task<string> TestExtractMethod(string[] args)
{
    if (args.Length < 5)
        return "Error: Missing arguments. Usage: --cli extract-method <filePath> <range> <methodName> [solutionPath]";

    var filePath = args[2];
    var range = args[3];
    var methodName = args[4];
    var solutionPath = args.Length > 5 ? args[5] : null;

    return await RefactoringTools.ExtractMethod(filePath, range, methodName, solutionPath);
}

static async Task<string> TestIntroduceField(string[] args)
{
    if (args.Length < 5)
        return "Error: Missing arguments. Usage: --cli introduce-field <filePath> <range> <fieldName> [accessModifier] [solutionPath]";

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
        return "Error: Missing arguments. Usage: --cli introduce-variable <filePath> <range> <variableName> [solutionPath]";

    var filePath = args[2];
    var range = args[3];
    var variableName = args[4];
    var solutionPath = args.Length > 5 ? args[5] : null;

    return await RefactoringTools.IntroduceVariable(filePath, range, variableName, solutionPath);
}

static async Task<string> TestMakeFieldReadonly(string[] args)
{
    if (args.Length < 4)
        return "Error: Missing arguments. Usage: --cli make-field-readonly <filePath> <fieldName> [solutionPath]";

    var filePath = args[2];
    var fieldName = args[3];
    var solutionPath = args.Length > 4 ? args[4] : null;

    return await RefactoringTools.MakeFieldReadonly(filePath, fieldName, solutionPath);
}

static string TestUnloadSolution(string[] args)
{
    if (args.Length < 3)
        return "Error: Missing solution path. Usage: --cli unload-solution <solutionPath>";

    var solutionPath = args[2];
    return RefactoringTools.UnloadSolution(solutionPath);
}

static string ClearCacheCommand()
{
    return RefactoringTools.ClearSolutionCache();
}

static string ShowVersionInfo()
{
    return RefactoringTools.Version();
}

static async Task<string> TestConvertToExtensionMethod(string[] args)
{
    if (args.Length < 4)
        return "Error: Missing arguments. Usage: --cli convert-to-extension-method <filePath> <methodName> [solutionPath]";

    var filePath = args[2];
    var methodName = args[3];
    var solutionPath = args.Length > 4 ? args[4] : null;

    return await RefactoringTools.ConvertToExtensionMethod(filePath, methodName, null, solutionPath);
}

static async Task<string> TestSafeDeleteField(string[] args)
{
    if (args.Length < 4)
        return "Error: Missing arguments. Usage: --cli safe-delete-field <filePath> <fieldName> [solutionPath]";

    var filePath = args[2];
    var fieldName = args[3];
    var solutionPath = args.Length > 4 ? args[4] : null;

    return await RefactoringTools.SafeDeleteField(filePath, fieldName, solutionPath);
}

static async Task<string> TestSafeDeleteMethod(string[] args)
{
    if (args.Length < 4)
        return "Error: Missing arguments. Usage: --cli safe-delete-method <filePath> <methodName> [solutionPath]";

    var filePath = args[2];
    var methodName = args[3];
    var solutionPath = args.Length > 4 ? args[4] : null;

    return await RefactoringTools.SafeDeleteMethod(filePath, methodName, solutionPath);
}

static async Task<string> TestSafeDeleteParameter(string[] args)
{
    if (args.Length < 5)
        return "Error: Missing arguments. Usage: --cli safe-delete-parameter <filePath> <methodName> <parameterName> [solutionPath]";

    var filePath = args[2];
    var methodName = args[3];
    var parameterName = args[4];
    var solutionPath = args.Length > 5 ? args[5] : null;

    return await RefactoringTools.SafeDeleteParameter(filePath, methodName, parameterName, solutionPath);
}

static async Task<string> TestSafeDeleteVariable(string[] args)
{
    if (args.Length < 4)
        return "Error: Missing arguments. Usage: --cli safe-delete-variable <filePath> <range> [solutionPath]";

    var filePath = args[2];
    var range = args[3];
    var solutionPath = args.Length > 4 ? args[4] : null;

    return await RefactoringTools.SafeDeleteVariable(filePath, range, solutionPath);
}

[McpServerToolType]
public static partial class RefactoringTools
{
}
