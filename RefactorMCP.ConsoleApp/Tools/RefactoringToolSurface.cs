using ModelContextProtocol;
using ModelContextProtocol.Server;
using RefactorMCP.ConsoleApp.Tools;
using System.ComponentModel;
using System.Reflection;
using System.Text;

[McpServerToolType]
public static class RefactoringToolSurface
{
    [McpServerTool, Description("List the compact RefactorMCP operation groups and the operation names accepted by each grouped tool.")]
    public static string ListRefactoringOperations()
        => RefactoringToolCatalog.FormatOperationGroups();

    [McpServerTool, Description("Manage the refactoring session. Operations: load-solution, unload-solution, clear-solution-cache, version, reset-move-history.")]
    public static Task<string> ManageSolution(
        [Description("Operation to run: load-solution, unload-solution, clear-solution-cache, version, or reset-move-history.")] string operation,
        [Description("Absolute path to the solution file (.sln), required by load-solution and unload-solution.")] string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return Normalize(operation) switch
        {
            "load-solution" => LoadSolutionTool.LoadSolution(Require(solutionPath), null, cancellationToken),
            "unload-solution" => Task.FromResult(UnloadSolutionTool.UnloadSolution(Require(solutionPath))),
            "clear-solution-cache" => Task.FromResult(UnloadSolutionTool.ClearSolutionCache()),
            "version" => Task.FromResult(VersionTool.Version()),
            "reset-move-history" => Task.FromResult(MoveMethodTool.ResetMoveHistory()),
            _ => UnknownOperation(operation, nameof(ManageSolution))
        };
    }

    [McpServerTool, Description("Analyze code without modifying it. Operations: refactoring-opportunities, class-lengths.")]
    public static Task<string> AnalyzeCode(
        [Description("Operation to run: refactoring-opportunities or class-lengths.")] string operation,
        [Description("Absolute path to the solution file (.sln).")] string solutionPath,
        [Description("Path to the C# file, required by refactoring-opportunities.")] string? filePath = null,
        CancellationToken cancellationToken = default)
    {
        return Normalize(operation) switch
        {
            "refactoring-opportunities" => AnalyzeRefactoringOpportunitiesTool.AnalyzeRefactoringOpportunities(solutionPath, Require(filePath), cancellationToken),
            "class-lengths" => ClassLengthMetricsTool.ListClassLengths(solutionPath, cancellationToken),
            _ => UnknownOperation(operation, nameof(AnalyzeCode))
        };
    }

    [McpServerTool, Description("Introduce new code from a selected expression. Operations: field, parameter, variable.")]
    public static Task<string> IntroduceCode(
        [Description("Operation to run: field, parameter, or variable.")] string operation,
        [Description("Absolute path to the solution file (.sln).")] string solutionPath,
        [Description("Path to the C# file to refactor.")] string filePath,
        [Description("Selection range in line:column-line:column format.")] string selectionRange,
        [Description("Name of the field, parameter, or variable to introduce.")] string name,
        [Description("Method name, required by the parameter operation.")] string? methodName = null,
        [Description("Field access modifier, used by the field operation.")] string accessModifier = "private")
    {
        return Normalize(operation) switch
        {
            "field" => IntroduceFieldTool.IntroduceField(solutionPath, filePath, selectionRange, name, accessModifier),
            "parameter" => IntroduceParameterTool.IntroduceParameter(solutionPath, filePath, Require(methodName), selectionRange, name),
            "variable" => IntroduceVariableTool.IntroduceVariable(solutionPath, filePath, selectionRange, name),
            _ => UnknownOperation(operation, nameof(IntroduceCode))
        };
    }

