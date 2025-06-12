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
using System.Text.Json;

// Parse command line arguments
if (args.Length > 0 && args[0] == "--cli")
{
    await RunCliMode(args);
    return;
}
if (args.Length > 0 && args[0] == "--json")
{
    await RunJsonMode(args);
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
        ["cleanup-usings"] = TestCleanupUsings,
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
    Console.WriteLine("  analyze-refactoring-opportunities <solutionPath> <filePath> - Analyze code for potential refactorings");

    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  --cli load-solution ./MySolution.sln");
    Console.WriteLine("  --cli extract-method ./MySolution.sln ./MyFile.cs \"10:5-15:20\" \"ExtractedMethod\"");
    Console.WriteLine("  --cli introduce-field ./MySolution.sln ./MyFile.cs \"12:10-12:25\" \"_myField\" \"private\"");
    Console.WriteLine("  --cli make-field-readonly ./MySolution.sln ./MyFile.cs 15");
    Console.WriteLine("  --cli cleanup-usings ./MySolution.sln ./MyFile.cs");
    Console.WriteLine("  --cli analyze-refactoring-opportunities ./MySolution.sln ./MyFile.cs");
    Console.WriteLine("  --cli version");
    Console.WriteLine();
    Console.WriteLine("JSON mode: --json ToolName '{\"param\":\"value\"}'");
    Console.WriteLine();
    Console.WriteLine("Range format: \"startLine:startColumn-endLine:endColumn\" (1-based)");
    Console.WriteLine("Note: Solution path is optional. When omitted, single file mode is used with limited semantic analysis.");
}

