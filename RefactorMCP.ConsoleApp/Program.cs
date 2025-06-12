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
    .WithToolsFromAssembly()
    .WithPromptsFromAssembly();

await builder.Build().RunAsync();

static async Task RunCliMode(string[] args)
{
    if (args.Length < 2)
    {
        ShowCliHelp();
        return;
    }

    var command = args[1].ToLowerInvariant();

    var handlers = new Dictionary<string, Func<string[], Task<string>>>(StringComparer.OrdinalIgnoreCase)
    {
        ["load-solution"] = TestLoadSolution,
        ["extract-method"] = TestExtractMethod,
        ["introduce-field"] = TestIntroduceField,
        ["introduce-variable"] = TestIntroduceVariable,
        ["make-field-readonly"] = TestMakeFieldReadonly,
        ["unload-solution"] = args => Task.FromResult(TestUnloadSolution(args)),
        ["clear-solution-cache"] = _ => Task.FromResult(ClearCacheCommand()),
        ["convert-to-extension-method"] = TestConvertToExtensionMethod,
        ["convert-to-static-with-parameters"] = TestConvertToStaticWithParameters,
        ["convert-to-static-with-instance"] = TestConvertToStaticWithInstance,
        ["introduce-parameter"] = TestIntroduceParameter,
        ["move-static-method"] = TestMoveStaticMethod,
        ["inline-method"] = TestInlineMethod,
        ["move-instance-method"] = TestMoveInstanceMethod,
        ["transform-setter-to-init"] = TestTransformSetterToInit,
        ["safe-delete-field"] = TestSafeDeleteField,
        ["safe-delete-method"] = TestSafeDeleteMethod,
        ["safe-delete-parameter"] = TestSafeDeleteParameter,
        ["safe-delete-variable"] = TestSafeDeleteVariable,
        ["analyze-refactoring-opportunities"] = TestAnalyzeRefactoringOpportunities,
        ["list-tools"] = _ => Task.FromResult(ListAvailableTools()),
        ["version"] = _ => Task.FromResult(ShowVersionInfo())
    };

    try
    {
        if (!handlers.TryGetValue(command, out var handler))
        {
            Console.WriteLine($"Unknown command: {command}. Use --cli list-tools to see available commands.");
            return;
        }


        var result = await handler(args);
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
    var toolsList = ListAvailableTools()
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Skip(1); // Skip heading
    foreach (var tool in toolsList)
        Console.WriteLine($"  {tool}");
    Console.WriteLine("  list-tools - List all available refactoring tools");
    Console.WriteLine("  analyze-refactoring-opportunities <filePath> [solutionPath] - Analyze code for potential refactorings");

    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  --cli load-solution ./MySolution.sln");
    Console.WriteLine("  --cli extract-method ./MyFile.cs \"10:5-15:20\" \"ExtractedMethod\"");
    Console.WriteLine("  --cli extract-method ./MyFile.cs \"10:5-15:20\" \"ExtractedMethod\" ./MySolution.sln");
    Console.WriteLine("  --cli introduce-field ./MyFile.cs \"12:10-12:25\" \"_myField\" \"private\"");
    Console.WriteLine("  --cli make-field-readonly ./MyFile.cs 15");
    Console.WriteLine("  --cli analyze-refactoring-opportunities ./MyFile.cs ./MySolution.sln");
    Console.WriteLine("  --cli version");
    Console.WriteLine();
    Console.WriteLine("Range format: \"startLine:startColumn-endLine:endColumn\" (1-based)");
    Console.WriteLine("Note: Solution path is optional. When omitted, single file mode is used with limited semantic analysis.");
}

static string ListAvailableTools()
{
    var toolNames = System.Reflection.Assembly.GetExecutingAssembly()
        .GetTypes()
        .Where(t => t.GetCustomAttributes(typeof(McpServerToolTypeAttribute), false).Length > 0)
        .SelectMany(t => t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
        .Where(m => m.GetCustomAttributes(typeof(McpServerToolAttribute), false).Length > 0)
        .Select(m => ToKebabCase(m.Name))
        .OrderBy(n => n)
        .ToArray();

    return "Available refactoring tools:\n" + string.Join("\n", toolNames);

    static string ToKebabCase(string name)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c) && i > 0)
                sb.Append('-');

            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}