    [McpServerTool, Description("Transform members or file structure in place. Operations: extract-method, inline-method, convert-to-static-with-parameters, convert-to-static-with-instance, convert-to-extension-method, make-field-readonly, setter-to-init, constructor-injection, use-interface, rename-symbol, cleanup-usings, feature-flag.")]
    public static Task<string> TransformCode(
        [Description("Operation to run. Use list-refactoring-operations for the complete set.")] string operation,
        [Description("Absolute path to the solution file (.sln).")] string solutionPath,
        [Description("Path to the C# file to refactor.")] string filePath,
        [Description("Method name used by method-oriented operations.")] string? methodName = null,
        [Description("Selection range in line:column-line:column format, required by extract-method.")] string? selectionRange = null,
        [Description("New method name for extract-method, or new symbol name for rename-symbol.")] string? name = null,
        [Description("Instance parameter name for convert-to-static-with-instance.")] string? instanceParameterName = null,
        [Description("Extension class name for convert-to-extension-method.")] string? extensionClass = null,
        [Description("Field name for make-field-readonly.")] string? fieldName = null,
        [Description("Property name for setter-to-init.")] string? propertyName = null,
        [Description("Method/parameter pairs for constructor-injection.")] ConstructorInjectionTool.MethodParameterPair[]? methodParameters = null,
        [Description("Use a public property instead of a private field for constructor-injection.")] bool useProperty = false,
        [Description("Parameter name for use-interface.")] string? parameterName = null,
        [Description("Interface name for use-interface.")] string? interfaceName = null,
        [Description("Symbol name to rename for rename-symbol.")] string? symbolName = null,
        [Description("Line number for rename-symbol disambiguation.")] int? line = null,
        [Description("Column number for rename-symbol disambiguation.")] int? column = null,
        [Description("Feature flag name for feature-flag.")] string? flagName = null)
    {
        return Normalize(operation) switch
        {
            "extract-method" => ExtractMethodTool.ExtractMethod(solutionPath, filePath, Require(selectionRange), Require(name)),
            "inline-method" => InlineMethodTool.InlineMethod(solutionPath, filePath, Require(methodName)),
            "convert-to-static-with-parameters" => ConvertToStaticWithParametersTool.ConvertToStaticWithParameters(solutionPath, filePath, Require(methodName)),
            "convert-to-static-with-instance" => ConvertToStaticWithInstanceTool.ConvertToStaticWithInstance(solutionPath, filePath, Require(methodName), Require(instanceParameterName)),
            "convert-to-extension-method" => ConvertToExtensionMethodTool.ConvertToExtensionMethod(solutionPath, filePath, Require(methodName), extensionClass),
            "make-field-readonly" => MakeFieldReadonlyTool.MakeFieldReadonly(solutionPath, filePath, Require(fieldName)),
            "setter-to-init" => TransformSetterToInitTool.TransformSetterToInit(solutionPath, filePath, Require(propertyName)),
            "constructor-injection" => ConstructorInjectionTool.ConvertToConstructorInjection(solutionPath, filePath, methodParameters ?? throw Missing(nameof(methodParameters)), useProperty),
            "use-interface" => UseInterfaceTool.UseInterface(solutionPath, filePath, Require(methodName), Require(parameterName), Require(interfaceName)),
            "rename-symbol" => RenameSymbolTool.RenameSymbol(solutionPath, filePath, Require(symbolName), Require(name), line, column),
            "cleanup-usings" => CleanupUsingsTool.CleanupUsings(solutionPath, filePath),
            "feature-flag" => FeatureFlagRefactorTool.FeatureFlagRefactor(solutionPath, filePath, Require(flagName)),
            _ => UnknownOperation(operation, nameof(TransformCode))
        };
    }