static async Task RunJsonMode(string[] args)
{
    if (args.Length < 3)
    {
        Console.WriteLine("Usage: --json <ToolName> '{\"param\":\"value\"}'");
        return;
    }

    var toolName = args[1];
    var json = string.Join(" ", args.Skip(2));
    Dictionary<string, string>? paramDict;
    try
    {
        paramDict = JsonSerializer.Deserialize<Dictionary<string, string>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        if (paramDict == null)
        {
            Console.WriteLine("Error: Failed to parse parameters");
            return;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error parsing JSON: {ex.Message}");
        return;
    }

    var method = System.Reflection.Assembly.GetExecutingAssembly()
        .GetTypes()
        .Where(t => t.GetCustomAttributes(typeof(McpServerToolTypeAttribute), false).Length > 0)
        .SelectMany(t => t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
        .FirstOrDefault(m => m.GetCustomAttributes(typeof(McpServerToolAttribute), false).Length > 0 &&
                             m.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));

    if (method == null)
    {
        Console.WriteLine($"Unknown tool: {toolName}. Use --cli list-tools to see available commands.");
        return;
    }

    var parameters = method.GetParameters();
    var invokeArgs = new object?[parameters.Length];
    for (int i = 0; i < parameters.Length; i++)
    {
        var p = parameters[i];
        if (paramDict.TryGetValue(p.Name!, out var value))
        {
            invokeArgs[i] = value;
        }
        else if (p.HasDefaultValue)
        {
            invokeArgs[i] = p.DefaultValue;
        }
        else
        {
            Console.WriteLine($"Error: Missing required parameter '{p.Name}'");
            return;
        }
    }

    try
    {
        var result = method.Invoke(null, invokeArgs);
        if (result is Task<string> taskStr)
        {
            Console.WriteLine(await taskStr);
        }
        else if (result is Task task)
        {
            await task;
            Console.WriteLine("Done");
        }
        else if (result != null)
        {
            Console.WriteLine(result.ToString());
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error executing tool: {ex.Message}");
    }
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
    if (args.Length < 6)
        return "Error: Missing arguments. Usage: --cli extract-method <solutionPath> <filePath> <range> <methodName>";

    var solutionPath = args[2];
    var filePath = args[3];
    var range = args[4];
    var methodName = args[5];

    return await ExtractMethodTool.ExtractMethod(solutionPath, filePath, range, methodName);
}

static async Task<string> TestIntroduceField(string[] args)
{
    if (args.Length < 6)
        return "Error: Missing arguments. Usage: --cli introduce-field <solutionPath> <filePath> <range> <fieldName> [accessModifier]";

    var solutionPath = args[2];
    var filePath = args[3];
    var range = args[4];
    var fieldName = args[5];
    var accessModifier = args.Length > 6 ? args[6] : "private";

    return await IntroduceFieldTool.IntroduceField(solutionPath, filePath, range, fieldName, accessModifier);
}

static async Task<string> TestIntroduceVariable(string[] args)
{
    if (args.Length < 6)
        return "Error: Missing arguments. Usage: --cli introduce-variable <solutionPath> <filePath> <range> <variableName>";

    var solutionPath = args[2];
    var filePath = args[3];
    var range = args[4];
    var variableName = args[5];

    return await IntroduceVariableTool.IntroduceVariable(solutionPath, filePath, range, variableName);
}

static async Task<string> TestMakeFieldReadonly(string[] args)
{
    if (args.Length < 5)
        return "Error: Missing arguments. Usage: --cli make-field-readonly <solutionPath> <filePath> <fieldName>";

    var solutionPath = args[2];
    var filePath = args[3];
    var fieldName = args[4];

    return await MakeFieldReadonlyTool.MakeFieldReadonly(solutionPath, filePath, fieldName);
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
    if (args.Length < 5)
        return "Error: Missing arguments. Usage: --cli convert-to-extension-method <solutionPath> <filePath> <methodName>";

    var solutionPath = args[2];
    var filePath = args[3];
    var methodName = args[4];

    return await ConvertToExtensionMethodTool.ConvertToExtensionMethod(solutionPath, filePath, methodName, null);
}

static async Task<string> TestSafeDeleteField(string[] args)
{
    if (args.Length < 5)
        return "Error: Missing arguments. Usage: --cli safe-delete-field <solutionPath> <filePath> <fieldName>";

    var solutionPath = args[2];
    var filePath = args[3];
    var fieldName = args[4];

    return await SafeDeleteTool.SafeDeleteField(solutionPath, filePath, fieldName);
}

static async Task<string> TestSafeDeleteMethod(string[] args)
{
    if (args.Length < 5)
        return "Error: Missing arguments. Usage: --cli safe-delete-method <solutionPath> <filePath> <methodName>";

    var solutionPath = args[2];
    var filePath = args[3];
    var methodName = args[4];

    return await SafeDeleteTool.SafeDeleteMethod(solutionPath, filePath, methodName);
}

static async Task<string> TestSafeDeleteParameter(string[] args)
{
    if (args.Length < 6)
        return "Error: Missing arguments. Usage: --cli safe-delete-parameter <solutionPath> <filePath> <methodName> <parameterName>";

    var solutionPath = args[2];
    var filePath = args[3];
    var methodName = args[4];
    var parameterName = args[5];

    return await SafeDeleteTool.SafeDeleteParameter(solutionPath, filePath, methodName, parameterName);
}

static async Task<string> TestSafeDeleteVariable(string[] args)
{
    if (args.Length < 5)
        return "Error: Missing arguments. Usage: --cli safe-delete-variable <solutionPath> <filePath> <range>";

    var solutionPath = args[2];
    var filePath = args[3];
    var range = args[4];

    return await SafeDeleteTool.SafeDeleteVariable(solutionPath, filePath, range);
}

static async Task<string> TestCleanupUsings(string[] args)
{
    if (args.Length < 4)
        return "Error: Missing arguments. Usage: --cli cleanup-usings <solutionPath> <filePath>";

    var solutionPath = args[2];
    var filePath = args[3];

    return await CleanupUsingsTool.CleanupUsings(solutionPath, filePath);
}

static async Task<string> TestAnalyzeRefactoringOpportunities(string[] args)
{
    if (args.Length < 4)
        return "Error: Missing arguments. Usage: --cli analyze-refactoring-opportunities <solutionPath> <filePath>";

    var solutionPath = args[2];
    var filePath = args[3];

    return await AnalyzeRefactoringOpportunitiesTool.AnalyzeRefactoringOpportunities(solutionPath, filePath);
}

static async Task<string> TestConvertToStaticWithParameters(string[] args)
{
    if (args.Length < 5)
        return "Error: Missing arguments. Usage: --cli convert-to-static-with-parameters <solutionPath> <filePath> <methodName>";

    var solutionPath = args[2];
    var filePath = args[3];
    var methodName = args[4];

    return await ConvertToStaticWithParametersTool.ConvertToStaticWithParameters(solutionPath, filePath, methodName);
}

static async Task<string> TestConvertToStaticWithInstance(string[] args)
{
    if (args.Length < 5)
        return "Error: Missing arguments. Usage: --cli convert-to-static-with-instance <solutionPath> <filePath> <methodName> [instanceParamName]";

    var solutionPath = args[2];
    var filePath = args[3];
    var methodName = args[4];
    var instanceParam = args.Length > 5 ? args[5] : "instance";

    return await ConvertToStaticWithInstanceTool.ConvertToStaticWithInstance(solutionPath, filePath, methodName, instanceParam);
}

static async Task<string> TestIntroduceParameter(string[] args)
{
    if (args.Length < 7)
        return "Error: Missing arguments. Usage: --cli introduce-parameter <solutionPath> <filePath> <methodName> <range> <parameterName>";

    var solutionPath = args[2];
    var filePath = args[3];
    var methodName = args[4];
    var range = args[5];
    var paramName = args[6];

    return await IntroduceParameterTool.IntroduceParameter(solutionPath, filePath, methodName, range, paramName);
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
    if (args.Length < 8)
        return "Error: Missing arguments. Usage: --cli move-instance-method <solutionPath> <filePath> <sourceClass> <methodName> <targetClass> <accessMember> [memberType] [targetFile]";

    var solutionPath = args[2];
    var filePath = args[3];
    var sourceClass = args[4];
    var methodName = args[5];
    var targetClass = args[6];
    var accessMember = args[7];
    var memberType = args.Length > 8 ? args[8] : "field";
    var targetFile = args.Length > 9 ? args[9] : null;

    return await MoveMethodsTool.MoveInstanceMethod(solutionPath, filePath, sourceClass, methodName, targetClass, accessMember, memberType, targetFile);
}

static async Task<string> TestTransformSetterToInit(string[] args)
{
    if (args.Length < 5)
        return "Error: Missing arguments. Usage: --cli transform-setter-to-init <solutionPath> <filePath> <propertyName>";

    var solutionPath = args[2];
    var filePath = args[3];
    var propertyName = args[4];

    return await TransformSetterToInitTool.TransformSetterToInit(solutionPath, filePath, propertyName);
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