static async Task<string> TestLoadSolution(string[] args)
{
    if (args.Length < 3)
        return "Error: Missing solution path. Usage: --cli load-solution <solutionPath>";

    var solutionPath = args[2];
    return await LoadSolutionTool.LoadSolution(solutionPath);
}

static async Task<string> TestExtractMethod(string[] args)
{
    if (args.Length < 5)
        return "Error: Missing arguments. Usage: --cli extract-method <filePath> <range> <methodName> [solutionPath]";

    var filePath = args[2];
    var range = args[3];
    var methodName = args[4];
    var solutionPath = args.Length > 5 ? args[5] : null;

    return await ExtractMethodTool.ExtractMethod(filePath, range, methodName, solutionPath);
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

    return await IntroduceFieldTool.IntroduceField(filePath, range, fieldName, accessModifier, solutionPath);
}

static async Task<string> TestIntroduceVariable(string[] args)
{
    if (args.Length < 5)
        return "Error: Missing arguments. Usage: --cli introduce-variable <filePath> <range> <variableName> [solutionPath]";

    var filePath = args[2];
    var range = args[3];
    var variableName = args[4];
    var solutionPath = args.Length > 5 ? args[5] : null;

    return await IntroduceVariableTool.IntroduceVariable(filePath, range, variableName, solutionPath);
}

static async Task<string> TestMakeFieldReadonly(string[] args)
{
    if (args.Length < 4)
        return "Error: Missing arguments. Usage: --cli make-field-readonly <filePath> <fieldName> [solutionPath]";

    var filePath = args[2];
    var fieldName = args[3];
    var solutionPath = args.Length > 4 ? args[4] : null;

    return await MakeFieldReadonlyTool.MakeFieldReadonly(filePath, fieldName, solutionPath);
}

static string TestUnloadSolution(string[] args)
{
    if (args.Length < 3)
        return "Error: Missing solution path. Usage: --cli unload-solution <solutionPath>";

    var solutionPath = args[2];
    return UnloadSolutionTool.UnloadSolution(solutionPath);
}

static string ClearCacheCommand()
{
    return UnloadSolutionTool.ClearSolutionCache();
}

static string ShowVersionInfo()
{
    return VersionTool.Version();
}

static async Task<string> TestConvertToExtensionMethod(string[] args)
{
    if (args.Length < 4)
        return "Error: Missing arguments. Usage: --cli convert-to-extension-method <filePath> <methodName> [solutionPath]";

    var filePath = args[2];
    var methodName = args[3];
    var solutionPath = args.Length > 4 ? args[4] : null;

    return await ConvertToExtensionMethodTool.ConvertToExtensionMethod(filePath, methodName, null, solutionPath);
}

static async Task<string> TestSafeDeleteField(string[] args)
{
    if (args.Length < 4)
        return "Error: Missing arguments. Usage: --cli safe-delete-field <filePath> <fieldName> [solutionPath]";

    var filePath = args[2];
    var fieldName = args[3];
    var solutionPath = args.Length > 4 ? args[4] : null;

    return await SafeDeleteTool.SafeDeleteField(filePath, fieldName, solutionPath);
}

static async Task<string> TestSafeDeleteMethod(string[] args)
{
    if (args.Length < 4)
        return "Error: Missing arguments. Usage: --cli safe-delete-method <filePath> <methodName> [solutionPath]";

    var filePath = args[2];
    var methodName = args[3];
    var solutionPath = args.Length > 4 ? args[4] : null;

    return await SafeDeleteTool.SafeDeleteMethod(filePath, methodName, solutionPath);
}

static async Task<string> TestSafeDeleteParameter(string[] args)
{
    if (args.Length < 5)
        return "Error: Missing arguments. Usage: --cli safe-delete-parameter <filePath> <methodName> <parameterName> [solutionPath]";

    var filePath = args[2];
    var methodName = args[3];
    var parameterName = args[4];
    var solutionPath = args.Length > 5 ? args[5] : null;

    return await SafeDeleteTool.SafeDeleteParameter(filePath, methodName, parameterName, solutionPath);
}

static async Task<string> TestSafeDeleteVariable(string[] args)
{
    if (args.Length < 4)
        return "Error: Missing arguments. Usage: --cli safe-delete-variable <filePath> <range> [solutionPath]";

    var filePath = args[2];
    var range = args[3];
    var solutionPath = args.Length > 4 ? args[4] : null;

    return await SafeDeleteTool.SafeDeleteVariable(filePath, range, solutionPath);
}