    [McpServerTool, Description("Move methods or types. Operations: static-method, instance-method, multiple-methods-static, multiple-methods-instance, make-static-then-move, type-to-file.")]
    public static Task<string> MoveCode(
        [Description("Operation to run: static-method, instance-method, multiple-methods-static, multiple-methods-instance, make-static-then-move, or type-to-file.")] string operation,
        [Description("Absolute path to the solution file (.sln).")] string solutionPath,
        [Description("Path to the C# file containing the member or type.")] string filePath,
        [Description("Name of the source class, required by instance and multiple method moves.")] string? sourceClass = null,
        [Description("Single method name for static-method and make-static-then-move.")] string? methodName = null,
        [Description("Method names for instance and multiple method moves.")] string[]? methodNames = null,
        [Description("Name of the target class.")] string? targetClass = null,
        [Description("Path to the target file, optional for method moves.")] string? targetFilePath = null,
        [Description("Dependencies to inject through the constructor for instance-method.")] string[]? constructorInjections = null,
        [Description("Dependencies to keep as parameters for instance-method.")] string[]? parameterInjections = null,
        [Description("Name of the type to move for type-to-file.")] string? typeName = null,
        [Description("Instance parameter name for make-static-then-move.")] string instanceParameterName = "instance")
    {
        return Normalize(operation) switch
        {
            "static-method" => MoveMethodTool.MoveStaticMethod(solutionPath, filePath, Require(methodName), Require(targetClass), targetFilePath),
            "instance-method" => MoveMethodTool.MoveInstanceMethod(solutionPath, filePath, Require(sourceClass), Require(methodNames), Require(targetClass), targetFilePath, constructorInjections ?? [], parameterInjections ?? []),
            "multiple-methods-static" => MoveMultipleMethodsTool.MoveMultipleMethodsStatic(solutionPath, filePath, Require(sourceClass), Require(methodNames), Require(targetClass), targetFilePath),
            "multiple-methods-instance" => MoveMultipleMethodsTool.MoveMultipleMethodsInstance(solutionPath, filePath, Require(sourceClass), Require(methodNames), Require(targetClass), targetFilePath),
            "make-static-then-move" => MakeStaticThenMoveTool.MakeStaticThenMove(solutionPath, filePath, Require(methodName), Require(targetClass), instanceParameterName, targetFilePath),
            "type-to-file" => MoveTypeToFileTool.MoveToSeparateFile(solutionPath, filePath, Require(typeName)),
            _ => UnknownOperation(operation, nameof(MoveCode))
        };
    }

    [McpServerTool, Description("Generate structural design-pattern helpers. Operations: extract-interface, extract-decorator, create-adapter, add-observer.")]
    public static Task<string> GeneratePattern(
        [Description("Operation to run: extract-interface, extract-decorator, create-adapter, or add-observer.")] string operation,
        [Description("Absolute path to the solution file (.sln).")] string solutionPath,
        [Description("Path to the C# file to refactor.")] string filePath,
        [Description("Class name used by pattern operations.")] string className,
        [Description("Method name used by decorator, adapter, and observer operations.")] string? methodName = null,
        [Description("Comma-separated member list for extract-interface.")] string? memberList = null,
        [Description("Path to write the generated interface file for extract-interface.")] string? interfaceFilePath = null,
        [Description("Decorator class name for extract-decorator (reserved; current implementation names the decorator automatically).")] string? decoratorName = null,
        [Description("Adapter class name for create-adapter.")] string? adapterName = null,
        [Description("Event name for add-observer.")] string? eventName = null)
    {
        return Normalize(operation) switch
        {
            "extract-interface" => ExtractInterfaceTool.ExtractInterface(solutionPath, filePath, className, Require(memberList), Require(interfaceFilePath)),
            "extract-decorator" => ExtractDecoratorTool.ExtractDecorator(solutionPath, filePath, className, Require(methodName)),
            "create-adapter" => CreateAdapterTool.CreateAdapter(solutionPath, filePath, className, Require(methodName), Require(adapterName)),
            "add-observer" => AddObserverTool.AddObserver(solutionPath, filePath, className, Require(methodName), Require(eventName)),
            _ => UnknownOperation(operation, nameof(GeneratePattern))
        };
    }

