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
[McpServerToolType]
public static partial class RefactoringTools
{
}