static async Task<string> TestAnalyzeRefactoringOpportunities(string[] args)
{
    if (args.Length < 3)
        return "Error: Missing arguments. Usage: --cli analyze-refactoring-opportunities <filePath> [solutionPath]";

    var filePath = args[2];
    var solutionPath = args.Length > 3 ? args[3] : null;

    return await AnalyzeRefactoringOpportunitiesTool.AnalyzeRefactoringOpportunities(filePath, solutionPath);
}

static async Task<string> TestConvertToStaticWithParameters(string[] args)
{
    if (args.Length < 4)
        return "Error: Missing arguments. Usage: --cli convert-to-static-with-parameters <filePath> <methodName> [solutionPath]";

    var filePath = args[2];
    var methodName = args[3];
    var solutionPath = args.Length > 4 ? args[4] : null;

    return await ConvertToStaticWithParametersTool.ConvertToStaticWithParameters(filePath, methodName, solutionPath);
}

static async Task<string> TestConvertToStaticWithInstance(string[] args)
{
    if (args.Length < 4)
        return "Error: Missing arguments. Usage: --cli convert-to-static-with-instance <filePath> <methodName> [instanceParamName] [solutionPath]";

    var filePath = args[2];
    var methodName = args[3];
    var instanceParam = args.Length > 4 ? args[4] : "instance";
    var solutionPath = args.Length > 5 ? args[5] : null;

    return await ConvertToStaticWithInstanceTool.ConvertToStaticWithInstance(filePath, methodName, instanceParam, solutionPath);
}

static async Task<string> TestIntroduceParameter(string[] args)
{
    if (args.Length < 6)
        return "Error: Missing arguments. Usage: --cli introduce-parameter <filePath> <methodName> <range> <parameterName> [solutionPath]";

    var filePath = args[2];
    var methodName = args[3];
    var range = args[4];
    var paramName = args[5];
    var solutionPath = args.Length > 6 ? args[6] : null;

    return await IntroduceParameterTool.IntroduceParameter(filePath, methodName, range, paramName, solutionPath);
}

static async Task<string> TestMoveStaticMethod(string[] args)
{
    if (args.Length < 6)
        return "Error: Missing arguments. Usage: --cli move-static-method <solutionPath> <filePath> <methodName> <targetClass> [targetFilePath]";

    var solutionPath = args[2];
    var filePath = args[3];
    var methodName = args[4];
    var targetClass = args[5];
    var targetFilePath = args.Length > 6 ? args[6] : null;

    return await MoveMethodsTool.MoveStaticMethod(solutionPath, filePath, methodName, targetClass, targetFilePath);
}

static async Task<string> TestMoveInstanceMethod(string[] args)
{
    if (args.Length < 7)
        return "Error: Missing arguments. Usage: --cli move-instance-method <filePath> <sourceClass> <methodName> <targetClass> <accessMember> [memberType] [solutionPath] [targetFile]";

    var filePath = args[2];
    var sourceClass = args[3];
    var methodName = args[4];
    var targetClass = args[5];
    var accessMember = args[6];
    var memberType = args.Length > 7 ? args[7] : "field";
    var solutionPath = args.Length > 8 ? args[8] : null;
    var targetFile = args.Length > 9 ? args[9] : null;

    return await MoveMethodsTool.MoveInstanceMethod(filePath, sourceClass, methodName, targetClass, accessMember, memberType, solutionPath, targetFile);
}

static async Task<string> TestTransformSetterToInit(string[] args)
{
    if (args.Length < 4)
        return "Error: Missing arguments. Usage: --cli transform-setter-to-init <filePath> <propertyName> [solutionPath]";

    var filePath = args[2];
    var propertyName = args[3];
    var solutionPath = args.Length > 4 ? args[4] : null;

    return await TransformSetterToInitTool.TransformSetterToInit(filePath, propertyName, solutionPath);
}


static async Task<string> TestInlineMethod(string[] args)
{
    if (args.Length < 5)
        return "Error: Missing arguments. Usage: --cli inline-method <solutionPath> <filePath> <methodName>";

    var solutionPath = args[2];
    var filePath = args[3];
    var methodName = args[4];

    return await InlineMethodTool.InlineMethod(solutionPath, filePath, methodName);
}