    [McpServerTool, Description("Safely delete unused members or locals after dependency checks. Operations: field, method, parameter, variable.")]
    public static Task<string> SafeDelete(
        [Description("Operation to run: field, method, parameter, or variable.")] string operation,
        [Description("Absolute path to the solution file (.sln).")] string solutionPath,
        [Description("Path to the C# file to refactor.")] string filePath,
        [Description("Field, method, or parameter name, depending on the operation.")] string? name = null,
        [Description("Method name required when deleting a parameter.")] string? methodName = null,
        [Description("Selection range required when deleting a variable.")] string? selectionRange = null)
    {
        return Normalize(operation) switch
        {
            "field" => SafeDeleteTool.SafeDeleteField(solutionPath, filePath, Require(name)),
            "method" => SafeDeleteTool.SafeDeleteMethod(solutionPath, filePath, Require(name)),
            "parameter" => SafeDeleteTool.SafeDeleteParameter(solutionPath, filePath, Require(methodName), Require(name)),
            "variable" => SafeDeleteTool.SafeDeleteVariable(solutionPath, filePath, Require(selectionRange)),
            _ => UnknownOperation(operation, nameof(SafeDelete))
        };
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();

    private static string Require(string? value, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(value))] string? expression = null)
        => string.IsNullOrWhiteSpace(value) ? throw Missing(expression ?? "parameter") : value;

    private static T[] Require<T>(T[]? value, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(value))] string? expression = null)
        => value is null || value.Length == 0 ? throw Missing(expression ?? "parameter") : value;

    private static McpException Missing(string parameter)
        => new($"Missing required parameter: {parameter}.");

    private static Task<string> UnknownOperation(string operation, string toolName)
        => Task.FromException<string>(new McpException($"Unknown operation '{operation}' for {toolName}. Use list-refactoring-operations to see valid operations."));
}

public static class RefactoringToolCatalog
{
    public static readonly Type[] ExposedToolTypes = [typeof(RefactoringToolSurface)];

    public static readonly IReadOnlyDictionary<string, string[]> OperationGroups = new Dictionary<string, string[]>
    {
        ["list-refactoring-operations"] = [],
        ["manage-solution"] = ["load-solution", "unload-solution", "clear-solution-cache", "version", "reset-move-history"],
        ["analyze-code"] = ["refactoring-opportunities", "class-lengths"],
        ["introduce-code"] = ["field", "parameter", "variable"],
        ["transform-code"] = ["extract-method", "inline-method", "convert-to-static-with-parameters", "convert-to-static-with-instance", "convert-to-extension-method", "make-field-readonly", "setter-to-init", "constructor-injection", "use-interface", "rename-symbol", "cleanup-usings", "feature-flag"],
        ["move-code"] = ["static-method", "instance-method", "multiple-methods-static", "multiple-methods-instance", "make-static-then-move", "type-to-file"],
        ["generate-pattern"] = ["extract-interface", "extract-decorator", "create-adapter", "add-observer"],
        ["safe-delete"] = ["field", "method", "parameter", "variable"]
    };

    public static IEnumerable<MethodInfo> GetExposedToolMethods()
        => ExposedToolTypes
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => m.GetCustomAttributes(typeof(McpServerToolAttribute), false).Any());

    public static IEnumerable<MethodInfo> GetLegacyToolMethods()
        => Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => !ExposedToolTypes.Contains(t) && t.GetCustomAttributes(typeof(McpServerToolTypeAttribute), false).Any())
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => m.GetCustomAttributes(typeof(McpServerToolAttribute), false).Any());

    public static string FormatOperationGroups()
    {
        var sb = new StringBuilder("RefactorMCP exposes a compact grouped MCP surface. Call a group tool with one of its operations:\n");
        foreach (var (group, operations) in OperationGroups)
        {
            sb.Append("- ").Append(group);
            if (operations.Length > 0)
                sb.Append(": ").AppendJoin(", ", operations);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    public static string ToKebabCase(string name)
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
